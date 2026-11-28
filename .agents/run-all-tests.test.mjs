import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { createGitHubRuns } from './github-runs.mjs';
import { allTestsWorkflowPath, createFakeGitHub, restJob, restRun, runUrl } from './github-runs.fake-github.mjs';
import { repositoryWithTooling, runCommand } from './github-runs.fake-github.process.mjs';
import { run } from './run-all-tests.mjs';
import { commitA, commitB, createFakeGit } from './version-tag.fake-git.mjs';

// Run All tests as releasing.md describes it: the real GitHub component over the one fake GitHub, the fake git of the version tag script,
// and a plain stand-in for the terminal. `runOptions` change the run the fake GitHub reports.
function session({ git: gitOptions, run: runOptions, jobs, fake } = {}) {
  const git = createFakeGit({ branch: 'feature', originHead: commitA, ...gitOptions });
  const github = createFakeGitHub({
    runs: [restRun({ id: 60, path: allTestsWorkflowPath, head_sha: commitA, head_branch: 'feature', ...runOptions })],
    jobs: { 60: jobs ?? [restJob('full-tests', 'success')] },
    dispatch: { workflow_run_id: 60 },
    ...fake,
  });
  const output = [];
  const services = {
    git,
    github: createGitHubRuns(github.execute, { clock: github.clock }),
    write: (text) => output.push(text),
    waitForGitHub: (text) => output.push(text),
  };
  return { git, github, services, text: () => output.join('') };
}

describe('run all tests command: its command line (unit)', () => {
  test('shows its usage and starts nothing for --help', async () => {
    const s = session();
    const result = await run(['--help'], s.services);
    assert.equal(result.code, 0);
    assert.match(s.text(), /run-all-tests/);
    assert.deepEqual(s.github.dispatches, []);
  });

  for (const args of [['--part', 'minor'], ['now'], ['--ref', 'main'], ['--help', 'now']]) {
    test(`rejects the argument ${args.join(' ')} and starts nothing`, async () => {
      const s = session();
      const result = await run(args, s.services);
      assert.equal(result.code, 1);
      assert.ok(result.stderr);
      assert.deepEqual(s.github.dispatches, []);
    });
  }

  test('asks nothing, because it has no ask service', async () => {
    const s = session();
    assert.equal(s.services.ask, undefined);
    assert.equal((await run([], s.services)).code, 0);
  });
});

describe('run all tests command: checking your branch (unit)', () => {
  test('refuses on a detached HEAD and starts nothing', async () => {
    const s = session({ git: { branch: undefined } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /detached/i);
    assert.deepEqual(s.github.dispatches, []);
  });

  test('refuses when your branch has a commit origin does not have, and starts nothing', async () => {
    const s = session({ git: { ahead: 1 } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /push/i);
    assert.deepEqual(s.github.dispatches, []);
  });

  test('refuses when origin has a commit your branch does not have, and starts nothing', async () => {
    const s = session({ git: { behind: 2 } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /pull|behind/i);
    assert.deepEqual(s.github.dispatches, []);
  });

  test('fetches the branch from origin before it compares', async () => {
    const s = session();
    await run([], s.services);
    assert.deepEqual(s.git.fetchedBranches, ['feature']);
  });

  test('warns about uncommitted changes but still starts the run', async () => {
    const s = session({ git: { clean: false } });
    const result = await run([], s.services);
    assert.equal(result.code, 0);
    assert.match(s.text(), /uncommitted/i);
    assert.equal(s.github.dispatches.length, 1);
  });

  test('does not warn when the working tree is clean', async () => {
    const s = session();
    await run([], s.services);
    assert.doesNotMatch(s.text(), /uncommitted/i);
  });
});

describe('run all tests command: starting and following the run (unit)', () => {
  test('starts all-tests.yml on your branch with no inputs', async () => {
    const s = session();
    await run([], s.services);
    assert.deepEqual(s.github.dispatches, [{ workflow: 'all-tests.yml', ref: 'feature', inputs: {} }]);
  });

  test('checks the sign-in and origin before it starts the run', async () => {
    const s = session();
    await run([], s.services);
    const calls = s.github.calls.map((call) => call.join(' '));
    assert.ok(calls.indexOf('gh auth status --hostname github.com') >= 0);
    assert.ok(calls.indexOf('gh auth status --hostname github.com') < calls.findIndex((call) => call.includes('/dispatches')));
  });

  test('prints the run link and exits 0 when the run succeeds', async () => {
    const s = session();
    const result = await run([], s.services);
    assert.equal(result.code, 0);
    assert.ok(s.text().includes(runUrl(60)), s.text());
  });

  test('prints the run link and exits nonzero when the run fails', async () => {
    const s = session({ run: { conclusion: 'failure' } });
    const result = await run([], s.services);
    assert.notEqual(result.code, 0);
    assert.ok((s.text() + result.stderr).includes(runUrl(60)));
  });

  test('exits nonzero when the run is cancelled', async () => {
    const s = session({ run: { conclusion: 'cancelled' } });
    assert.notEqual((await run([], s.services)).code, 0);
  });

  test('says it is waiting on GitHub while it follows a run that is not finished yet', async () => {
    const s = session({ run: { status: 'in_progress', conclusion: null }, fake: { afterReads: { 60: [{ status: 'completed', conclusion: 'success' }] } } });
    const result = await run([], s.services);
    assert.equal(result.code, 0);
    assert.ok(s.text().length > runUrl(60).length);
  });

  test('stops with the run link when the run is on another commit than your HEAD', async () => {
    const s = session({ run: { head_sha: commitB } });
    const result = await run([], s.services);
    assert.notEqual(result.code, 0);
    assert.ok(result.stderr.includes(runUrl(60)));
    assert.ok(result.stderr.includes(commitA) && result.stderr.includes(commitB));
    assert.equal(s.github.dispatches.length, 1, 'starts no second run');
  });

  test('stops with the run link when origin moves to the commit the run started on after the readiness check', async () => {
    const s = session({ run: { head_sha: commitB } });
    const dispatch = s.services.github.dispatch;
    s.services.github.dispatch = async (...args) => {
      s.git.configure({ originHead: commitB });
      return dispatch(...args);
    };
    const result = await run([], s.services);
    assert.notEqual(result.code, 0, 'a run of a commit you did not check must not pass');
    assert.ok((s.text() + result.stderr).includes(runUrl(60)));
    assert.ok(result.stderr.includes(commitA) && result.stderr.includes(commitB), result.stderr);
    assert.equal(s.github.dispatches.length, 1, 'starts no second run');
  });

  test('prints the link of the run it started when Ctrl+C stops it', async () => {
    const s = session();
    s.services.github.follow = async () => { throw Object.assign(new Error('The operation was aborted'), { name: 'AbortError' }); };
    const result = await run([], s.services);
    assert.notEqual(result.code, 0);
    assert.ok(result.stderr.includes(runUrl(60)), result.stderr);
  });

  test('stops with the run link when GitHub does not finish the run in time', async () => {
    const s = session({ run: { status: 'in_progress', conclusion: null } });
    const result = await run([], s.services);
    assert.notEqual(result.code, 0);
    assert.ok(result.stderr.includes(runUrl(60)));
  });
});

describe('run all tests command as a child process (integration)', () => {
  const start = (repo, fake) => runCommand(repo.work, repo.script('run-all-tests.mjs'), [], {
    runs: [restRun({ id: 60, path: allTestsWorkflowPath, head_sha: repo.head, head_branch: 'main', ...fake.run })],
    jobs: { 60: [restJob('full-tests', fake.run?.conclusion ?? 'success')] },
    dispatch: { workflow_run_id: 60 },
  });

  test('exits 0 and prints the run link when the run passes', { timeout: 120_000 }, async (t) => {
    const repo = repositoryWithTooling(t);
    const result = await start(repo, {});
    assert.equal(result.code, 0, `${result.stdout}\n${result.stderr}`);
    assert.ok(result.stdout.includes(runUrl(60)), result.stdout);
    assert.deepEqual(result.record().dispatches, [{ workflow: 'all-tests.yml', ref: 'main', inputs: {} }]);
  });

  test('exits nonzero and prints the run link when the run fails', { timeout: 120_000 }, async (t) => {
    const repo = repositoryWithTooling(t);
    const result = await start(repo, { run: { conclusion: 'failure' } });
    assert.notEqual(result.code, 0);
    assert.ok((result.stdout + result.stderr).includes(runUrl(60)), `${result.stdout}\n${result.stderr}`);
  });
});
