// The one fake GitHub, for the tests of release.mjs, run-all-tests.mjs, github-runs.mjs, and full-run-check.mjs.
// createFakeGitHub(options) gives the `execute(file, args, settings)` that github-runs.mjs runs `gh` and `git remote get-url`
// through, and a clock to hand to github-runs.mjs. It never filters runs by commit, event, or workflow file itself (except that
// only all-tests.yml lists runs), so the code under test must. Anything else it is asked fails the test.
//
// What it serves (the command lines are the contract github-runs.mjs follows):
//   git remote get-url origin                       -> options.origin
//   git remote get-url --push --all origin          -> options.pushOrigin (default: options.origin)
//   gh auth status --hostname github.com            -> nothing, or fails with options.authError
//   gh api repos/<repo>/actions/workflows/all-tests.yml/runs?...       -> { workflow_runs: every run given }
//   gh api repos/<repo>/actions/workflows/<other>.yml/runs?...         -> { workflow_runs: [] }
//   gh api repos/<repo>/actions/runs/<id>/jobs?...   -> { jobs }, only the newest attempt's when the query has filter=latest
//   gh api repos/<repo>/actions/runs/<id>            -> one run; see `afterReads`
//   gh api -X POST repos/<repo>/actions/workflows/<file>/dispatches -f ref=<ref> [-f inputs[<name>]=<value> ...] -F return_run_details=true
//                                                    -> options.dispatch as JSON (default { workflow_run_id: <the first run's id> })
//   gh release view <tag> --repo <repo> --json ...   -> options.release as JSON, or fails when none is given
//   gh release download <tag> --repo <repo> --pattern <name> --dir <dir> -> writes <dir>/<name> holding options.installer
// Options: repository, origin, pushOrigin, authError, apiError (every `gh api` fails with it), runs (restRun objects), jobs ({ <run id>: [restJob objects] }), dispatch, release,
// installer, and afterReads: { <run id>: [overrides, ...] }, where the Nth read of that run applies the Nth override (the last repeats),
// so a run can go from queued to completed, or stay unfinished for ever.
// Reading what happened: `calls` (every [file, ...args]), `dispatches` ({ workflow, ref, inputs } of each POST), `clock`.
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { join } from 'node:path';

export const repositoryName = 'example/tasks';
export const allTestsWorkflowPath = '.github/workflows/all-tests.yml';
export const releaseWorkflowPath = '.github/workflows/release.yml';
export const runUrl = (id) => `https://github.com/${repositoryName}/actions/runs/${id}`;
export const jobUrl = (id) => `https://github.com/${repositoryName}/actions/runs/job/${id}`;

export const restRun = (overrides = {}) => ({
  id: 30,
  path: allTestsWorkflowPath,
  event: 'workflow_dispatch',
  head_sha: 'a1'.repeat(20),
  head_branch: 'main',
  status: 'completed',
  conclusion: 'success',
  run_attempt: 1,
  html_url: runUrl(overrides.id ?? 30),
  ...overrides,
});

export const restJob = (name, conclusion, runAttempt = 1, id = 900) => ({
  id, name, status: conclusion ? 'completed' : 'in_progress', conclusion, run_attempt: runAttempt, html_url: jobUrl(id),
});

export function createFakeGitHub({
  repository = repositoryName, origin = `https://github.com/${repository}.git\n`, pushOrigin, authError, apiError,
  runs = [], jobs = {}, dispatch, release, installer = 'installer', afterReads = {},
} = {}) {
  const calls = [];
  const dispatches = [];
  const reads = {};
  let time = 0;
  const clock = { now: () => time, wait: async (ms) => { time += ms; } };

  const valuesOf = (args) => Object.fromEntries(args.flatMap((arg, i) => (['-f', '-F'].includes(args[i - 1]) ? [arg.split(/=(.*)/s).slice(0, 2)] : [])));

  const execute = async (file, args) => {
    calls.push([file, ...args]);
    if (file === 'git') {
      assert.deepEqual(args.slice(0, 2), ['remote', 'get-url'], `Unexpected git command ${args.join(' ')}`);
      return args.includes('--push') ? (pushOrigin ?? origin) : origin;
    }
    assert.equal(file, 'gh', `Unexpected program ${file}`);
    if (args[0] === 'auth') {
      if (authError) throw new Error(authError);
      return '';
    }
    if (args[0] === 'release' && args[1] === 'view') {
      if (!release) throw new Error('gh release view failed: release not found');
      return JSON.stringify(release);
    }
    if (args[0] === 'release' && args[1] === 'download') {
      await writeFile(join(args[args.indexOf('--dir') + 1], args[args.indexOf('--pattern') + 1]), installer);
      return '';
    }
    assert.equal(args[0], 'api', `Unexpected gh command ${args.join(' ')}`);
    if (apiError) throw new Error(apiError);
    const endpoint = args.find((arg, index) => index > 0 && /^repos\//.test(arg)) ?? '';
    assert.ok(endpoint.startsWith(`repos/${repository}/`), `Unexpected repository in ${args.join(' ')}`);

    const dispatchOf = /\/actions\/workflows\/([^/]+)\/dispatches$/.exec(endpoint);
    if (args.includes('POST')) {
      assert.ok(dispatchOf, `Unexpected POST ${args.join(' ')}`);
      const { ref, return_run_details: details, ...rest } = valuesOf(args);
      assert.equal(details, 'true', 'a dispatch must ask for the run details');
      dispatches.push({
        workflow: dispatchOf[1],
        ref,
        inputs: Object.fromEntries(Object.entries(rest).map(([key, value]) => [/^inputs\[(.+)\]$/.exec(key)?.[1] ?? assert.fail(`Unexpected field ${key}`), value])),
      });
      return JSON.stringify(dispatch ?? { workflow_run_id: runs[0]?.id });
    }
    const listOf = /\/actions\/workflows\/([^/?]+)\/runs/.exec(endpoint);
    if (listOf) return JSON.stringify({ workflow_runs: listOf[1] === 'all-tests.yml' ? runs : [] });
    const jobsOf = /\/actions\/runs\/(\d+)\/jobs/.exec(endpoint);
    if (jobsOf) {
      const all = jobs[jobsOf[1]];
      assert.ok(all, `No jobs were made up for run ${jobsOf[1]}`);
      const newest = Math.max(...all.map((job) => job.run_attempt));
      return JSON.stringify({ jobs: /filter=latest/.test(endpoint) ? all.filter((job) => job.run_attempt === newest) : all });
    }
    const one = /\/actions\/runs\/(\d+)(\?|$)/.exec(endpoint);
    if (one) {
      const run = runs.find((candidate) => String(candidate.id) === one[1]) ?? assert.fail(`No run ${one[1]} was made up`);
      const steps = afterReads[one[1]] ?? [];
      const count = reads[one[1]] = (reads[one[1]] ?? 0) + 1;
      return JSON.stringify({ ...run, ...(steps[Math.min(count, steps.length) - 1] ?? {}) });
    }
    return assert.fail(`Unexpected gh api call ${args.join(' ')}`);
  };
  return { execute, calls, dispatches, clock };
}
