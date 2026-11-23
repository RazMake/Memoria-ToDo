#!/usr/bin/env node
// Decides whether a manual run of the build workflow proved that a commit passed every test.
// Design: docs/architecture/releasing.md and docs/architecture/technology-stack.md.
// Used by the release command (before tagging) and by the release job (before publishing).
import { execFile } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const workflowFile = 'build.yml';
const workflowPath = `.github/workflows/${workflowFile}`;
const fullJobName = 'full-tests';
const usage = 'usage: node .agents/full-run-check.mjs --repo <owner/name> --commit <40 hex digits> --ref <tag or branch>';
const options = ['--repo', '--commit', '--ref'];

export const messageOf = (error) => String(error.message).trim().replace(/\s*\n\s*/g, ' ');

// Only a finished manual run of this workflow on this exact commit counts; the newest such run decides.
export async function checkFullRun(commit, source) {
  const counted = (await source.runs(commit))
    .filter((run) => run.path === workflowPath && run.event === 'workflow_dispatch' && run.headSha === commit);
  if (!counted.length) return { state: 'none' };
  const run = counted.reduce((newest, next) => (next.databaseId > newest.databaseId ? next : newest));
  if (run.status !== 'completed') return { state: 'running', run };
  if (run.conclusion !== 'success') return { state: 'failed', run };
  const jobs = await source.jobs(run.databaseId);
  return { state: jobs.some((job) => job.name === fullJobName && job.conclusion === 'success') ? 'passed' : 'failed', run };
}

const invalid = (what) => new Error(`GitHub returned an invalid ${what} response.`);
const text = (value) => typeof value === 'string';
const textOrNull = (value) => value === null || text(value);

function runOf(raw) {
  if (!Number.isSafeInteger(raw?.id) || !text(raw.path) || !text(raw.event) || !text(raw.head_sha)
    || !text(raw.status) || !textOrNull(raw.conclusion) || !text(raw.html_url)) throw invalid('workflow run');
  return { databaseId: raw.id, path: raw.path, event: raw.event, headSha: raw.head_sha, status: raw.status, conclusion: raw.conclusion, url: raw.html_url };
}

// Reads GitHub through `gh api`; execute(file, args) resolves to stdout.
export function createSource(execute, repository) {
  const get = async (path) => JSON.parse(await execute('gh', ['api', `repos/${repository}/${path}`]));
  return {
    async runs(commit) {
      const body = await get(`actions/workflows/${workflowFile}/runs?head_sha=${commit}&event=workflow_dispatch&per_page=100`);
      if (!Array.isArray(body?.workflow_runs)) throw invalid('workflow runs');
      return body.workflow_runs.map(runOf);
    },
    // filter=latest keeps only the newest attempt, so a rerun replaces the attempt it repeats.
    async jobs(databaseId) {
      const body = await get(`actions/runs/${databaseId}/jobs?filter=latest&per_page=100`);
      if (!Array.isArray(body?.jobs) || body.jobs.some((job) => !text(job?.name) || !textOrNull(job.conclusion))) throw invalid('jobs');
      return body.jobs.map((job) => ({ name: job.name, conclusion: job.conclusion }));
    },
    async run(databaseId) {
      return runOf(await get(`actions/runs/${databaseId}`));
    },
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

const verdicts = {
  passed: ({ commit, run }) => `Commit ${commit} passed every test in the manual run ${run.url}.`,
  running: ({ commit, run }) => `The manual run ${run.url} of commit ${commit} is still running. Wait for it to finish, then retry.`,
  failed: ({ commit, run }) => `The manual run ${run.url} of commit ${commit} did not pass every test. Fix the problem and start a new manual run.`,
  none: ({ commit, ref }) => `No manual run of commit ${commit} exists. Start one with: gh workflow run ${workflowFile} --ref ${ref}`,
};

export async function run(args, source) {
  try {
    const { commit, ref } = parseArguments(args);
    const result = await checkFullRun(commit, source);
    const message = verdicts[result.state]({ commit, ref, run: result.run });
    return result.state === 'passed' ? { code: 0, stdout: `${message}\n`, stderr: '' } : { code: 1, stdout: '', stderr: `${message}\n` };
  } catch (error) {
    return { code: 1, stdout: '', stderr: `${messageOf(error)}\n` };
  }
}

const execute = (file, args) => new Promise((resolve, reject) => {
  execFile(file, args, { encoding: 'utf8', maxBuffer: 8 * 1024 * 1024, env: { ...process.env, GH_PROMPT_DISABLED: '1', GH_PAGER: 'cat' } }, (error, stdout, stderr) => {
    if (error) reject(new Error(`${file} ${args.slice(0, 2).join(' ')} failed: ${stderr.trim() || error.message}`));
    else resolve(stdout);
  });
});

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const args = process.argv.slice(2);
  let repository;
  try {
    repository = parseArguments(args).repo;
  } catch {
    // run() reports the same usage error before it uses the source.
  }
  const result = await run(args, createSource(execute, repository));
  process.stdout.write(result.stdout);
  process.stderr.write(result.stderr);
  process.exitCode = result.code;
}
