#!/usr/bin/env node
// Decides whether a Run All tests run proved that a commit passed every test.
// Design: docs/architecture/releasing.md and docs/architecture/technology-stack.md.
// Used by the Plan job of the release workflow, so a release doesn't run the same tests twice on one commit.
import { pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, messageOf } from './github-runs.mjs';

const workflowFile = 'all-tests.yml';
const workflowPath = `.github/workflows/${workflowFile}`;
const fullJobName = 'full-tests';
const usage = 'usage: node .agents/full-run-check.mjs --repo <owner/name> --commit <40 hex digits>';
const options = ['--repo', '--commit'];

// The newest eligible run decides across automatic and manual runs of the exact commit.
export async function checkFullRun(commit, source) {
  const counted = (await source.runs(commit))
    .filter((run) => run.path === workflowPath && ['push', 'workflow_dispatch'].includes(run.event) && run.headSha === commit);
  if (!counted.length) return { state: 'none' };
  const run = counted.reduce((newest, next) => (next.databaseId > newest.databaseId ? next : newest));
  if (run.status !== 'completed') return { state: 'running', run };
  if (run.conclusion !== 'success') return { state: 'failed', run };
  const jobs = await source.jobs(run.databaseId);
  return { state: jobs.some((job) => job.name === fullJobName && job.conclusion === 'success') ? 'passed' : 'failed', run };
}

/** Reads the runs and jobs `checkFullRun` judges through the GitHub component. */
export function createSource(github) {
  return {
    runs: (commit) => github.runs(workflowFile, commit),
    jobs: (databaseId) => github.jobs(databaseId),
  };
}

function parseArguments(args) {
  const values = {};
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i].replace(/^--/, '');
    if (!options.includes(args[i]) || i + 1 >= args.length || name in values) throw new Error(usage);
    values[name] = args[i + 1];
  }
  if (Object.keys(values).length !== options.length || !/^[0-9a-f]{40}$/.test(values.commit) || !/^[\w.-]+\/[\w.-]+$/.test(values.repo)) throw new Error(usage);
  return values;
}

export async function run(args, source) {
  try {
    const { commit } = parseArguments(args);
    const result = await checkFullRun(commit, source);
    return { code: 0, stdout: result.state === 'passed' ? `${result.run.url}\n` : '', stderr: '' };
  } catch (error) {
    return { code: 1, stdout: '', stderr: `${messageOf(error)}\n` };
  }
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const args = process.argv.slice(2);
  let repository;
  try {
    repository = parseArguments(args).repo;
  } catch {
    // run() reports the same usage error before it uses the source.
  }
  const result = await run(args, createSource(createGitHubRuns(createCommands(process.cwd()), { repository })));
  process.stdout.write(result.stdout);
  process.stderr.write(result.stderr);
  process.exitCode = result.code;
}
