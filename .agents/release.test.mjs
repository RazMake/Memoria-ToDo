import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createGitHubRuns } from './github-runs.mjs';
import { createFakeGitHub, jobUrl, releaseWorkflowPath, repositoryName, restJob, restRun, runUrl } from './github-runs.fake-github.mjs';
import { repositoryWithTooling, runCommand } from './github-runs.fake-github.process.mjs';
import * as release from './release.mjs';
import { changelogWith, commitA, commitB, createFakeFiles, createFakeGit } from './version-tag.fake-git.mjs';

const { run } = release;

const agents = dirname(fileURLToPath(import.meta.url));
const url = `https://github.com/${repositoryName}`;
const published = { tagName: 'v1.0.0', isDraft: false, isPrerelease: false, url: `${url}/releases/tag/v1.0.0`, assets: [{ name: 'MemoriaToDo-win-Setup.exe', size: 123 }] };
const installerPath = join(tmpdir(), 'example', 'MemoriaToDo-win-Setup.exe');

// Test then Tag and Release, as GitHub lists it: the caller's run holds the called workflow's jobs, named "Tests / full-tests".
const releaseRun = (overrides = {}) => restRun({ id: 50, path: releaseWorkflowPath, head_sha: commitA, head_branch: 'main', ...overrides });
const jobsOf = (failed, ...skipped) => ['Plan', 'Tests / full-tests', 'Tag', 'Publish'].map((name, index) => restJob(
  name, name === failed ? 'failure' : skipped.includes(name) ? 'skipped' : 'success', 1, 100 + index,
));
const jobLink = (name) => jobUrl(100 + ['Plan', 'Tests / full-tests', 'Tag', 'Publish'].indexOf(name));

// The release command's services (see release.mjs): the real GitHub component over the one fake GitHub, the fake git of the version tag
// script, and plain stand-ins for the terminal, the Node tooling tests, and the release's files.
function session(options = {}) {
  const git = createFakeGit({ originHead: commitA, changelog: changelogWith('1.0.0'), ...options.git });
  const fake = createFakeGitHub({ runs: [releaseRun(options.run)], jobs: { 50: jobsOf() }, dispatch: { workflow_run_id: 50 }, ...options.fake });
  const timeline = [];
  const execute = async (file, args, settings) => {
    timeline.push({ call: [file, ...args] });
    return fake.execute(file, args, settings);
  };
  const github = createGitHubRuns(execute, { clock: fake.clock });
  const output = [];
  const questions = [];
  const downloads = [];
  const answers = [...(options.answers ?? ['yes', 'yes'])];
  let tested = 0;
  const services = {
    interactive: true,
    git,
    files: createFakeFiles(),
    write: (text) => output.push(text),
    waitForGitHub: (text) => { timeline.push({ wait: text }); output.push(text); },
    ask: async (question) => {
      if (question.startsWith('Version part')) return options.part ?? '';
      questions.push({ question, dispatchesSoFar: fake.dispatches.length, testedSoFar: tested });
      assert.ok(answers.length, `Unexpected question: ${question}`);
      const answer = answers.shift();
      return typeof answer === 'function' ? answer(git) : answer;
    },
    testTooling: async () => { tested += 1; },
    github,
    releases: {
      view: async (tag) => options.release ?? { ...published, tagName: tag, url: `${url}/releases/tag/${tag}` },
      download: async (tag, name) => { downloads.push([tag, name]); return installerPath; },
    },
    ...options.services,
  };
  return { git, fake, services, output, questions, downloads, timeline, text: () => output.join(''), tested: () => tested };
}

const nothingTagged = { local: {}, remote: {} };

describe('release command: preparing (unit)', () => {
  for (const [label, args, interactive] of [['a noninteractive terminal', [], false], ['an unknown part', ['everything'], true], ['two parts', ['major', 'minor'], true], ['tag-deletion arguments', ['--clear'], true]]) {
    test(`rejects ${label} before any git or GitHub operation`, async () => {
      const s = session({ services: { interactive } });
      const result = await run(args, s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, /terminal|major|minor|patch/i);
      assert.deepEqual(s.git.calls, []);
      assert.deepEqual(s.fake.calls, []);
      assert.equal(s.tested(), 0);
    });
  }

  test('names the interactive terminal and the GitHub Actions way to release when input is redirected', async () => {
    const s = session({ services: { interactive: false } });
    const { stderr } = await run([], s.services);
    assert.match(stderr, /interactive terminal/i);
    assert.match(stderr, /GitHub Actions/i);
    assert.doesNotMatch(stderr, /VS Code "release a version" task/i);
  });

  for (const part of ['major', 'minor', 'patch']) {
    test(`uses the ${part} part it was given without asking for one`, async () => {
      const s = session({ git: { local: { 'v1.2.3': commitA }, remote: { 'v1.2.3': commitA }, changelog: changelogWith('2.0.0', '1.3.0', '1.2.4') }, services: { ask: undefined } });
      s.services.ask = async (question) => { assert.ok(!question.startsWith('Version part'), 'must not ask for the part'); return 'no'; };
      await run([part], s.services);
      assert.match(s.text(), new RegExp({ major: 'v2\\.0\\.0', minor: 'v1\\.3\\.0', patch: 'v1\\.2\\.4' }[part]));
    });
  }

  test('asks for the part when none is given, and Enter chooses the default minor part', async () => {
    const asked = [];
    const s = session({ git: { local: { 'v1.2.3': commitA }, remote: { 'v1.2.3': commitA }, changelog: changelogWith('1.3.0') } });
    const ask = s.services.ask;
    s.services.ask = async (question) => { asked.push(question); return question.startsWith('Version part') ? '' : ask(question); };
    assert.equal((await run([], s.services)).code, 0);
    assert.match(asked[0], /^Version part/);
    assert.equal(s.fake.dispatches[0].inputs.part, 'minor');
  });

  test('uses the part typed when asked', async () => {
    const s = session({ part: 'patch', git: { local: { 'v1.2.3': commitA }, remote: { 'v1.2.3': commitA }, changelog: changelogWith('1.2.4') } });
    assert.equal((await run([], s.services)).code, 0);
    assert.equal(s.fake.dispatches[0].inputs.part, 'patch');
    assert.equal(s.fake.dispatches[0].inputs.tag, 'v1.2.4');
  });

  test('rejects an unknown part typed when asked, before any git or GitHub operation', async () => {
    const s = session({ part: 'everything' });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /major|minor|patch/);
    assert.deepEqual(s.fake.calls, []);
  });

  test('checks the GitHub sign-in and origin, and runs the Node tooling tests', async () => {
    const s = session({ answers: ['no'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.ok(s.fake.calls.some((call) => call.join(' ') === 'gh auth status --hostname github.com'));
    assert.ok(s.fake.calls.some((call) => call.join(' ') === 'git remote get-url origin'));
    assert.equal(s.tested(), 1);
  });

  test('runs no .NET test on this PC, leaving every test to the run on GitHub', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    assert.doesNotMatch(s.text(), /dotnet|every test kind/i);
  });

  test('stops before the preview, the question, and any run when the sign-in fails', async () => {
    const s = session({ fake: { authError: 'not logged in' } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /gh auth login/);
    assert.deepEqual(s.git.calls, []);
    assert.deepEqual(s.questions, []);
    assert.deepEqual(s.fake.dispatches, []);
  });

  test('stops before the preview and any run when the Node tooling tests fail', async () => {
    const s = session({ services: { testTooling: async () => { throw new Error('tooling failed'); } } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /tooling failed/);
    assert.deepEqual(s.git.calls, []);
    assert.deepEqual(s.fake.dispatches, []);
  });
});

describe('release command: the preview (unit)', () => {
  test('shows the preview, then asks once for yes, before it starts anything', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    assert.match(s.text(), /v1\.0\.0/);
    assert.match(s.text(), /a1a1a1a/);
    assert.equal(s.questions[0].dispatchesSoFar, 0, 'no run exists when the yes is asked');
    assert.match(s.questions[0].question, /yes/);
    assert.match(s.questions[0].question, /v1\.0\.0/);
    assert.match(s.questions[0].question, new RegExp(commitA.slice(0, 7)));
  });

  test('lets the user fix an uncommitted working tree, and retries the preview on Enter', async () => {
    const s = session({ git: { clean: false }, answers: [(git) => { git.configure({ clean: true }); return ''; }, 'yes', 'yes'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.match(s.text(), /uncommitted/);
    assert.match(s.questions[0].question, /commit|push/i);
    assert.equal(s.questions[0].dispatchesSoFar, 0);
    assert.equal(s.fake.dispatches.length, 1);
  });

  test('guides a missing changelog fix and previews again with the newly committed notes', async () => {
    const s = session({ git: { changelog: changelogWith('0.1.0') }, answers: [(git) => { git.configure({ changelog: changelogWith('1.0.0') }); return ''; }, 'yes', 'yes'] });
    assert.equal((await run([], s.services)).code, 0);
    assert.match(s.text(), /1\.0\.0/);
    assert.equal(s.fake.dispatches[0].inputs.tag, 'v1.0.0');
  });

  for (const answer of ['cancel', 'no', 'y', ' ']) {
    test(`cancels with exit 0 and starts nothing when a refused preview gets ${JSON.stringify(answer)} instead of Enter`, async () => {
      const s = session({ git: { clean: false }, answers: [answer] });
      const result = await run([], s.services);
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(s.fake.dispatches, []);
      assert.equal(s.questions.length, 1);
      assert.match(s.text(), /cancel/i);
    });
  }
});

describe('release command: the one approval (unit)', () => {
  for (const answer of ['no', '', 'y', 'Yes', 'YES', ' yes', 'yes ', 'yes please']) {
    test(`exits 0 with nothing started when the answer is ${JSON.stringify(answer)}`, async () => {
      const s = session({ answers: [answer] });
      const result = await run([], s.services);
      assert.equal(result.code, 0, result.stderr);
      assert.equal(result.stderr, '');
      assert.deepEqual(s.fake.dispatches, []);
      assert.deepEqual(s.downloads, []);
      assert.deepEqual(s.git.snapshot(), nothingTagged);
      assert.equal(s.questions.length, 1);
      assert.match(s.text(), /cancel/i);
    });
  }

  test('starts the run after exactly yes, and asks only that one question before the manual checks', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    assert.equal(s.fake.dispatches.length, 1);
    assert.equal(s.questions.length, 2);
    assert.match(s.questions[1].question, /tray|Task View/i);
  });
});

describe('release command: it only starts a release run (unit)', () => {
  test('never looks for an earlier run, never pushes, and never re-runs a job, on a pass', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    assertOnlyStarted(s);
  });

  for (const failed of ['Plan', 'Tests / full-tests', 'Tag', 'Publish']) {
    test(`never looks for an earlier run, never pushes, and never re-runs a job when ${failed} fails`, async () => {
      const s = session({ run: { conclusion: 'failure' }, fake: { jobs: { 50: jobsOf(failed) } } });
      assert.equal((await run([], s.services)).code, 1);
      assertOnlyStarted(s);
    });
  }

  function assertOnlyStarted(s) {
    const words = s.fake.calls.flat();
    assert.ok(words.every((arg) => !/\/actions\/workflows\/[^/]+\/runs/.test(arg)), 'no run list is read');
    assert.ok(words.every((arg) => !/\/rerun/.test(arg)) && s.fake.calls.every((call) => call[1] !== 'run'), 'no job is re-run');
    assert.equal(s.fake.calls.filter((call) => call.includes('POST')).length, 1, 'exactly one request starts something');
    assert.ok(!s.git.calls.includes('pushTag') && !s.git.calls.includes('createTag'), 'no tag is made or pushed');
    assert.deepEqual(s.git.snapshot(), nothingTagged);
  }
});

describe('release command: starting the run (unit)', () => {
  test('starts release.yml on the default branch with the preview\'s part, tag, and commit', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    assert.deepEqual(s.fake.dispatches, [{ workflow: 'release.yml', ref: 'main', inputs: { part: 'minor', tag: 'v1.0.0', commit: commitA } }]);
  });

  test('starts the run on whatever the default branch is called', async () => {
    const s = session({ git: { defaultBranch: 'trunk', branch: 'trunk' }, run: { head_branch: 'trunk' } });
    assert.equal((await run([], s.services)).code, 0);
    assert.equal(s.fake.dispatches[0].ref, 'trunk');
  });

  test('passes the part that was previewed, with the tag and commit that part gave', async () => {
    const s = session({ git: { local: { 'v1.2.3': commitA }, remote: { 'v1.2.3': commitA }, changelog: changelogWith('2.0.0') } });
    assert.equal((await run(['major'], s.services)).code, 0);
    assert.deepEqual(s.fake.dispatches[0].inputs, { part: 'major', tag: 'v2.0.0', commit: commitA });
  });

  test('links the run GitHub reports, read from the run ID the dispatch returned', async () => {
    const s = session({ fake: { runs: [releaseRun({ id: 51 })], jobs: { 51: jobsOf() }, dispatch: { workflow_run_id: 51 } } });
    assert.equal((await run([], s.services)).code, 0);
    assert.ok(s.text().includes(runUrl(51)));
  });

  test('stops, naming the moved default branch and linking the run, when the started run is on another commit', async () => {
    const s = session({ run: { head_sha: commitB } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /moved/);
    assert.ok(result.stderr.includes(runUrl(50)), 'links the run');
    assert.ok(result.stderr.includes(commitA) && result.stderr.includes(commitB), 'names both commits');
    assert.equal(s.questions.length, 1, 'no manual-check question');
    assert.deepEqual(s.downloads, []);
    assert.equal(s.fake.dispatches.length, 1, 'starts no second run');
    const reads = s.fake.calls.filter((call) => call.some((arg) => /\/actions\/runs\/50$/.test(arg)));
    assert.equal(reads.length, 1, 'it reads the new run once and does not follow it');
  });

  test('announces each wait on GitHub, apart from the questions, before it starts', async () => {
    const s = session();
    assert.equal((await run([], s.services)).code, 0);
    const at = (matches) => s.timeline.findIndex((entry) => entry.call && matches(entry.call));
    const waitBefore = (index, after) => s.timeline.slice(after + 1, index).some((entry) => 'wait' in entry);
    const signIn = at((call) => call[1] === 'auth');
    const dispatch = at((call) => call.includes('POST'));
    assert.ok(waitBefore(signIn, -1), 'announces the sign-in check');
    assert.ok(waitBefore(dispatch, signIn), 'announces the start of the run');
    const followRead = s.timeline.findIndex((entry, index) => index > dispatch + 1 && entry.call?.some((arg) => /\/actions\/runs\/50$/.test(arg)));
    if (followRead > 0) assert.ok(waitBefore(followRead, dispatch + 1), 'announces following the run');
    assert.ok(s.questions.every((entry) => !s.output.includes(entry.question)), 'a question is not a wait message');
  });
});

describe('release command: a run that fails (unit)', () => {
  const failing = (failed, ...skipped) => ({ run: { conclusion: 'failure' }, fake: { jobs: { 50: jobsOf(failed, ...skipped) } } });

  test('lists each failed job with its link, and no passed job', async () => {
    const s = session({ run: { conclusion: 'failure' }, fake: { jobs: { 50: [restJob('Plan', 'success', 1, 101), restJob('Tests / full-tests', 'failure', 1, 102), restJob('Tag', 'skipped', 1, 103), restJob('Publish', 'cancelled', 1, 104)] } } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    for (const id of [102, 104]) assert.ok(result.stderr.includes(jobUrl(id)), `links job ${id}`);
    for (const id of [101, 103]) assert.ok(!result.stderr.includes(jobUrl(id)), `leaves out job ${id}`);
    assert.ok(result.stderr.includes('Tests / full-tests'));
    assert.deepEqual(s.downloads, []);
  });

  test('links the failed run', async () => {
    const s = session(failing('Plan', 'Tests / full-tests', 'Tag', 'Publish'));
    const result = await run([], s.services);
    assert.ok(result.stderr.includes(runUrl(50)) || s.text().includes(runUrl(50)));
  });

  test('says the tag exists on origin when it does, and that it does not when it does not', async () => {
    const withTag = session({ ...failing('Publish'), answers: [(git) => { git.setRemoteTag('v1.0.0', commitA); return 'yes'; }] });
    const present = await run([], withTag.services);
    assert.match(present.stderr, /v1\.0\.0 exists on origin/);
    assert.doesNotMatch(present.stderr, /does not exist on origin/);

    const withoutTag = session(failing('Tag'));
    const absent = await run([], withoutTag.services);
    assert.match(absent.stderr, /v1\.0\.0 does not exist on origin/);
  });

  // The rows are the ones of the failure table in docs/architecture/releasing.md.
  for (const [failed, skipped, row, notRow] of [
    ['Plan', ['Tests / full-tests', 'Tag', 'Publish'], /nothing published/i, /no new tag|re-run/i],
    ['Tests / full-tests', ['Tag', 'Publish'], /nothing published/i, /no new tag|re-run/i],
    ['Tag', ['Publish'], /no new tag/i, /nothing published|re-run/i],
    ['Publish', [], /re-run the failed jobs/i, /nothing published|no new tag/i],
  ]) {
    test(`prints the failure-table row for a failure in ${failed}`, async () => {
      const s = session(failing(failed, ...skipped));
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, row);
      assert.doesNotMatch(result.stderr, notRow);
    });
  }

  test('tells a Plan or Tests failure to fix the problem, commit and push, then start a new release', async () => {
    const s = session(failing('Plan', 'Tests / full-tests', 'Tag', 'Publish'));
    assert.match((await run([], s.services)).stderr, /commit and push[\s\S]*new release/i);
  });

  test('tells a Publish failure to delete any draft, then re-run the failed jobs on GitHub, or clear the tag, and re-runs nothing itself', async () => {
    const s = session(failing('Publish'));
    const result = await run([], s.services);
    assert.match(result.stderr, /delete[^.]*draft[\s\S]*re-run the failed jobs/i, 'a draft left by a partial upload is deleted before the re-run');
    assert.match(result.stderr, /node \.agents\/version-tag\.mjs --clear v1\.0\.0(?![\w.])/, 'names the failed release\'s tag, not the latest one');
    assert.ok(s.fake.calls.every((call) => !call.includes('rerun') && call[1] !== 'run'));
  });

  test('leaves the unfinished run alone, and prints its link, when the wait on it expires', async () => {
    const s = session({ run: { status: 'queued', conclusion: null }, fake: { afterReads: { 50: [{ status: 'in_progress', conclusion: null }] } } });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.ok(result.stderr.includes(runUrl(50)));
    assert.deepEqual(s.downloads, []);
  });

  test('prints the run link, saying the run was started, when reading the run after the dispatch fails', async () => {
    const s = session();
    s.services.github = createGitHubRuns(async (file, args, settings) => {
      if (args[0] === 'api' && !args.includes('POST') && args.some((arg) => /\/actions\/runs\/50$/.test(arg))) throw new Error('gh api failed: HTTP 502');
      return s.fake.execute(file, args, settings);
    }, { clock: s.fake.clock });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.ok(result.stderr.includes(runUrl(50)), result.stderr);
    assert.match(result.stderr, /started|goes on/i);
    assert.equal(s.fake.dispatches.length, 1);
  });

  test('prints the link of the run it started when Ctrl+C stops it', async () => {
    const s = session();
    s.services.github.follow = async () => { throw Object.assign(new Error('The operation was aborted'), { name: 'AbortError' }); };
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.ok(result.stderr.includes(runUrl(50)), result.stderr);
    assert.match(result.stderr, /run/i);
    assert.equal(s.fake.dispatches.length, 1);
  });
});

describe('release command: a run that passes (unit)', () => {
  test('downloads the installer of the published tag, says where it is, warns it is unsigned, and never launches it', async () => {
    const s = session();
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(s.downloads, [['v1.0.0', 'MemoriaToDo-win-Setup.exe']]);
    assert.ok(s.text().includes(installerPath));
    assert.match(s.text(), /unsigned/i);
    assert.match(s.text(), /never launch/i);
    assert.ok(s.text().includes(published.url));
  });

  test('guides the manual checks and exits 0 once they are confirmed with yes', async () => {
    const s = session();
    const result = await run([], s.services);
    assert.equal(result.code, 0, result.stderr);
    assert.match(s.questions.at(-1).question, /1\.0\.0/);
    assert.match(s.questions.at(-1).question, /tray|Task View/i);
    assert.match(s.text(), /confirmed/i);
  });

  for (const answer of ['no', '', 'y']) {
    test(`exits 1, saying the release is already published, when the manual checks get ${JSON.stringify(answer)}`, async () => {
      const s = session({ answers: ['yes', answer] });
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, /already published|already public/i);
      assert.match(result.stderr, /manual|install/i);
    });
  }

  for (const [label, changes] of [
    ['another tag', { tagName: 'v1.0.1' }],
    ['a draft', { isDraft: true }],
    ['a prerelease', { isPrerelease: true }],
    ['no installer', { assets: [] }],
    ['an empty installer', { assets: [{ name: 'App-Setup.exe', size: 0 }] }],
    ['two installers', { assets: [{ name: 'One-Setup.exe', size: 1 }, { name: 'Two-Setup.exe', size: 1 }] }],
    ['only a file that is not a setup executable', { assets: [{ name: 'MemoriaToDo-win-full.nupkg', size: 5 }] }],
  ]) {
    test(`exits 1 and downloads nothing when the release has ${label}`, async () => {
      const s = session({ release: { ...published, ...changes }, answers: ['yes'] });
      const result = await run([], s.services);
      assert.equal(result.code, 1);
      assert.match(result.stderr, /release|installer|tag/i);
      assert.deepEqual(s.downloads, []);
    });
  }

  test('accepts the release when the setup executable is beside other assets', async () => {
    const assets = [...published.assets, { name: 'RELEASES', size: 10 }, { name: 'MemoriaToDo-win-full.nupkg', size: 5 }];
    const s = session({ release: { ...published, assets } });
    assert.equal((await run([], s.services)).code, 0);
    assert.deepEqual(s.downloads, [['v1.0.0', 'MemoriaToDo-win-Setup.exe']]);
  });

  test('exits 1 with the release link when the release is missing', async () => {
    const s = session({ services: { releases: { view: async () => { throw new Error('release not found'); }, download: async () => assert.fail('nothing to download') } }, answers: ['yes'] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /release not found/);
  });

  test('exits 1 with the failure when the download fails', async () => {
    const s = session({ services: { releases: { view: async () => published, download: async () => { throw new Error('download failed'); } } }, answers: ['yes'] });
    const result = await run([], s.services);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /download failed/);
  });
});

describe('release command: reading and downloading the release (unit)', () => {
  test('asks GitHub for the tag\'s release in the origin repository', async () => {
    const fake = createFakeGitHub({ release: published });
    const releases = release.createReleases(fake.execute, repositoryName);
    assert.deepEqual(await releases.view('v1.0.0'), published);
    const call = fake.calls.find((candidate) => candidate[1] === 'release');
    assert.deepEqual(call.slice(1, 3), ['release', 'view']);
    assert.equal(call[3], 'v1.0.0');
    assert.equal(call[call.indexOf('--repo') + 1], `github.com/${repositoryName}`);
  });

  test('rejects when GitHub has no release for the tag', async () => {
    const releases = release.createReleases(createFakeGitHub().execute, repositoryName);
    await assert.rejects(() => releases.view('v1.0.0'), /release/i);
  });
});

describe('release command process and files (integration)', () => {
  test('shows help without contacting GitHub', () => {
    const help = spawnSync(process.execPath, [join(agents, 'release.mjs'), '--help'], { encoding: 'utf8', timeout: 10_000 });
    assert.equal(help.status, 0, help.stderr);
    assert.match(help.stdout, /release\.mjs/);
  });

  test('requires an interactive terminal: redirected input fails without contacting GitHub', () => {
    const result = spawnSync(process.execPath, [join(agents, 'release.mjs')], { encoding: 'utf8', timeout: 10_000, env: { ...process.env, PATH: '' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /interactive terminal/i);
    assert.doesNotMatch(result.stderr, /ENOENT/);
  });

  test('downloads the named installer into a new temporary folder each time and checks that the file exists', async (t) => {
    const fake = createFakeGitHub({ installer: 'installer bytes' });
    const releases = release.createReleases(fake.execute, repositoryName);
    const first = await releases.download('v1.0.0', 'MemoriaToDo-win-Setup.exe');
    const second = await releases.download('v1.0.0', 'MemoriaToDo-win-Setup.exe');
    for (const path of [first, second]) t.after(() => rm(dirname(path), { recursive: true, force: true }));
    assert.notEqual(dirname(first), dirname(second));
    assert.ok(first.startsWith(tmpdir()), first);
    assert.equal(await readFile(first, 'utf8'), 'installer bytes');
    const call = fake.calls.find((candidate) => candidate[2] === 'download');
    assert.equal(call[call.indexOf('--repo') + 1], `github.com/${repositoryName}`);
    assert.equal(call[call.indexOf('--pattern') + 1], 'MemoriaToDo-win-Setup.exe');
    assert.equal(call[call.indexOf('--dir') + 1], dirname(first));
  });

  test('refuses an installer name that is a path, and an empty download', async () => {
    await assert.rejects(() => release.createReleases(createFakeGitHub().execute, repositoryName).download('v1.0.0', '..\\evil.exe'), /name|path/i);
    await assert.rejects(() => release.createReleases(createFakeGitHub({ installer: '' }).execute, repositoryName).download('v1.0.0', 'Empty-Setup.exe'), /empty|missing/i);
  });
});

// ---------- end to end: the command as a child process ----------
describe('release command end to end (integration)', () => {
  test('previews, asks yes once, starts the release run, follows it, and downloads the installer', { timeout: 120_000 }, async (t) => {
    const repo = repositoryWithTooling(t);
    const fakeOptions = {
      runs: [releaseRun({ head_sha: repo.head })], jobs: { 50: jobsOf() }, dispatch: { workflow_run_id: 50 },
      release: published, installer: 'installer bytes',
    };
    const result = await runCommand(repo.work, repo.script('release.mjs'), ['minor'], { tty: true, ...fakeOptions }, ['yes', 'yes']);
    assert.equal(result.code, 0, `${result.stdout}\n${result.stderr}`);
    assert.equal(result.asked, 2);
    assert.match(result.stdout, /v1\.0\.0/);
    assert.ok(result.stdout.includes(runUrl(50)));

    assert.deepEqual(result.record().dispatches, [{ workflow: 'release.yml', ref: 'main', inputs: { part: 'minor', tag: 'v1.0.0', commit: repo.head } }]);
    assert.equal(repo.git(repo.origin, 'tag', '--list'), '', 'the command never pushes a tag');

    const path = /([A-Za-z]:\\[^\r\n]*Setup\.exe|\/[^\r\n]*Setup\.exe)/.exec(result.stdout)?.[1];
    assert.ok(path, `the installer's path is printed: ${result.stdout}`);
    t.after(() => rm(dirname(path), { recursive: true, force: true }));
    assert.equal(await readFile(path, 'utf8'), 'installer bytes');
    assert.ok(!path.startsWith(repo.root), 'the download is in its own temporary folder');
  });
});
