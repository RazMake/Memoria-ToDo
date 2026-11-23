// Shared fake of the part of GitHub's Actions REST API that `gh api` reaches, for the tests of
// full-run-check.mjs and release.mjs. Build the fake's `execute(file, args)` with createFakeActionsApi
// and hand it to code that runs gh. It serves:
//   gh api <repo>/actions/workflows/build.yml/runs?...   -> { workflow_runs: <every run given, unfiltered> }
//   gh api <repo>/actions/runs/<id>/jobs?...             -> { jobs }, only the newest attempt's when the query has filter=latest
//   gh api <repo>/actions/runs/<id>                      -> one run
//   gh api ... POST <repo>/actions/workflows/build.yml/dispatches -> `dispatch` (default: nothing)
// It never filters runs itself, so the code under test must.
import assert from 'node:assert/strict';

export const repositoryName = 'example/tasks';
export const buildWorkflowPath = '.github/workflows/build.yml';

export const restRun = (overrides = {}) => ({
  id: 30,
  path: buildWorkflowPath,
  event: 'workflow_dispatch',
  head_sha: 'a1'.repeat(20),
  head_branch: 'main',
  status: 'completed',
  conclusion: 'success',
  run_attempt: 1,
  html_url: `https://github.com/${repositoryName}/actions/runs/${overrides.id ?? 30}`,
  ...overrides,
});

export const restJob = (name, conclusion, runAttempt = 1) => ({ name, status: conclusion ? 'completed' : 'in_progress', conclusion, run_attempt: runAttempt });

export function createFakeActionsApi({ runs = [], jobs = {}, dispatch } = {}) {
  const calls = [];
  const execute = async (file, args) => {
    calls.push([file, ...args]);
    assert.equal(file, 'gh', `Unexpected program ${file}`);
    assert.equal(args[0], 'api', `Unexpected gh command ${args.join(' ')}`);
    const endpoint = args.find((arg, index) => index > 0 && /^repos\//.test(arg)) ?? '';
    if (args.includes('POST')) {
      assert.match(endpoint, /\/actions\/workflows\/build\.yml\/dispatches/);
      return dispatch === undefined ? '' : JSON.stringify(dispatch);
    }
    if (/\/actions\/workflows\/build\.yml\/runs/.test(endpoint)) return JSON.stringify({ workflow_runs: runs });
    const jobsOf = /\/actions\/runs\/(\d+)\/jobs/.exec(endpoint);
    if (jobsOf) {
      const all = jobs[jobsOf[1]];
      assert.ok(all, `No jobs were made up for run ${jobsOf[1]}`);
      const newest = Math.max(...all.map((job) => job.run_attempt));
      return JSON.stringify({ jobs: /filter=latest/.test(endpoint) ? all.filter((job) => job.run_attempt === newest) : all });
    }
    const one = /\/actions\/runs\/(\d+)(\?|$)/.exec(endpoint);
    if (one) return JSON.stringify(runs.find((run) => String(run.id) === one[1]) ?? assert.fail(`No run ${one[1]} was made up`));
    return assert.fail(`Unexpected gh api call ${args.join(' ')}`);
  };
  return { execute, calls };
}
