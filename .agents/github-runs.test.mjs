import assert from 'node:assert/strict';
import { once } from 'node:events';
import { watch } from 'node:fs';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, test } from 'node:test';
import { Writable } from 'node:stream';
import { createCommands, createGitHubRuns, createTerminalOutput } from './github-runs.mjs';
import { createFakeGitHub, jobUrl, repositoryName, restJob, restRun, runUrl } from './github-runs.fake-github.mjs';
import { createGit } from './version-tag.mjs';
import { commitA, commitB } from './version-tag.fake-git.mjs';

// github-runs.mjs is the one place that talks to GitHub for the release command, the Run All tests command, and the run check.
// Its interface, as these tests define it, is described at the top of github-runs.mjs.
const url = `https://github.com/${repositoryName}`;

describe('reading release uploads (unit)', () => {
  test('reads a draft through its numeric identifier rather than the published tag endpoint', async () => {
    const draft = { id: 42, tag_name: 'android-v1.0.0', draft: true, assets: [{ name: 'app.apk' }] };
    const github = createGitHubRuns(async (file, args) => {
      assert.equal(file, 'gh');
      if (args[0] === 'release') return '{"databaseId":42}';
      if (args.includes('repos/example/tasks/releases/42')) return JSON.stringify(draft);
      throw new Error('The published tag endpoint cannot find a draft.');
    }, { repository: 'example/tasks' });
    assert.deepEqual(await github.release('android-v1.0.0'), draft);
  });

  for (const databaseId of [null, '42', 0]) {
    test(`rejects an invalid release identifier ${JSON.stringify(databaseId)}`, async () => {
      const github = createGitHubRuns(async (file, args) =>
        args[0] === 'release' ? JSON.stringify({ databaseId }) : '{"draft":true}',
      { repository: 'example/tasks' });
      await assert.rejects(github.release('android-v1.0.0'), /invalid release identifier/);
    });
  }

  test('propagates a failed draft lookup instead of using a published release', async () => {
    const failure = new Error('Draft lookup denied');
    const github = createGitHubRuns(async (file, args) => {
      if (args[0] === 'release') throw failure;
      return '{"draft":false}';
    }, { repository: 'example/tasks' });
    await assert.rejects(github.release('android-v1.0.0'), error => error === failure);
  });
});

function connected(options = {}) {
  const fake = createFakeGitHub(options);
  return { fake, github: createGitHubRuns(fake.execute, { clock: fake.clock }) };
}

async function connectedGitHub(options) {
  const made = connected(options);
  await made.github.connect();
  return made;
}

describe('origin and sign-in (unit)', () => {
  for (const [label, origin] of [
    ['an HTTPS address', 'https://github.com/example/tasks.git\n'],
    ['an HTTPS address without .git', 'https://github.com/example/tasks\n'],
    ['an SSH address', 'git@github.com:example/tasks.git\n'],
  ]) {
    test(`reads the repository name and link from ${label}`, async () => {
      const { github } = connected({ origin });
      assert.deepEqual(await github.connect(), { name: repositoryName, url });
    });
  }

  test('checks the sign-in on github.com after it accepts origin', async () => {
    const { fake, github } = connected();
    await github.connect();
    assert.ok(fake.calls.some((call) => call.join(' ') === 'gh auth status --hostname github.com'), fake.calls.join('\n'));
  });

  for (const origin of ['C:\\local\\origin.git\n', 'https://other.example/example/tasks.git\n', 'https://github.com/example\n', 'https://github.com/example/tasks/extra\n']) {
    test(`rejects origin ${JSON.stringify(origin)} before contacting GitHub`, async () => {
      const { fake, github } = connected({ origin });
      await assert.rejects(() => github.connect(), /origin/i);
      assert.ok(fake.calls.every((call) => call[0] === 'git'), 'only git was asked');
    });
  }

  for (const pushOrigin of ['git@github.com:someone/else.git\n', 'git@github.com:example/tasks.git\ngit@github.com:someone/else.git\n']) {
    test(`rejects an origin that pushes elsewhere or to several places (${pushOrigin.trim().split('\n').length} push address)`, async () => {
      const { fake, github } = connected({ pushOrigin });
      await assert.rejects(() => github.connect(), /push|origin/i);
      assert.ok(fake.calls.every((call) => call[0] === 'git'), 'only git was asked');
    });
  }

  test('names gh auth login when the sign-in check fails', async () => {
    const { github } = connected({ authError: 'not logged in' });
    await assert.rejects(() => github.connect(), /gh auth login/);
  });
});

describe('starting a workflow (unit)', () => {
  const started = restRun({ id: 77, head_sha: commitA, status: 'queued', conclusion: null, event: 'workflow_dispatch' });

  test('posts the workflow file, the ref, and each input, and asks GitHub for the new run', async () => {
    const { fake, github } = await connectedGitHub({ runs: [started], dispatch: { workflow_run_id: 77 } });
    await github.dispatch('release.yml', 'main', { part: 'minor', tag: 'v1.0.0', commit: commitA });
    assert.deepEqual(fake.dispatches, [{ workflow: 'release.yml', ref: 'main', inputs: { part: 'minor', tag: 'v1.0.0', commit: commitA } }]);
  });

  test('posts a workflow without inputs', async () => {
    const { fake, github } = await connectedGitHub({ runs: [started], dispatch: { workflow_run_id: 77 } });
    await github.dispatch('all-tests.yml', 'feature');
    assert.deepEqual(fake.dispatches, [{ workflow: 'all-tests.yml', ref: 'feature', inputs: {} }]);
  });

  test('returns the started run with its ID, commit, and link', async () => {
    const { github } = await connectedGitHub({ runs: [started], dispatch: { workflow_run_id: 77 } });
    const run = await github.dispatch('all-tests.yml', 'main');
    assert.equal(run.databaseId, 77);
    assert.equal(run.headSha, commitA);
    assert.equal(run.url, runUrl(77));
  });

  test('returns the started run even when it runs another commit, so the command can refuse it', async () => {
    const { github } = await connectedGitHub({ runs: [{ ...started, head_sha: commitB }], dispatch: { workflow_run_id: 77 } });
    assert.equal((await github.dispatch('all-tests.yml', 'main')).headSha, commitB);
  });

  test('fails, naming the actions page, when GitHub does not return the run, instead of guessing among recent runs', async () => {
    const { fake, github } = await connectedGitHub({ runs: [started], dispatch: {} });
    await assert.rejects(() => github.dispatch('all-tests.yml', 'main'), new RegExp(`${url}/actions`));
    assert.equal(fake.dispatches.length, 1, 'it never starts a second run');
  });
});

describe('a run that started but cannot be read (unit)', () => {
  test('rejects with the run link, saying the run was started, when reading it after the dispatch fails', async () => {
    const fake = createFakeGitHub({ runs: [restRun({ id: 77 })], dispatch: { workflow_run_id: 77 } });
    const execute = async (file, args, settings) => {
      if (args[0] === 'api' && !args.includes('POST') && args.some((arg) => /\/actions\/runs\/77$/.test(arg))) throw new Error('gh api failed: HTTP 502');
      return fake.execute(file, args, settings);
    };
    const github = createGitHubRuns(execute, { repository: repositoryName, clock: fake.clock });
    await assert.rejects(() => github.dispatch('all-tests.yml', 'main'), (error) => error.message.includes(runUrl(77)) && /started/i.test(error.message));
    assert.equal(fake.dispatches.length, 1, 'it never starts a second run');
  });
});

describe('every gh api call names github.com (unit)', () => {
  const hostOf = (call) => call[call.indexOf('--hostname') + 1];
  const apiCalls = (fake) => fake.calls.filter((call) => call[0] === 'gh' && call[1] === 'api');
  const assertHost = (fake, count) => {
    const calls = apiCalls(fake);
    assert.ok(calls.length >= count, `expected at least ${count} gh api call(s): ${fake.calls.join('\n')}`);
    for (const call of calls) assert.equal(hostOf(call), 'github.com', call.join(' '));
  };
  const run = restRun({ id: 77, status: 'queued', conclusion: null });
  const options = { runs: [run], jobs: { 77: [restJob('full-tests', 'success')] }, dispatch: { workflow_run_id: 77 }, afterReads: { 77: [{ status: 'completed', conclusion: 'success' }] } };

  test('names it when it starts a workflow and reads the started run', async () => {
    const { fake, github } = await connectedGitHub(options);
    await github.dispatch('all-tests.yml', 'main');
    assertHost(fake, 2);
  });

  test('names it when it follows a run', async () => {
    const { fake, github } = await connectedGitHub(options);
    await github.follow({ databaseId: 77, url: runUrl(77) });
    assertHost(fake, 1);
  });

  test('names it when it reads the jobs of a run and its failed jobs', async () => {
    const { fake, github } = await connectedGitHub(options);
    await github.jobs(77);
    await github.failedJobs({ databaseId: 77, url: runUrl(77) });
    assertHost(fake, 2);
  });

  test('names it when it lists the runs of a workflow', async () => {
    const { fake, github } = await connectedGitHub(options);
    await github.runs('all-tests.yml', commitA, 'workflow_dispatch');
    assertHost(fake, 1);
  });
});

describe('following a run (unit)', () => {
  const base = restRun({ id: 77, status: 'queued', conclusion: null });
  const runOf = async (github) => github.dispatch('all-tests.yml', 'main');

  test('returns the finished run once GitHub says it completed, however it ended', async () => {
    for (const conclusion of ['success', 'failure', 'cancelled']) {
      const { github } = await connectedGitHub({
        runs: [base], dispatch: { workflow_run_id: 77 },
        afterReads: { 77: [{}, { status: 'in_progress', conclusion: null }, { status: 'completed', conclusion }] },
      });
      const finished = await github.follow(await runOf(github));
      assert.equal(finished.status, 'completed');
      assert.equal(finished.conclusion, conclusion);
      assert.equal(finished.url, runUrl(77));
    }
  });

  test('returns at once, without a wait, a run that already finished', async () => {
    const { fake, github } = await connectedGitHub({ runs: [{ ...base, status: 'completed', conclusion: 'success' }], dispatch: { workflow_run_id: 77 } });
    await github.follow(await runOf(github));
    assert.equal(fake.clock.now(), 0);
  });

  test('stops waiting for a run that never ends, and fails with the run link', async () => {
    const { fake, github } = await connectedGitHub({ runs: [base], dispatch: { workflow_run_id: 77 }, afterReads: { 77: [{ status: 'in_progress', conclusion: null }] } });
    const run = await runOf(github);
    await assert.rejects(() => github.follow(run), (error) => error.message.includes(runUrl(77)));
    assert.ok(fake.clock.now() > 0, 'it waited, within a bound');
  });

  test('never accepts an unfinished run as a pass', async () => {
    const { github } = await connectedGitHub({ runs: [base], dispatch: { workflow_run_id: 77 }, afterReads: { 77: [{ status: 'in_progress', conclusion: 'success' }] } });
    const run = await runOf(github);
    await assert.rejects(() => github.follow(run));
  });

  test('reads only the named run in the origin repository', async () => {
    const { fake, github } = await connectedGitHub({ runs: [{ ...base, status: 'completed' }], dispatch: { workflow_run_id: 77 } });
    await github.follow(await runOf(github));
    const apiCalls = fake.calls.filter((call) => call[1] === 'api' && !call.includes('POST'));
    assert.ok(apiCalls.length >= 1);
    for (const call of apiCalls) assert.ok(call.some((arg) => arg.startsWith(`repos/${repositoryName}/actions/runs/77`)), call.join(' '));
  });
});

describe('a run\'s failed jobs (unit)', () => {
  const run = { databaseId: 40, url: runUrl(40) };

  test('lists each failed or cancelled job with its link, and no other job', async () => {
    const jobs = [restJob('Plan', 'success', 1, 1), restJob('Tests / full-tests', 'failure', 1, 2), restJob('Tag', 'skipped', 1, 3), restJob('Publish', 'cancelled', 1, 4), restJob('Other', 'timed_out', 1, 5)];
    const { github } = await connectedGitHub({ jobs: { 40: jobs } });
    assert.deepEqual(await github.failedJobs(run), [
      { name: 'Tests / full-tests', url: jobUrl(2) },
      { name: 'Publish', url: jobUrl(4) },
      { name: 'Other', url: jobUrl(5) },
    ]);
  });

  test('lists nothing when no job failed', async () => {
    const { github } = await connectedGitHub({ jobs: { 40: [restJob('Plan', 'success')] } });
    assert.deepEqual(await github.failedJobs(run), []);
  });

  test('judges only the newest attempt, so a re-run job that passed is not listed', async () => {
    const { github } = await connectedGitHub({ jobs: { 40: [restJob('Publish', 'failure', 1, 4), restJob('Publish', 'success', 2, 5)] } });
    assert.deepEqual(await github.failedJobs(run), []);
  });
});

describe('reading runs and jobs for the run check (unit)', () => {
  test('lists the runs of a workflow file for one commit and event, in the named repository, and reads them as runs', async () => {
    const { fake, github } = await connectedGitHub({ runs: [restRun({ id: 31, head_sha: commitB })] });
    const runs = await github.runs('all-tests.yml', commitB, 'workflow_dispatch');
    assert.deepEqual(runs, [{
      databaseId: 31, path: '.github/workflows/all-tests.yml', event: 'workflow_dispatch', headSha: commitB, status: 'completed', conclusion: 'success', url: runUrl(31),
    }]);
    const call = fake.calls.find((candidate) => candidate.some((arg) => arg.includes('/runs?')));
    const endpoint = call.find((arg) => arg.startsWith('repos/'));
    assert.ok(endpoint.startsWith(`repos/${repositoryName}/actions/workflows/all-tests.yml/runs?`), endpoint);
    assert.match(endpoint, new RegExp(`head_sha=${commitB}`));
    assert.match(endpoint, /event=workflow_dispatch/);
  });

  test('lists the runs of a commit without an event filter when none is given', async () => {
    const { fake, github } = await connectedGitHub({ runs: [] });
    await github.runs('all-tests.yml', commitA);
    const endpoint = fake.calls.flat().find((arg) => /\/runs\?/.test(arg));
    assert.doesNotMatch(endpoint, /event=/);
  });

  test('does not filter the list itself: it returns the runs GitHub gave, whatever their commit or event', async () => {
    const { github } = await connectedGitHub({ runs: [restRun({ id: 31, head_sha: commitB, event: 'push' }), restRun({ id: 32 })] });
    assert.deepEqual((await github.runs('all-tests.yml', commitA)).map((run) => run.databaseId), [31, 32]);
  });

  test('reads the jobs of the newest attempt only, so a re-run replaces the attempt it repeats', async () => {
    const jobs = { 30: [restJob('full-tests', 'failure', 1, 1), restJob('full-tests', 'success', 2, 2)] };
    const { fake, github } = await connectedGitHub({ jobs });
    assert.deepEqual(await github.jobs(30), [{ name: 'full-tests', conclusion: 'success', url: jobUrl(2) }]);
    assert.ok(fake.calls.flat().some((arg) => /\/jobs\?.*filter=latest/.test(arg)), 'the query asks for filter=latest');
  });

  test('reads the repository it was given without asking origin or the sign-in', async () => {
    const fake = createFakeGitHub({ runs: [restRun()] });
    const github = createGitHubRuns(fake.execute, { repository: repositoryName, clock: fake.clock });
    assert.equal((await github.runs('all-tests.yml', commitA)).length, 1);
    assert.deepEqual(fake.calls.filter((call) => call[0] === 'git' || call[1] === 'auth'), []);
  });

  for (const [label, run] of [
    ['a body that is not a list of runs', async (github) => github.runs('all-tests.yml', commitA)],
    ['a body without jobs', async (github) => github.jobs(30)],
  ]) {
    test(`rejects ${label}`, async () => {
      const github = createGitHubRuns(async () => '{"wrong":"shape"}', { repository: repositoryName });
      await assert.rejects(() => run(github), /response|invalid/i);
    });
  }

  test('lets a gh failure through', async () => {
    const github = createGitHubRuns(async () => { throw new Error('gh api failed: HTTP 403'); }, { repository: repositoryName });
    await assert.rejects(() => github.runs('all-tests.yml', commitA), /HTTP 403/);
  });
});

const colorVariables = ['NO_COLOR', 'FORCE_COLOR', 'NODE_DISABLE_COLORS'];
const yellow = (text) => `\u001b[33m${text}\u001b[39m`;
const cyan = (text) => `\u001b[36m${text}\u001b[39m`;

function isolateColorEnvironment(t, changes) {
  const saved = Object.fromEntries(colorVariables.map((name) => [name, process.env[name]]));
  t.after(() => {
    for (const name of colorVariables) {
      if (saved[name] === undefined) delete process.env[name];
      else process.env[name] = saved[name];
    }
  });
  for (const name of colorVariables) delete process.env[name];
  Object.assign(process.env, changes);
}

function terminalFixture(supportsTerminalColor) {
  const writes = [];
  const output = new Writable();
  output.write = (text) => { writes.push(text); return true; };
  if (supportsTerminalColor) {
    output.isTTY = true;
    output.getColorDepth = (env = process.env) => ('NO_COLOR' in env || env.FORCE_COLOR === '0' ? 1 : 8);
  }
  const prompts = [];
  const answers = [];
  const terminal = { question: (text, options) => new Promise((resolve) => { prompts.push({ text, options }); answers.push(resolve); }) };
  const controller = new AbortController();
  return { services: createTerminalOutput(terminal, output, controller.signal), writes, prompts, answers, signal: controller.signal };
}

describe('terminal output (unit)', () => {
  for (const [label, environment, supportsTerminalColor, colored] of [
    ['a color terminal', {}, true, true],
    ['color forced on a stream without a terminal', { FORCE_COLOR: '1' }, false, true],
    ['a stream that is not a terminal', {}, false, false],
    ['NO_COLOR on a color terminal', { NO_COLOR: '1' }, true, false],
  ]) {
    test(`${colored ? 'shows' : 'does not show'} colors on ${label}`, async (t) => {
      isolateColorEnvironment(t, environment);
      const f = terminalFixture(supportsTerminalColor);
      const answer = f.services.ask('Type yes: ');
      assert.equal(f.prompts.length, 1, 'the question must reach the terminal before it is answered');
      assert.equal(f.prompts[0].text, colored ? yellow('Type yes: ') : 'Type yes: ');
      assert.equal(f.prompts[0].options.signal, f.signal);
      f.answers[0]('yes');
      assert.equal(await answer, 'yes');

      f.services.waitForGitHub('Waiting for the branch checks.\n');
      assert.equal(f.writes.length, 1);
      if (colored) assert.match(f.writes[0], /^\u001b\[36m[^\u001b]*Waiting for the branch checks\.[^\u001b]*\u001b\[39m\n?$/);
      else assert.equal(f.writes[0], 'Waiting for the branch checks.\n');
    });
  }

  test('writes ordinary output unchanged even when the terminal shows colors', (t) => {
    isolateColorEnvironment(t, {});
    const f = terminalFixture(true);
    f.services.write('Published.\n');
    assert.deepEqual(f.writes, ['Published.\n']);
  });

  test('uses different colors for questions and GitHub waits', (t) => {
    isolateColorEnvironment(t, { FORCE_COLOR: '1' });
    const f = terminalFixture(false);
    f.services.ask('Same words');
    f.services.waitForGitHub('Same words');
    assert.equal(f.prompts[0].text, yellow('Same words'));
    assert.equal(f.writes[0], cyan('Same words'));
  });
});

describe('the executor of git and gh on real processes (integration)', () => {
  test('runs native commands with literal arguments and reports nonzero exits', async (t) => {
    const directory = await mkdtemp(join(tmpdir(), 'memoria-command-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const execute = createCommands(directory);
    assert.equal(await execute(process.execPath, ['-e', 'process.stdout.write(process.argv[1])', 'literal & value']), 'literal & value');
    assert.equal(await execute(process.execPath, ['-e', 'process.stdin.pipe(process.stdout)'], { input: 'tag object input' }), 'tag object input');
    await assert.rejects(() => execute(process.execPath, ['-e', 'process.stderr.write("specific failure"); process.exitCode = 3']), /specific failure/);
  });

  test('cancels a running command after the child signals readiness', { timeout: 10_000 }, async (t) => {
    const directory = await mkdtemp(join(tmpdir(), 'memoria-abort-'));
    const controller = new AbortController();
    const watcher = watch(directory);
    t.after(async () => {
      controller.abort();
      const closed = once(watcher, 'close');
      watcher.close();
      await closed;
      await rm(directory, { recursive: true, force: true });
    });
    const ready = once(watcher, 'change');
    const execute = createCommands(directory, controller.signal);
    const stopped = assert.rejects(
      () => execute(process.execPath, ['-e', 'require("node:fs").writeFileSync("ready", "");setInterval(() => {}, 1000)']),
      /abort/i,
    );
    await ready;
    controller.abort();
    await stopped;
  });

  test('routes git through the cancellable executor instead of starting unbounded git', async (t) => {
    const directory = await mkdtemp(join(tmpdir(), 'memoria-git-abort-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const controller = new AbortController();
    controller.abort();
    const git = createGit(directory, createCommands(directory, controller.signal));
    await assert.rejects(() => git.isClean(), /abort/i);
  });
});
