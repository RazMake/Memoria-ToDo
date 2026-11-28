import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { createGitHubRuns } from './github-runs.mjs';
import { allTestsWorkflowPath, createFakeGitHub, releaseWorkflowPath, repositoryName, restJob, restRun, runUrl } from './github-runs.fake-github.mjs';
import { repositoryWithTooling, runCommand } from './github-runs.fake-github.process.mjs';
import { checkFullRun, createSource, run } from './full-run-check.mjs';
import { commitA, commitB } from './version-tag.fake-git.mjs';

// full-run-check.mjs checks the selected platform's test run for an exact commit. Its interface:
//   checkFullRun(commit, source) -> { state: 'passed' | 'running' | 'failed' | 'none', run? }
//   source = { runs(commit) -> run[], jobs(databaseId) -> job[] }, where a run is
//     { databaseId, path, event, headSha, status, conclusion, url } and a job is { name, conclusion };
//     `jobs` gives the jobs of the run's newest attempt only.
//   createSource(github) -> source, reading all-tests.yml's runs of the commit and jobs through the github-runs.mjs component.
//   run(args, source) -> { code, stdout, stderr }, the command line:
//     --repo <owner/name> --commit <40 hex>
//     A pass prints the run link on stdout and exits 0; no pass prints nothing and exits 0; wrong arguments or a GitHub error
//     print one stderr line and exit 1.
const manual = (overrides = {}) => ({
  databaseId: 30, path: allTestsWorkflowPath, event: 'workflow_dispatch', headSha: commitA,
  status: 'completed', conclusion: 'success', url: runUrl(overrides.databaseId ?? 30), ...overrides,
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

describe('independent Android test gates (unit)', () => {
  const androidRun = (changes = {}) => manual({ path: '.github/workflows/android-tests.yml', ...changes });
  const androidJobs = [
    { name: 'android-pc-tests', conclusion: 'success' },
    { name: 'android-device-tests', conclusion: 'success' },
  ];
  const args = ['--repo', repositoryName, '--commit', commitA, '--platform', 'android'];

  test('reuses only a successful Android run with both required jobs', async () => {
    const result = await run(args, sourceOf([androidRun(), manual({ databaseId: 99, conclusion: 'failure' })], { 30: androidJobs }));
    assert.deepEqual(result, { code: 0, stdout: `${runUrl(30)}\n`, stderr: '' });
  });

  for (const scenario of ['missing', 'failed', 'skipped', 'unfinished']) {
    test(`does not reuse an Android run whose native gate is ${scenario}`, async () => {
      const jobs = scenario === 'missing' ? androidJobs.slice(0, 1)
        : [androidJobs[0], { ...androidJobs[1], conclusion: { failed: 'failure', skipped: 'skipped', unfinished: null }[scenario] }];
      const result = await run(args, sourceOf([androidRun()], { 30: jobs }));
      assert.deepEqual(result, { code: 0, stdout: '', stderr: '' });
    });
  }

  test('ignores a successful desktop run and release-called Android run', async () => {
    const result = await run(args, sourceOf([manual(), androidRun({ databaseId: 40, event: 'workflow_call' })]));
    assert.deepEqual(result, { code: 0, stdout: '', stderr: '' });
  });

  test('does not let an older Android pass hide the newest failed attempt', async () => {
    const result = await run(args, sourceOf([androidRun(), androidRun({ databaseId: 40, conclusion: 'failure' })], { 30: androidJobs }));
    assert.deepEqual(result, { code: 0, stdout: '', stderr: '' });
  });
  for (const conclusion of ['failure', 'skipped']) {
    test(`does not reuse a required Android job with both successful and ${conclusion} results`, async () => {
      const jobs = [...androidJobs, { ...androidJobs[1], conclusion }];
      const result = await run(args, sourceOf([androidRun()], { 30: jobs }));
      assert.deepEqual(result, { code: 0, stdout: '', stderr: '' });
    });
  }
});

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
    ['a run started by a pull request', { event: 'pull_request' }],
    ['a run started by another workflow', { event: 'workflow_call' }],
    ['a scheduled run', { event: 'schedule' }],
    ['a run of another workflow file', { path: '.github/workflows/other.yml' }],
    ['the Build and unit tests workflow', { path: '.github/workflows/build.yml' }],
    ['a release run, whose own Tests job is not a Run All tests run', { path: releaseWorkflowPath }],
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
      assert.equal(result.run.url, runUrl(30));
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
    const runs = [manual({ databaseId: 20 }), manual({ databaseId: 50, event: 'pull_request', conclusion: 'failure' }),
      manual({ databaseId: 60, headSha: commitB, conclusion: 'failure' }), manual({ databaseId: 70, path: releaseWorkflowPath, conclusion: 'failure' })];
    const result = await checkFullRun(commitA, sourceOf(runs));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 20);
  });

  for (const [label, runs, state, id] of [
    ['a passed push run', [manual({ databaseId: 30, event: 'push' })], 'passed', 30],
    ['a newer passed push run over an older failed manual run', [manual({ databaseId: 20, conclusion: 'failure' }), manual({ databaseId: 40, event: 'push' })], 'passed', 40],
    ['a newer failed push run over an older passed manual run', [manual({ databaseId: 20 }), manual({ databaseId: 40, event: 'push', conclusion: 'failure' })], 'failed', 40],
    ['a newer running push run over an older passed manual run', [manual({ databaseId: 20 }), manual({ databaseId: 40, event: 'push', status: 'in_progress', conclusion: null })], 'running', 40],
    ['a newer passed manual run over an older failed push run', [manual({ databaseId: 20, event: 'push', conclusion: 'failure' }), manual({ databaseId: 40 })], 'passed', 40],
    ['a newer failed manual run over an older passed push run', [manual({ databaseId: 20, event: 'push' }), manual({ databaseId: 40, conclusion: 'failure' })], 'failed', 40],
    ['a newer running manual run over an older passed push run', [manual({ databaseId: 20, event: 'push' }), manual({ databaseId: 40, status: 'queued', conclusion: null })], 'running', 40],
    ['an older passed push run when a newer pull request run failed', [manual({ databaseId: 20, event: 'push' }), manual({ databaseId: 40, event: 'pull_request', conclusion: 'failure' })], 'passed', 20],
  ]) {
    test(`judges ${label} by the highest run id across push and manual runs`, async () => {
      const result = await checkFullRun(commitA, sourceOf(runs));
      assert.equal(result.state, state);
      assert.equal(result.run.databaseId, id);
    });
  }

  test('does not accept a successful push run whose full-tests job failed', async () => {
    const result = await checkFullRun(commitA, sourceOf([manual({ event: 'push' })], { 30: [{ name: 'full-tests', conclusion: 'failure' }] }));
    assert.equal(result.state, 'failed');
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

describe('the check reads all-tests.yml through the GitHub component (unit)', () => {
  const sourceFrom = (fake) => createSource(createGitHubRuns(fake.execute, { repository: repositoryName, clock: fake.clock }));
  const fakeWith = (runs, jobs = { 30: [restJob('full-tests', 'success')] }) => createFakeGitHub({ runs, jobs });

  test('turns a passing run of the commit into a passed result with its link', async () => {
    const result = await checkFullRun(commitA, sourceFrom(fakeWith([restRun()])));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 30);
    assert.equal(result.run.url, runUrl(30));
    assert.equal(result.run.headSha, commitA);
  });

  test('lists the runs of all-tests.yml, not build.yml, for the commit with no event filter, so push runs come back', async () => {
    const fake = fakeWith([restRun()]);
    await checkFullRun(commitA, sourceFrom(fake));
    const listing = fake.calls.find((call) => call.some((arg) => /\/workflows\/[^/]+\/runs/.test(arg)));
    assert.ok(listing, fake.calls.map((call) => call.join(' ')).join('\n'));
    const text = listing.join(' ');
    assert.ok(text.includes('/workflows/all-tests.yml/runs'), text);
    assert.ok(!text.includes('build.yml'), text);
    assert.ok(text.includes(`head_sha=${commitA}`), text);
    assert.ok(!text.includes('event='), text);
  });

  test('counts a push run of the commit that GitHub returned', async () => {
    const result = await checkFullRun(commitA, sourceFrom(fakeWith([restRun({ event: 'push' })])));
    assert.equal(result.state, 'passed');
    assert.equal(result.run.databaseId, 30);
  });

  test('does not count a pull request or scheduled run, or a run of another commit, that GitHub returned', async () => {
    const fake = fakeWith([restRun({ event: 'pull_request' }), restRun({ id: 32, event: 'schedule' }), restRun({ id: 31, head_sha: commitB, event: 'push' })]);
    assert.equal((await checkFullRun(commitA, sourceFrom(fake))).state, 'none');
  });

  test('counts a run on any branch or tag', async () => {
    for (const head_branch of ['main', 'feature/x', 'v1.0.0']) {
      assert.equal((await checkFullRun(commitA, sourceFrom(fakeWith([restRun({ head_branch })])))).state, 'passed', head_branch);
    }
  });

  test('judges the jobs of the newest attempt, so a rerun that passed counts', async () => {
    const fake = fakeWith([restRun({ run_attempt: 2 })], { 30: [restJob('full-tests', 'failure', 1), restJob('full-tests', 'success', 2)] });
    assert.equal((await checkFullRun(commitA, sourceFrom(fake))).state, 'passed');
  });

  test('judges the jobs of the newest attempt, so a rerun that failed does not count', async () => {
    const fake = fakeWith([restRun({ run_attempt: 2 })], { 30: [restJob('full-tests', 'success', 1), restJob('full-tests', 'failure', 2)] });
    assert.equal((await checkFullRun(commitA, sourceFrom(fake))).state, 'failed');
  });

  test('reads the jobs with filter=latest', async () => {
    const fake = fakeWith([restRun()]);
    await checkFullRun(commitA, sourceFrom(fake));
    assert.ok(fake.calls.some((call) => call.some((arg) => /\/runs\/30\/jobs/.test(arg) && arg.includes('filter=latest'))));
  });

  test('lets a gh failure through', async () => {
    const source = createSource(createGitHubRuns(async () => { throw new Error('gh api failed: HTTP 403'); }, { repository: repositoryName }));
    await assert.rejects(() => source.runs(commitA), /HTTP 403/);
  });
});

describe('the check command line (unit)', () => {
  const args = ['--repo', repositoryName, '--commit', commitA];
  const oneLine = (text) => assert.match(text, /^[^\r\n]+\r?\n?$/, `expected exactly one line, got: ${JSON.stringify(text)}`);

  test('exits zero and prints the passing run link on one line', async () => {
    const result = await run(args, sourceOf([manual()]));
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stderr, '');
    oneLine(result.stdout);
    assert.ok(result.stdout.includes(runUrl(30)));
  });

  for (const [label, source] of [
    ['there is no run of the commit', sourceOf([])],
    ['the run is still running', sourceOf([manual({ status: 'in_progress', conclusion: null })])],
    ['the run failed', sourceOf([manual({ conclusion: 'failure' })])],
    ['a run passed but its full-tests job did not', sourceOf([manual()], { 30: [{ name: 'full-tests', conclusion: 'skipped' }] })],
  ]) {
    test(`prints nothing and exits zero when ${label}`, async () => {
      const result = await run(args, source);
      assert.equal(result.code, 0);
      assert.equal(result.stdout, '');
      assert.equal(result.stderr, '');
    });
  }

  test('reports a source failure as one line and exit code one', async () => {
    const result = await run(args, { runs: async () => { throw new Error('GitHub is down\nsecond line'); }, jobs: async () => [] });
    assert.equal(result.code, 1);
    assert.equal(result.stdout, '');
    oneLine(result.stderr);
    assert.match(result.stderr, /GitHub is down/);
  });

  for (const [label, bad] of [
    ['no arguments', []],
    ['a missing commit', ['--repo', repositoryName]],
    ['a missing repository', ['--commit', commitA]],
    ['the removed --ref option', [...args, '--ref', 'v1.0.0']],
    ['the removed --ref option alone in place of the commit', ['--repo', repositoryName, '--ref', 'v1.0.0']],
    ['a commit that is not 40 hex digits', ['--repo', repositoryName, '--commit', 'main']],
    ['a repository that is not owner/name', ['--repo', 'tasks', '--commit', commitA]],
    ['an unknown option', [...args, '--force']],
  ]) {
    test(`rejects ${label} with a one line usage error before asking GitHub`, async () => {
      const source = { runs: async () => assert.fail('must not ask GitHub'), jobs: async () => assert.fail('must not ask GitHub') };
      const result = await run(bad, source);
      assert.equal(result.code, 1);
      assert.equal(result.stdout, '');
      oneLine(result.stderr);
      assert.match(result.stderr, /usage/i);
      assert.doesNotMatch(result.stderr, /--ref/);
    });
  }
});

describe('the check command as a child process (integration)', () => {
  const start = (t, args, fake) => {
    const repo = repositoryWithTooling(t);
    return runCommand(repo.work, repo.script('full-run-check.mjs'), args, fake);
  };
  const args = ['--repo', repositoryName, '--commit', commitA];

  test('exits 0 and prints the link of a passed Run All tests run of the commit', { timeout: 60_000 }, async (t) => {
    const result = await start(t, args, { runs: [restRun()], jobs: { 30: [restJob('full-tests', 'success')] } });
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stdout.trim(), runUrl(30));
  });

  test('exits 0 and prints the link of a passed push run of the commit', { timeout: 60_000 }, async (t) => {
    const result = await start(t, args, { runs: [restRun({ event: 'push' })], jobs: { 30: [restJob('full-tests', 'success')] } });
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stdout.trim(), runUrl(30));
  });

  test('exits 0 and prints nothing when the commit has no passed run', { timeout: 60_000 }, async (t) => {
    const result = await start(t, args, { runs: [restRun({ head_sha: commitB })] });
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stdout, '');
  });

  test('exits one with a one line usage error when started without arguments', { timeout: 60_000 }, async (t) => {
    const result = await start(t, [], {});
    assert.equal(result.code, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /^[^\r\n]*usage[^\r\n]*\r?\n?$/i);
  });

  test('exits one with a one line error when GitHub answers with an error, without a stack trace', { timeout: 60_000 }, async (t) => {
    const result = await start(t, args, { apiError: 'HTTP 403' });
    assert.equal(result.code, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /^[^\r\n]+\r?\n?$/);
    assert.doesNotMatch(result.stderr, /\n\s+at /);
  });
});
