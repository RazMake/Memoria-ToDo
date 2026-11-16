import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { once } from 'node:events';
import { watch } from 'node:fs';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { Writable } from 'node:stream';
import * as release from './release.mjs';
import { createCommands, createGitHub, run } from './release.mjs';
import { createGit } from './version-tag.mjs';
import { changelogWith, commitA, commitB, createFakeFiles, createFakeGit } from './version-tag.fake-git.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const repository = { nameWithOwner: 'example/tasks', url: 'https://github.com/example/tasks', pushUrl: 'git@github.com:example/tasks.git' };
const branchRun = { databaseId: 10, headBranch: 'main', headSha: commitA, status: 'completed', conclusion: 'success', url: `${repository.url}/actions/runs/10` };
const tagRun = { ...branchRun, databaseId: 20, headBranch: 'v1.0.0' };
const published = { tagName: 'v1.0.0', isDraft: false, isPrerelease: false, url: `${repository.url}/releases/tag/v1.0.0`, assets: [{ name: 'MemoriaToDo-win-Setup.exe', size: 123 }] };

const runningBranchRun = { ...branchRun, status: 'in_progress', conclusion: null };

function session(options = {}) {
  const git = createFakeGit({ originHead: commitA, changelog: changelogWith('1.0.0'), ...options.git });
  const output = [];
  const githubWaits = [];
  const questions = [];
  const firstBranchRun = options.branchRun ?? runningBranchRun;
  const answers = [...(options.answers ?? ['yes', 'yes'])];
  let tested = false;
  let appTestedAfterQuestions;
  const services = {
    interactive: true,
    git,
    files: createFakeFiles(),
    write: (text) => output.push(text),
    waitForGitHub: (text) => { githubWaits.push(text); output.push(text); },
    ask: async (question) => {
      if (question.startsWith('Version part')) return '';
      questions.push(question);
      assert.ok(answers.length, `Unexpected question: ${question}`);
      const answer = answers.shift();
      return typeof answer === 'function' ? answer(git) : answer;
    },
    testTooling: async () => { tested = true; },
    testApp: async () => { appTestedAfterQuestions = questions.length; },
    github: {
      connect: async () => repository,
      ensureOrigin: async () => {},
      runs: async () => [],
      waitForRun: async (ref) => ref === 'main' ? firstBranchRun : tagRun,
      watch: async (job) => {
        if (job.headBranch === 'main') assert.deepEqual(git.snapshot().remote, {});
        else assert.equal(git.snapshot().remote['v1.0.0'], commitA);
      },
      release: async () => published,
      download: async () => join(tmpdir(), 'example', 'MemoriaToDo-win-Setup.exe'),
      ...options.github,
    },
    ...options.services,
  };
  return { git, services, questions, output, githubWaits, tested: () => tested, appTestedAfterQuestions: () => appTestedAfterQuestions };
}

const finishedBranchRun = (conclusion, status = 'completed') => ({ ...branchRun, status, conclusion });

describe('guided release (unit)', () => {
  test('tests the tooling, publishes the previewed commit, and guides installation and smoke checks', async () => {
    const s = session();
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.equal(s.tested(), true);
    assert.deepEqual(s.git.snapshot(), { local: { 'v1.0.0': commitA }, remote: { 'v1.0.0': commitA } });
    assert.match(s.output.join('\n'), /Setup\.exe/);
    assert.match(s.output.join('\n'), /unsigned/i);
    assert.match(s.questions.at(-1), /1\.0\.0/);
    assert.match(s.questions.at(-1), /tray|Task View/i);
    assert.match(s.output.join('\n'), /confirmed/i);
  });

  test('accepts a requested patch without changing later version rules', async () => {
    const s = session({
      git: { local: { 'v1.2.3': commitA }, remote: { 'v1.2.3': commitA }, changelog: changelogWith('1.2.4') },
      github: { watch: async () => {}, release: async () => ({ ...published, tagName: 'v1.2.4' }) },
    });
    assert.equal((await run(['patch'], s.services)).code, 0);
    assert.equal(s.git.snapshot().remote['v1.2.4'], commitA);
  });

  for (const [label, args, interactive] of [['a noninteractive terminal', [], false], ['an unknown part', ['everything'], true], ['tag-deletion arguments', ['--clear'], true]]) {
    test(`rejects ${label} before git or GitHub operations`, async () => {
      const s = session({ services: { interactive }, github: { connect: async () => assert.fail('must not contact GitHub') } });
      const result = await run(args, s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, /terminal|major|minor|patch/i);
      assert.deepEqual(s.git.calls, []);
      assert.equal(s.tested(), false);
    });
  }

  for (const answer of ['no', '', 'y']) {
    test(`does not publish when confirmation is ${JSON.stringify(answer)}`, async () => {
      const s = session({ answers: [answer] });
      assert.equal((await run([], s.services)).code, 0);
      assert.deepEqual(s.git.snapshot(), { local: {}, remote: {} });
      assert.match(s.output.join('\n'), /cancel/i);
    });
  }

  test('lets the user fix an uncommitted working tree and retry the preview', async () => {
    const s = session({ git: { clean: false }, answers: [(git) => { git.configure({ clean: true }); return ''; }, 'yes', 'yes'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.match(s.output.join('\n'), /uncommitted/);
    assert.match(s.questions[0], /commit|push/i);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  test('guides a missing changelog fix and checks the newly committed notes again', async () => {
    const s = session({ git: { changelog: changelogWith('0.1.0') }, answers: [(git) => { git.configure({ changelog: changelogWith('1.0.0') }); return ''; }, 'yes', 'yes'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.match(s.output.join('\n'), /1\.0\.0/);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  test('allows cancellation rather than repeatedly retrying a refused preview', async () => {
    const s = session({ git: { clean: false }, answers: ['cancel'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.deepEqual(s.git.snapshot().remote, {});
    assert.match(s.output.join('\n'), /cancel/i);
  });

  test('runs every app test only once the preview passes, so it tests the commit it tags', async () => {
    const s = session({ git: { clean: false }, answers: [(git) => { git.configure({ clean: true }); return ''; }, 'yes', 'yes'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.equal(s.appTestedAfterQuestions(), 1);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  for (const stage of ['connect', 'tests', 'app tests', 'branch']) {
    test(`reports a ${stage} failure before publishing any tag`, async () => {
      const failure = async () => { throw new Error(`${stage} failed`); };
      const s = session({
        services: stage === 'tests' ? { testTooling: failure } : stage === 'app tests' ? { testApp: failure } : {},
        github: stage === 'connect' ? { connect: failure } : stage === 'branch' ? { watch: failure } : {},
      });
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, new RegExp(`${stage} failed`));
      assert.deepEqual(s.git.snapshot().remote, {});
    });
  }

  test('does not silently approve another commit when the plan changes after confirmation', async () => {
    const s = session({ answers: [(git) => { git.configure({ originHead: commitB }); return 'yes'; }] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /approved|preview|behind/i);
    assert.deepEqual(s.git.snapshot().remote, {});
  });

  for (const stage of ['release workflow', 'download']) {
    test(`keeps the published tag and gives recovery guidance after a ${stage} failure`, async () => {
      const s = session({ github: stage === 'download'
        ? { download: async () => { throw new Error('download failed'); } }
        : { watch: async (job) => { if (job.headBranch !== 'main') throw new Error('release workflow failed'); } },
      });
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, new RegExp(`${stage} failed`));
      assert.match(result.stderr, /tag.*remain|tag.*kept/i);
      assert.match(result.stderr, /https:\/\/github\.com\/example\/tasks/);
      assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
    });
  }

  test('does not report manual verification as successful when the user has not confirmed it', async () => {
    const s = session({ answers: ['yes', 'no'] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /already published|already public/i);
    assert.match(result.stderr, /manual|install/i);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });
});

describe('release publishing approval (unit)', () => {
  const finalQuestion = /tray|Task View/i;

  test('publishes without a publishing question when the first branch run had already passed, but still asks for the manual checks', async () => {
    const s = session({ branchRun, answers: ['yes'] });
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(s.git.snapshot(), { local: { 'v1.0.0': commitA }, remote: { 'v1.0.0': commitA } });
    assert.equal(s.questions.length, 1);
    assert.match(s.questions[0], finalQuestion);
    assert.match(s.output.join('\n'), /already passed/i);
  });

  test('still fails when the manual installer checks are not confirmed after an automatic publish', async () => {
    const s = session({ branchRun, answers: ['no'] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /already published|already public/i);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  for (const [status, conclusion] of [['queued', null], ['in_progress', null], ['waiting', null], ['requested', null], ['pending', null], ['in_progress', 'success']]) {
    test(`asks before publishing when the first branch run was ${status} with conclusion ${conclusion}, even though watching succeeds`, async () => {
      const watched = [];
      const s = session({
        branchRun: finishedBranchRun(conclusion, status),
        answers: ['no'],
        github: { watch: async (job) => { watched.push(job.databaseId); } },
      });
      const result = await run([], s.services);
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(watched, [branchRun.databaseId]);
      assert.equal(s.questions.length, 1);
      assert.match(s.questions[0], /publish/i);
      assert.deepEqual(s.git.snapshot(), { local: {}, remote: {} });
      assert.match(s.output.join('\n'), /cancel/i);
    });
  }

  test('publishes a build that was waited for once the user types yes', async () => {
    const s = session({ branchRun: finishedBranchRun(null, 'queued') });
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.equal(s.questions.length, 2);
    assert.match(s.questions[0], /publish/i);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  for (const conclusion of ['failure', 'cancelled', 'timed_out', 'skipped', null]) {
    test(`stops without asking or tagging when the first branch run finished with ${conclusion}`, async () => {
      const s = session({ branchRun: finishedBranchRun(conclusion), answers: [] });
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, /branch checks/i);
      assert.deepEqual(s.questions, []);
      assert.deepEqual(s.git.snapshot(), { local: {}, remote: {} });
    });
  }

  test('refuses a plan that changed after an already passed build, without asking or tagging', async () => {
    const s = session({ branchRun, answers: [] });
    s.services.github.waitForRun = async (ref) => {
      if (ref === 'main') s.git.configure({ originHead: commitB });
      return ref === 'main' ? branchRun : tagRun;
    };
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /approved|preview|behind/i);
    assert.deepEqual(s.questions, []);
    assert.deepEqual(s.git.snapshot().remote, {});
  });

  test('refuses a destination changed after an already passed build, without asking or tagging', async () => {
    const s = session({ branchRun, answers: [], github: { ensureOrigin: async () => { throw new Error('origin push destination changed'); } } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /destination changed/);
    assert.deepEqual(s.questions, []);
    assert.deepEqual(s.git.snapshot(), { local: {}, remote: {} });
  });

  test('pins the tag push to the validated endpoint after an already passed build', async () => {
    const s = session({ branchRun, answers: ['yes'] });
    const push = s.git.pushTag;
    const destinations = [];
    s.git.pushTag = async (name, id, destination) => {
      destinations.push(destination);
      return push(name, id);
    };
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(destinations, [repository.pushUrl]);
  });
});

describe('release waits on GitHub (unit)', () => {
  test('announces each GitHub wait before starting it, separately from questions', async () => {
    const seen = [];
    const s = session({ answers: ['yes', 'yes'] });
    const github = s.services.github;
    for (const name of ['connect', 'waitForRun', 'watch', 'release']) {
      const original = github[name];
      github[name] = async (...args) => {
        seen.push(s.githubWaits.length);
        return original(...args);
      };
    }
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    // connect, branch find, branch watch, tag find, tag watch, release fetch
    assert.equal(seen.length, 6);
    assert.ok(seen[0] >= 1);
    for (let i = 1; i < seen.length; i++) assert.ok(seen[i] > seen[i - 1], `wait ${i} was not announced before its GitHub call: ${seen}`);
    assert.ok(s.githubWaits.every((text) => !s.questions.includes(text)));
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
  assert.equal(typeof release.createTerminalOutput, 'function', 'release.mjs must export createTerminalOutput(terminal, output, signal)');
  return { services: release.createTerminalOutput(terminal, output, controller.signal), writes, prompts, answers, signal: controller.signal };
}

describe('release terminal output (unit)', () => {
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

function githubFixture(options = {}) {
  let time = 0;
  const calls = [];
  const execute = async (file, args) => {
    calls.push([file, ...args]);
    if (file === 'git') return args.includes('--push') ? (options.pushOrigin ?? 'git@github.com:example/tasks.git\n') : (options.origin ?? 'https://github.com/example/tasks.git\n');
    if (args[0] === 'auth') {
      if (options.authError) throw new Error(options.authError);
      return '';
    }
    if (args[0] === 'repo') return JSON.stringify(repository);
    if (args[0] === 'run' && args[1] === 'list') return options.runJson ?? JSON.stringify(typeof options.runs === 'function' ? options.runs() : options.runs ?? [branchRun, tagRun]);
    if (args[0] === 'run' && args[1] === 'watch') return '';
    if (args[0] === 'run' && args[1] === 'view') return JSON.stringify(options.runResult ?? { status: 'completed', conclusion: 'success' });
    if (args[0] === 'release' && args[1] === 'view') return JSON.stringify(options.release ?? published);
    assert.fail(`Unexpected command: ${file} ${args.join(' ')}`);
  };
  const github = createGitHub(execute, { now: () => time, wait: async (ms) => { time += ms; } });
  return { github, calls };
}

describe('GitHub release boundary (unit)', () => {
  test('uses the origin repository explicitly instead of a gh default', async () => {
    const { github, calls } = githubFixture();
    assert.deepEqual(await github.connect(), repository);
    await github.runs('v1.0.0', commitA);
    const list = calls.find((call) => call[1] === 'run');
    assert.equal(list[list.indexOf('--repo') + 1], repository.nameWithOwner);
    assert.equal(list[list.indexOf('--commit') + 1], commitA);
  });

  for (const origin of ['C:\\local\\origin.git', 'https://other.example/example/tasks.git']) {
    test(`rejects a non-GitHub origin ${origin}`, async () => {
      const { github, calls } = githubFixture({ origin });
      await assert.rejects(() => github.connect(), /origin.*GitHub|github\.com/i);
      assert.ok(calls.every((call) => call[0] === 'git'));
    });
  }

  for (const pushOrigin of ['git@github.com:someone/else.git\n', 'git@github.com:example/tasks.git\ngit@github.com:someone/else.git\n']) {
    test('rejects a different or multiple push destinations before contacting GitHub', async () => {
      const { github, calls } = githubFixture({ pushOrigin });
      await assert.rejects(() => github.connect(), /push|origin/i);
      assert.ok(calls.every((call) => call[0] === 'git'));
    });
  }

  test('explains how to sign in when GitHub authentication fails', async () => {
    const { github } = githubFixture({ authError: 'not logged in' });
    await assert.rejects(() => github.connect(), /gh auth login/);
  });

  test('ignores a branch run, another commit, and a previous run of the same tag', async () => {
    const old = { ...tagRun, databaseId: 15 };
    const wrongCommit = { ...tagRun, databaseId: 25, headSha: commitB };
    let queries = 0;
    const { github } = githubFixture({ runs: () => ++queries === 1 ? [branchRun, old, wrongCommit] : [branchRun, old, wrongCommit, tagRun] });
    await github.connect();
    const job = await github.waitForRun('v1.0.0', commitA, [15]);
    assert.equal(job.databaseId, 20);
    assert.equal(queries, 2);
  });

  test('uses the newest matching branch check when a commit was pushed more than once', async () => {
    const { github } = githubFixture({ runs: [{ ...branchRun, databaseId: 9 }, branchRun] });
    await github.connect();
    assert.equal((await github.waitForRun('main', commitA)).databaseId, 10);
  });

  test('fails explicitly when no matching workflow appears within the bound', async () => {
    const { github } = githubFixture({ runs: [] });
    await github.connect();
    await assert.rejects(() => github.waitForRun('v1.0.0', commitA, []), /workflow|Actions/i);
  });

  test('refuses ambiguous new workflows rather than choosing an unrelated run', async () => {
    const { github } = githubFixture({ runs: [tagRun, { ...tagRun, databaseId: 21 }] });
    await github.connect();
    await assert.rejects(() => github.waitForRun('v1.0.0', commitA, []), /multiple|ambiguous/i);
  });

  test('rejects malformed workflow responses', async () => {
    const { github } = githubFixture({ runJson: '{"wrong":"shape"}' });
    await github.connect();
    await assert.rejects(() => github.runs('v1.0.0', commitA), /workflow|response|array/i);
  });

  test('does not accept an unsuccessful workflow just because watch exited successfully', async () => {
    const { github } = githubFixture({ runResult: { status: 'completed', conclusion: 'failure' } });
    await github.connect();
    await assert.rejects(() => github.watch(tagRun), /failure|failed/i);
  });

  test('accepts the published tag and exactly one nonempty installer', async () => {
    const { github } = githubFixture();
    await github.connect();
    assert.deepEqual(await github.release('v1.0.0'), published);
  });

  for (const [label, changes] of [
    ['another tag', { tagName: 'v1.0.1' }],
    ['a draft', { isDraft: true }],
    ['a prerelease', { isPrerelease: true }],
    ['no installer', { assets: [] }],
    ['an empty installer', { assets: [{ name: 'App-Setup.exe', size: 0 }] }],
    ['ambiguous installers', { assets: [{ name: 'One-Setup.exe', size: 1 }, { name: 'Two-Setup.exe', size: 1 }] }],
  ]) {
    test(`rejects ${label} after the workflow finishes`, async () => {
      const { github } = githubFixture({ release: { ...published, ...changes } });
      await github.connect();
      await assert.rejects(() => github.release('v1.0.0'), /release|installer|tag/i);
    });
  }
});

describe('release destination changes (unit)', () => {
  test('refuses a destination changed after the publishing confirmation', async () => {
    let changed = false;
    const s = session({
      answers: [() => { changed = true; return 'yes'; }, 'yes'],
      github: { ensureOrigin: async () => { if (changed) throw new Error('origin push destination changed'); } },
    });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /destination changed/);
    assert.deepEqual(s.git.snapshot(), { local: {}, remote: {} });
  });

  test('pins the tag push to the endpoint whose branch checks passed', async () => {
    const s = session();
    const push = s.git.pushTag;
    s.git.pushTag = async (name, id, destination) => {
      assert.equal(destination, repository.pushUrl);
      return push(name, id);
    };
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.equal(s.git.snapshot().remote['v1.0.0'], commitA);
  });

  test('detects an origin changed after the initial GitHub connection', async () => {
    const options = {};
    const { github } = githubFixture(options);
    await github.connect();
    options.pushOrigin = 'git@github.com:someone/else.git\n';
    await assert.rejects(() => github.ensureOrigin(), /origin|destination/i);
  });
});

describe('release interruption reporting (unit)', () => {
  test('reports both a failed push and aborted rollback without claiming that the tag changed', async () => {
    const s = session({ git: { refusePush: 'push interrupted', refuseLocalDelete: 'operation aborted' }, answers: ['yes'] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /push interrupted/);
    assert.match(result.stderr, /operation aborted/);
    assert.doesNotMatch(result.stderr, /changed since/);
    assert.match(result.stderr, /may remain/);
    assert.equal(s.git.snapshot().local['v1.0.0'], commitA);
  });
});

describe('guided release process and files (integration)', () => {
  test('shows help without contacting GitHub', () => {
    const script = join(root, '.agents', 'release.mjs');
    const help = spawnSync(process.execPath, [script, '--help'], { encoding: 'utf8', timeout: 10_000 });
    assert.equal(help.status, 0, help.stderr);
    assert.match(help.stdout, /release\.mjs/);
  });

  test('refuses redirected input without contacting GitHub', () => {
    const script = join(root, '.agents', 'release.mjs');
    const result = spawnSync(process.execPath, [script], { encoding: 'utf8', timeout: 10_000, env: { ...process.env, PATH: '' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /interactive terminal/i);
    assert.doesNotMatch(result.stderr, /ENOENT/);
  });

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

  test('downloads the named installer into an isolated folder and verifies the file exists', async (t) => {
    let folder;
    const github = createGitHub(async (file, args) => {
      if (args[0] === 'release' && args[1] === 'download') {
        folder = args[args.indexOf('--dir') + 1];
        t.after(() => rm(folder, { recursive: true, force: true }));
        assert.equal(args[args.indexOf('--repo') + 1], repository.nameWithOwner);
        assert.equal(args[args.indexOf('--pattern') + 1], published.assets[0].name);
        await writeFile(join(folder, published.assets[0].name), 'installer');
        return '';
      }
      if (file === 'git') return 'git@github.com:example/tasks.git\n';
      if (args[0] === 'auth') return '';
      if (args[0] === 'repo') return JSON.stringify(repository);
      assert.fail(`Unexpected command ${file} ${args.join(' ')}`);
    });
    await github.connect();
    const path = await github.download('v1.0.0', published.assets[0].name);
    assert.equal(path, join(folder, published.assets[0].name));
    assert.equal(await readFile(path, 'utf8'), 'installer');
  });
});
