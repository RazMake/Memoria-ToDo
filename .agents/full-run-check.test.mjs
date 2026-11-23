import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildWorkflowPath, createFakeActionsApi, repositoryName, restJob, restRun } from './full-run-check.fake-github.mjs';
import { checkFullRun, createSource, run } from './full-run-check.mjs';
import { commitA, commitB } from './version-tag.fake-git.mjs';

// full-run-check.mjs decides which workflow run proves a commit passed every test. Its interface, as these tests define it:
//   checkFullRun(commit, source) -> { state: 'passed' | 'running' | 'failed' | 'none', run? }
//   source = { runs(commit) -> run[], jobs(databaseId) -> job[] }, where a run is
//     { databaseId, path, event, headSha, status, conclusion, url } and a job is { name, conclusion };
//     `jobs` gives the jobs of the run's newest attempt only.
//   createSource(execute, repository) -> source, reading GitHub through `gh api` (execute(file, args) -> stdout).
//   run(args, source) -> { code, stdout, stderr }, the command line:
//     --repo <owner/name> --commit <40 hex> --ref <tag or branch>
const url = (id) => `https://github.com/${repositoryName}/actions/runs/${id}`;
const manual = (overrides = {}) => ({
  databaseId: 30, path: buildWorkflowPath, event: 'workflow_dispatch', headSha: commitA,
  status: 'completed', conclusion: 'success', url: url(overrides.databaseId ?? 30), ...overrides,
});
const passedJobs = [{ name: 'unit-tests', conclusion: 'success' }, { name: 'full-tests', conclusion: 'success' }];

function sourceOf(runs, jobs = {}) {
  const asked = [];
  return {
    asked,
    runs: async () => runs,
    jobs: async (id) => { asked.push(id); return jobs[id] ?? passedJobs; },
  };
}

describe('the run that proves a commit passed every test (unit)', () => {
  test('accepts a finished manual run of the commit whose full-tests job passed', async () => {
    const result = await checkFullRun(commitA, sourceOf([manual()]));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 30);
  });

  test('says none when there is no run at all', async () => {
    assert.deepEqual(await checkFullRun(commitA, sourceOf([])), { state: 'none' });
  });

  for (const [label, changes] of [
    ['a run of another commit', { headSha: commitB }],
    ['a run started by a push', { event: 'push' }],
    ['a scheduled run', { event: 'schedule' }],
    ['a run of another workflow file', { path: '.github/workflows/other.yml' }],
  ]) {
    test(`does not count ${label}, even when it passed`, async () => {
      assert.deepEqual(await checkFullRun(commitA, sourceOf([manual(changes)])), { state: 'none' });
    });
  }

  for (const [status, conclusion] of [['queued', null], ['in_progress', null], ['waiting', null], ['requested', null], ['pending', null], ['in_progress', 'success']]) {
    test(`reports a run that is ${status} with conclusion ${conclusion} as running, not passed`, async () => {
      const result = await checkFullRun(commitA, sourceOf([manual({ status, conclusion })]));
      assert.equal(result.state, 'running');
      assert.equal(result.run.databaseId, 30);
    });
  }

  for (const conclusion of ['failure', 'cancelled', 'timed_out', 'skipped', 'neutral', 'action_required', null]) {
    test(`reports a finished run with conclusion ${conclusion} as failed`, async () => {
      const result = await checkFullRun(commitA, sourceOf([manual({ conclusion })]));
      assert.equal(result.state, 'failed');
      assert.equal(result.run.url, url(30));
    });
  }

  for (const [label, jobs] of [
    ['has no full-tests job', [{ name: 'unit-tests', conclusion: 'success' }]],
    ['has a failed full-tests job', [{ name: 'full-tests', conclusion: 'failure' }]],
    ['has a skipped full-tests job', [{ name: 'full-tests', conclusion: 'skipped' }]],
    ['has an unfinished full-tests job', [{ name: 'full-tests', conclusion: null }]],
    ['has no jobs', []],
  ]) {
    test(`does not accept a successful run that ${label}`, async () => {
      const result = await checkFullRun(commitA, sourceOf([manual()], { 30: jobs }));
      assert.equal(result.state, 'failed');
      assert.equal(result.run.databaseId, 30);
    });
  }

  test('accepts a successful run whatever other jobs it has, as long as full-tests passed', async () => {
    const jobs = [{ name: 'full-tests', conclusion: 'success' }, { name: 'lint', conclusion: 'success' }];
    assert.equal((await checkFullRun(commitA, sourceOf([manual()], { 30: jobs }))).state, 'passed');
  });

  test('judges the newest matching run when several exist, whatever their order', async () => {
    const older = manual({ databaseId: 20, conclusion: 'failure' });
    const newer = manual({ databaseId: 40 });
    for (const runs of [[older, newer], [newer, older]]) {
      const result = await checkFullRun(commitA, sourceOf(runs));
      assert.equal(result.state, 'passed');
      assert.equal(result.run.databaseId, 40);
    }
  });

  test('lets a newer failed run override an older passed one, since the newest manual run decides', async () => {
    const result = await checkFullRun(commitA, sourceOf([manual({ databaseId: 20 }), manual({ databaseId: 40, conclusion: 'failure' })]));
    assert.equal(result.state, 'failed');
    assert.equal(result.run.databaseId, 40);
  });

  test('lets a newer running run override an older passed one', async () => {
    const result = await checkFullRun(commitA, sourceOf([manual({ databaseId: 20 }), manual({ databaseId: 40, status: 'queued', conclusion: null })]));
    assert.equal(result.state, 'running');
    assert.equal(result.run.databaseId, 40);
  });

  test('ignores newer runs that do not count when choosing the newest', async () => {
    const runs = [manual({ databaseId: 20 }), manual({ databaseId: 50, event: 'push', conclusion: 'failure' }),
      manual({ databaseId: 60, headSha: commitB, conclusion: 'failure' }), manual({ databaseId: 70, path: '.github/workflows/other.yml', conclusion: 'failure' })];
    const result = await checkFullRun(commitA, sourceOf(runs));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 20);
  });

  test('looks at the jobs of the chosen run only', async () => {
    const source = sourceOf([manual({ databaseId: 20 }), manual({ databaseId: 40 })]);
    await checkFullRun(commitA, source);
    assert.deepEqual(source.asked, [40]);
  });

  test('asks the source about the given commit', async () => {
    const asked = [];
    await checkFullRun(commitA, { runs: async (commit) => { asked.push(commit); return []; }, jobs: async () => [] });
    assert.deepEqual(asked, [commitA]);
  });

  test('lets a source failure through instead of reporting a state', async () => {
    const source = { runs: async () => { throw new Error('GitHub is down'); }, jobs: async () => [] };
    await assert.rejects(() => checkFullRun(commitA, source), /GitHub is down/);
  });
});

describe('the check reads GitHub through gh api (unit)', () => {
  const repoRuns = (...runs) => createSource(createFakeActionsApi({ runs, jobs: { 30: [restJob('full-tests', 'success')] } }).execute, repositoryName);

  test('turns a passing run of the commit into a passed result with its link', async () => {
    const result = await checkFullRun(commitA, repoRuns(restRun()));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 30);
    assert.equal(result.run.url, url(30));
    assert.equal(result.run.headSha, commitA);
  });

  test('does not count a push run or a run of another commit that GitHub returned', async () => {
    assert.equal((await checkFullRun(commitA, repoRuns(restRun({ event: 'push' }), restRun({ id: 31, head_sha: commitB })))).state, 'none');
  });

  test('judges the jobs of the newest attempt, so a rerun that passed counts', async () => {
    const api = createFakeActionsApi({ runs: [restRun({ run_attempt: 2 })], jobs: { 30: [restJob('full-tests', 'failure', 1), restJob('full-tests', 'success', 2)] } });
    assert.equal((await checkFullRun(commitA, createSource(api.execute, repositoryName))).state, 'passed');
  });

  test('judges the jobs of the newest attempt, so a rerun that failed does not count', async () => {
    const api = createFakeActionsApi({ runs: [restRun({ run_attempt: 2 })], jobs: { 30: [restJob('full-tests', 'success', 1), restJob('full-tests', 'failure', 2)] } });
    assert.equal((await checkFullRun(commitA, createSource(api.execute, repositoryName))).state, 'failed');
  });

  test('asks only about the named repository and the commit', async () => {
    const api = createFakeActionsApi({ runs: [restRun()], jobs: { 30: [restJob('full-tests', 'success')] } });
    await checkFullRun(commitA, createSource(api.execute, repositoryName));
    assert.ok(api.calls.length >= 2);
    for (const call of api.calls) assert.ok(call.some((arg) => arg.startsWith(`repos/${repositoryName}/`)), call.join(' '));
    assert.ok(api.calls[0].some((arg) => arg.includes(commitA)), 'the run list must be limited to the commit');
  });

  test('rejects a response that is not a list of runs', async () => {
    const source = createSource(async () => '{"wrong":"shape"}', repositoryName);
    await assert.rejects(() => source.runs(commitA), /response|runs/i);
  });

  test('lets a gh failure through', async () => {
    const source = createSource(async () => { throw new Error('gh api failed: HTTP 403'); }, repositoryName);
    await assert.rejects(() => source.runs(commitA), /HTTP 403/);
  });
});

describe('the check command line (unit)', () => {
  const args = ['--repo', repositoryName, '--commit', commitA, '--ref', 'v1.0.0'];
  const oneLine = (text) => assert.match(text, /^[^\r\n]+\r?\n?$/, `expected exactly one line, got: ${JSON.stringify(text)}`);

  test('exits zero and prints one line with the commit and the passing run link', async () => {
    const result = await run(args, sourceOf([manual()]));
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stderr, '');
    oneLine(result.stdout);
    assert.ok(result.stdout.includes(url(30)));
    assert.ok(result.stdout.includes(commitA));
  });

  test('fails with the commit and the fix when no manual run of the commit exists', async () => {
    const result = await run(args, sourceOf([]));
    assert.equal(result.code, 1);
    assert.equal(result.stdout, '');
    oneLine(result.stderr);
    assert.ok(result.stderr.includes(commitA));
    assert.ok(result.stderr.includes('gh workflow run build.yml --ref v1.0.0'));
  });

  test('names the fix for the ref it was given, such as a branch', async () => {
    const result = await run(['--repo', repositoryName, '--commit', commitA, '--ref', 'main'], sourceOf([]));
    assert.ok(result.stderr.includes('gh workflow run build.yml --ref main'));
  });

  test('fails with the commit and the run link while the manual run is still running', async () => {
    const result = await run(args, sourceOf([manual({ status: 'in_progress', conclusion: null })]));
    assert.equal(result.code, 1);
    oneLine(result.stderr);
    assert.ok(result.stderr.includes(commitA));
    assert.ok(result.stderr.includes(url(30)));
  });

  test('fails with the commit and the run link when the manual run failed', async () => {
    const result = await run(args, sourceOf([manual({ conclusion: 'failure' })]));
    assert.equal(result.code, 1);
    oneLine(result.stderr);
    assert.ok(result.stderr.includes(commitA));
    assert.ok(result.stderr.includes(url(30)));
  });

  test('fails with the commit when a run passed but its full-tests job did not', async () => {
    const result = await run(args, sourceOf([manual()], { 30: [{ name: 'full-tests', conclusion: 'skipped' }] }));
    assert.equal(result.code, 1);
    oneLine(result.stderr);
    assert.ok(result.stderr.includes(commitA));
  });

  test('reports a source failure as one line and exit code one', async () => {
    const result = await run(args, { runs: async () => { throw new Error('GitHub is down\nsecond line'); }, jobs: async () => [] });
    assert.equal(result.code, 1);
    oneLine(result.stderr);
    assert.match(result.stderr, /GitHub is down/);
  });

  for (const [label, bad] of [
    ['no arguments', []],
    ['a missing ref', ['--repo', repositoryName, '--commit', commitA]],
    ['a missing commit', ['--repo', repositoryName, '--ref', 'v1.0.0']],
    ['a missing repository', ['--commit', commitA, '--ref', 'v1.0.0']],
    ['a commit that is not 40 hex digits', ['--repo', repositoryName, '--commit', 'main', '--ref', 'v1.0.0']],
    ['a repository that is not owner/name', ['--repo', 'tasks', '--commit', commitA, '--ref', 'v1.0.0']],
    ['an unknown option', [...args, '--force']],
  ]) {
    test(`rejects ${label} with a one line usage error before asking GitHub`, async () => {
      const source = { runs: async () => assert.fail('must not ask GitHub'), jobs: async () => assert.fail('must not ask GitHub') };
      const result = await run(bad, source);
      assert.equal(result.code, 1);
      assert.equal(result.stdout, '');
      oneLine(result.stderr);
      assert.match(result.stderr, /usage/i);
    });
  }
});

describe('the check command process (integration)', () => {
  const script = join(dirname(fileURLToPath(import.meta.url)), 'full-run-check.mjs');
  const withoutPath = Object.fromEntries(Object.entries(process.env).filter(([name]) => name.toLowerCase() !== 'path'));
  const start = (args) => spawnSync(process.execPath, [script, ...args], { encoding: 'utf8', timeout: 10_000, env: withoutPath });

  test('exits one with a one line usage error when started without arguments', () => {
    const result = start([]);
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /^[^\r\n]*usage[^\r\n]*\r?\n?$/i);
  });

  test('exits one with a one line error when gh cannot be started, without a stack trace', () => {
    const result = start(['--repo', repositoryName, '--commit', commitA, '--ref', 'v1.0.0']);
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /^[^\r\n]+\r?\n?$/);
    assert.doesNotMatch(result.stderr, /\n\s+at /);
  });
});
