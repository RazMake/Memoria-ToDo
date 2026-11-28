#!/usr/bin/env node
// Checks whether the selected platform's test run passed for an exact commit.
// Design: docs/architecture/releasing.md and docs/architecture/technology-stack.md.
// Used by the Plan job of the release workflow, so a release doesn't run the same tests twice on one commit.
import { pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, messageOf } from './github-runs.mjs';
import { extractPlatform, platformOf } from './release-platforms.mjs';

const usage = 'usage: node .agents/full-run-check.mjs --repo <owner/name> --commit <40 hex digits> [--platform desktop|android]';
const options = ['--repo', '--commit'];

// The newest eligible run decides across automatic and manual runs of the exact commit.
export async function checkFullRun(commit, source, platform = platformOf()) {
  const counted = (await source.runs(commit, platform.workflow))
    .filter((run) => run.path === `.github/workflows/${platform.workflow}` && ['push', 'workflow_dispatch'].includes(run.event) && run.headSha === commit);
  if (!counted.length) return { state: 'none' };
  const run = counted.reduce((newest, next) => (next.databaseId > newest.databaseId ? next : newest));
  if (run.status !== 'completed') return { state: 'running', run };
  if (run.conclusion !== 'success') return { state: 'failed', run };
  const jobs = await source.jobs(run.databaseId);
  const passed = platform.jobs.every(name => {
    const matches = jobs.filter(job => job.name === name);
    return matches.length > 0 && matches.every(job => job.conclusion === 'success');
  });
  return { state: passed ? 'passed' : 'failed', run };
}

/** Reads the runs and jobs `checkFullRun` judges through the GitHub component. */
export function createSource(github) {
  return {
    runs: (commit, workflowFile = platformOf().workflow) => github.runs(workflowFile, commit),
    jobs: (databaseId) => github.jobs(databaseId),
  };
}

function parseArguments(args) {
  const extracted = extractPlatform(args);
  args = extracted.args;
  const values = {};
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i].replace(/^--/, '');
    if (!options.includes(args[i]) || i + 1 >= args.length || name in values) throw new Error(usage);
    values[name] = args[i + 1];
  }
  if (Object.keys(values).length !== options.length || !/^[0-9a-f]{40}$/.test(values.commit) || !/^[\w.-]+\/[\w.-]+$/.test(values.repo)) throw new Error(usage);
  return { ...values, platform: extracted.platform };
}

export async function run(args, source) {
  try {
    const { commit, platform } = parseArguments(args);
    const result = await checkFullRun(commit, source, platform);
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
