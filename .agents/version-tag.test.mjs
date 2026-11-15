// Tests for version-tag.mjs. Unit tests run `run(args, git)` against the in-memory fake in version-tag.fake-git.mjs.
// Integration tests run the script as a real process on temporary git repositories whose origin is a local bare repository.
//
// Contract the unit tests rely on, besides the git port described in version-tag.fake-git.mjs:
//   export async function run(args: string[], git): Promise<{ code: 0 | 1, stdout: string, stderr: string }>
//   Importing the module runs nothing; the script runs only when node starts it directly.
//   Each output line ends with "\n". A refusal prints exactly one stderr line, and nothing on stdout.
//   A refusal message names: the default branch (wrong branch), "uncommitted" (dirty tree), "behind" and/or "ahead"
//   (not in step; a diverged branch names both), each offending tag (tags missing on or different on origin).
//   A preview or clear plan names where a tag is with the words "here" and/or "origin".
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { run } from './version-tag.mjs';
import { changelogWith, commitA, commitB, commitC, createFakeFiles, createFakeGit, validChangelog } from './version-tag.fake-git.mjs';

const script = join(dirname(fileURLToPath(import.meta.url)), 'version-tag.mjs');

// ---------- unit helpers ----------

// `workingChangelog` in the options is the CHANGELOG.md as checked out, which --notes reads; the fake git holds the committed one.
async function runWith(args, options) {
  const git = createFakeGit(options);
  const result = await run(args, git, createFakeFiles({ changelog: options?.workingChangelog }));
  return { git, result };
}

const lines = (text) => text.split('\n').filter((line) => line !== '');

function assertRefusal(result, message) {
  assert.equal(result.code, 1, result.stderr);
  assert.equal(result.stdout, '');
  assert.equal(lines(result.stderr).length, 1, `one error line, got: ${result.stderr}`);
  assert.ok(result.stderr.endsWith('\n'));
  if (message) assert.match(result.stderr, message);
}

const onBoth = (commit) => ({ local: { ...commit }, remote: { ...commit } });
const secondCommit = { c2: { shortId: 'def5678', subject: 'Fix the build' } };

describe('version tag rule through --check (unit)', () => {
  for (const [tag, version] of [['v1.2.3', '1.2.3'], ['v0.0.0', '0.0.0'], ['v0.10.0', '0.10.0'], ['v10.20.30', '10.20.30']]) {
    test(`prints only ${version} on stdout, nothing on stderr, and succeeds for ${tag}`, async () => {
      const { result } = await runWith(['--check', tag]);
      assert.deepEqual(result, { code: 0, stdout: `${version}\n`, stderr: '' });
    });
  }

  for (const tag of ['v1.2', 'proof-1', 'v01.2.3', 'v1.02.3', 'v1.2.03', 'v1.2.3-rc1', 'V1.2.3', '1.2.3', 'v1.2.3.4', 'v1.2.x', 'v', '', ' v1.2.3', 'v-1.2.3']) {
    test(`refuses "${tag}" with one error line naming the tag and the form v<major>.<minor>.<patch>`, async () => {
      const { result } = await runWith(['--check', tag]);
      assertRefusal(result);
      assert.ok(result.stderr.includes(tag));
      assert.match(result.stderr, /v<major>\.<minor>\.<patch>/);
    });
  }

  test('runs no git operation', async () => {
    for (const tag of ['v1.2.3', 'proof-1']) {
      const { git } = await runWith(['--check', tag]);
      assert.deepEqual(git.calls, []);
    }
  });
});

describe('command line arguments (unit)', () => {
  const wrong = [
    ['an unknown option', ['--bogus']],
    ['an unknown word', ['everything']],
    ['two parts', ['major', 'minor']],
    ['a repeated part', ['patch', 'patch']],
    ['a part with --clear', ['major', '--clear']],
    ['--clear with a part after a name', ['--clear', 'v1.0.0', 'minor']],
    ['two names after --clear', ['--clear', 'v1.0.0', 'v1.0.1']],
    ['a repeated --clear', ['--clear', '--clear']],
    ['a repeated --dry-run', ['--dry-run', '--dry-run']],
    ['--dry-run with --check', ['--dry-run', '--check', 'v1.0.0']],
    ['--check with --clear', ['--check', '--clear']],
    ['--check without a tag', ['--check']],
    ['--check with two tags', ['--check', 'v1.0.0', 'v1.0.1']],
    ['--check with a part', ['--check', 'v1.0.0', 'major']],
    ['--check twice', ['--check', 'v1.0.0', '--check']],
  ];
  for (const [what, args] of wrong) {
    test(`fails before any git operation for ${what}`, async () => {
      const { git, result } = await runWith(args, onBoth({ 'v1.0.0': 'c1' }));
      assertRefusal(result);
      assert.deepEqual(git.calls, []);
    });
  }

  for (const args of [['--dry-run', 'major'], ['major', '--dry-run'], ['--clear', 'v1.0.0', '--dry-run'], ['--dry-run', '--clear'], ['--clear', '--dry-run', 'v1.0.0']]) {
    test(`accepts ${args.join(' ')}`, async () => {
      const { result } = await runWith(args, onBoth({ 'v1.0.0': 'c1' }));
      assert.equal(result.code, 0, result.stderr);
    });
  }
});

describe('making the next version tag (unit)', () => {
  test('raises the minor part when no part is named', async () => {
    const { git, result } = await runWith([], onBoth({ 'v1.2.3': 'c1' }));
    assert.equal(result.code, 0, result.stderr);
    assert.ok(Object.hasOwn(git.snapshot().remote, 'v1.3.0'));
  });

  for (const [part, expected] of [['major', 'v2.0.0'], ['minor', 'v1.3.0'], ['patch', 'v1.2.4']]) {
    test(`${part} raises that part of v1.2.3 to ${expected} and resets the lower ones`, async () => {
      const { git, result } = await runWith([part], onBoth({ 'v1.2.3': 'c1' }));
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(Object.keys(git.snapshot().remote).sort(), ['v1.2.3', expected].sort());
      assert.deepEqual(Object.keys(git.snapshot().local).sort(), ['v1.2.3', expected].sort());
    });
  }

  for (const part of [undefined, 'major', 'minor', 'patch']) {
    test(`makes v0.1.0 for ${part ?? 'no part'} when there is no version tag yet`, async () => {
      const { git, result } = await runWith(part ? [part] : []);
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(Object.keys(git.snapshot().remote), ['v0.1.0']);
    });
  }

  test('ignores tags that are not version tags when choosing the latest', async () => {
    const others = Object.fromEntries(['v1.2', 'proof-1', 'v01.2.3', 'v1.2.3-rc1', 'V1.2.3', '1.2.3'].map((tag) => [tag, 'c1']));
    const { git, result } = await runWith(['patch'], { local: others, remote: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.ok(Object.hasOwn(git.snapshot().remote, 'v0.1.0'));
  });

  test('is not blocked by a local tag that is not a version tag and is missing on origin', async () => {
    const { result } = await runWith([], { local: { 'proof-1': 'c1' }, remote: {} });
    assert.equal(result.code, 0, result.stderr);
  });

  test('picks the latest by number, so v0.10.0 comes after v0.9.0', async () => {
    const { git } = await runWith(['patch'], onBoth({ 'v0.9.0': 'c1', 'v0.10.0': 'c1' }));
    assert.ok(Object.hasOwn(git.snapshot().remote, 'v0.10.1'));
  });

  test('picks v1.0.0 over v0.99.99', async () => {
    const { git } = await runWith(['patch'], onBoth({ 'v0.99.99': 'c1', 'v1.0.0': 'c1' }));
    assert.ok(Object.hasOwn(git.snapshot().remote, 'v1.0.1'));
  });

  test('counts a version tag that exists only on origin as the latest, without making it here', async () => {
    const { git, result } = await runWith(['patch'], { local: {}, remote: { 'v2.4.0': 'c1' } });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(Object.keys(git.snapshot().local), ['v2.4.1']);
    assert.deepEqual(Object.keys(git.snapshot().remote).sort(), ['v2.4.0', 'v2.4.1']);
  });

  test('puts the tag on the newest commit of origin\'s default branch', async () => {
    const { git } = await runWith([], { originHead: 'c2', commits: { c2: { shortId: 'def5678', subject: 'Fix the build' } } });
    assert.deepEqual(git.snapshot(), { local: { 'v0.1.0': 'c2' }, remote: { 'v0.1.0': 'c2' } });
  });

  test('makes an annotated tag', async () => {
    const { git } = await runWith([]);
    assert.equal(git.isAnnotated('v0.1.0'), true);
  });

  test('pushes the tag, prints what it did, and succeeds', async () => {
    const { git, result } = await runWith(['major'], onBoth({ 'v1.2.3': 'c1' }));
    assert.equal(result.code, 0);
    assert.equal(result.stderr, '');
    assert.match(result.stdout, /v2\.0\.0/);
    assert.equal(git.snapshot().remote['v2.0.0'], 'c1');
  });

  test('deletes the tag it made, shows git\'s message, and fails when the push fails', async () => {
    const { git, result } = await runWith([], { refusePush: 'remote: refused by the server', ...onBoth({ 'v1.0.0': 'c1' }) });
    assert.equal(result.code, 1);
    assert.match(result.stderr, /remote: refused by the server/);
    assert.deepEqual(git.snapshot(), { local: { 'v1.0.0': 'c1' }, remote: { 'v1.0.0': 'c1' } });
  });
});

describe('checks before making a tag (unit)', () => {
  const unchanged = (git, before) => assert.deepEqual(git.snapshot(), before);

  for (const mode of [[], ['--dry-run']]) {
    const how = mode.length ? 'a preview' : 'a real run';
    const refuses = (name, options, message) => test(`${how} refuses ${name}, changing no tag`, async () => {
      const git = createFakeGit(options);
      const before = git.snapshot();
      const result = await run(mode, git);
      assertRefusal(result, message);
      unchanged(git, before);
    });

    refuses('when not on the default branch', { branch: 'feature' }, /main/);
    refuses('when HEAD is detached', { branch: undefined }, /branch/i);
    refuses('when the working tree has changes', { clean: false }, /uncommitted/i);
    refuses('when the branch is behind origin', { behind: 2 }, /behind/);
    refuses('when the branch is ahead of origin', { ahead: 1 }, /ahead/);
    refuses('when the branch has diverged from origin', { ahead: 1, behind: 1 }, /(ahead.*behind|behind.*ahead)/);
    refuses('when the origin default branch is main but trunk is checked out', { defaultBranch: 'main', branch: 'trunk' }, /main/);
    refuses('when a local version tag is missing on origin', { local: { 'v1.0.0': 'c1' }, remote: {} }, /v1\.0\.0/);
    refuses(
      'when a local version tag points to another commit on origin',
      { local: { 'v1.0.0': 'c1' }, remote: { 'v1.0.0': 'c2' }, commits: { c1: { shortId: 'abc1234', subject: 'a' }, ...secondCommit } },
      /v1\.0\.0/,
    );
    refuses('and names every offending tag', { local: { 'v1.0.0': 'c1', 'v1.1.0': 'c1', 'v1.2.0': 'c1' }, remote: { 'v1.1.0': 'c2', 'v1.2.0': 'c1' } }, /v1\.0\.0.*v1\.1\.0|v1\.1\.0.*v1\.0\.0/);

    test(`${how} uses origin's default branch name instead of main`, async () => {
      const { result } = await runWith(mode, { defaultBranch: 'trunk', branch: 'trunk' });
      assert.equal(result.code, 0, result.stderr);
    });

    test(`${how} fetches before checking, so a commit new on origin makes it refuse as behind`, async () => {
      const { result, git } = await runWith(mode, { afterFetch: { behind: 1 } });
      assertRefusal(result, /behind/);
      assert.equal(git.fetched, true);
    });

    test(`${how} shows git's message and fails when the fetch fails`, async () => {
      const { result, git } = await runWith(mode, { failFetch: 'fatal: unable to access origin' });
      assert.equal(result.code, 1);
      assert.match(result.stderr, /fatal: unable to access origin/);
      assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
    });
  }
});

describe('clearing a version tag (unit)', () => {
  test('deletes the latest version tag here and on origin', async () => {
    const { git, result } = await runWith(['--clear'], onBoth({ 'v1.0.0': 'c1', 'v1.2.0': 'c1', 'v1.10.0': 'c1' }));
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: { 'v1.0.0': 'c1', 'v1.2.0': 'c1' }, remote: { 'v1.0.0': 'c1', 'v1.2.0': 'c1' } });
  });

  test('deletes the named version tag here and on origin and prints what it did', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], onBoth({ 'v1.0.0': 'c1', 'v1.2.0': 'c1' }));
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stderr, '');
    assert.match(result.stdout, /v1\.0\.0/);
    assert.deepEqual(git.snapshot(), { local: { 'v1.2.0': 'c1' }, remote: { 'v1.2.0': 'c1' } });
  });

  test('deletes a tag that is only here', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': 'c1' }, remote: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('deletes a tag that is only on origin', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: {}, remote: { 'v1.0.0': 'c1' } });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('deletes a tag from both places when they point to different commits', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': 'c1' }, remote: { 'v1.0.0': 'c2' }, commits: { c1: { shortId: 'abc1234', subject: 'a' }, ...secondCommit } });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('counts tags from both places when choosing the latest to clear', async () => {
    const { git } = await runWith(['--clear'], { local: { 'v1.0.0': 'c1', 'v3.0.0': 'c1' }, remote: { 'v1.0.0': 'c1', 'v2.0.0': 'c1' } });
    assert.deepEqual(git.snapshot(), { local: { 'v1.0.0': 'c1' }, remote: { 'v1.0.0': 'c1', 'v2.0.0': 'c1' } });
  });

  test('leaves other tags alone, including tags that are not version tags', async () => {
    const { git } = await runWith(['--clear'], onBoth({ 'v1.0.0': 'c1', 'proof-1': 'c1', 'v9.9': 'c1' }));
    assert.deepEqual(git.snapshot(), onBoth({ 'proof-1': 'c1', 'v9.9': 'c1' }));
  });

  test('deletes on origin first, so when origin refuses the tag here stays', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { refuseRemoteDelete: 'remote: tag is protected', ...onBoth({ 'v1.0.0': 'c1' }) });
    assert.equal(result.code, 1);
    assert.match(result.stderr, /remote: tag is protected/);
    assert.deepEqual(git.snapshot(), onBoth({ 'v1.0.0': 'c1' }));
  });

  test('checks nothing about the branch or the working tree', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { branch: undefined, clean: false, behind: 3, ahead: 2, ...onBoth({ 'v1.0.0': 'c1' }) });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  for (const [what, args, options] of [
    ['a name that is not a version tag', ['--clear', 'proof-1'], onBoth({ 'proof-1': 'c1', 'v1.0.0': 'c1' })],
    ['a name that nearly looks like a version tag', ['--clear', 'v1.2'], onBoth({ 'v1.0.0': 'c1' })],
    ['a version tag that is in neither place', ['--clear', 'v7.0.0'], onBoth({ 'v1.0.0': 'c1' })],
    ['--clear alone when there is no version tag', ['--clear'], onBoth({ 'proof-1': 'c1' })],
  ]) {
    test(`refuses ${what}, changing no tag`, async () => {
      const git = createFakeGit(options);
      const before = git.snapshot();
      const result = await run(args, git);
      assertRefusal(result);
      assert.deepEqual(git.snapshot(), before);
    });
  }

  test('names the tag it could not find', async () => {
    const { result } = await runWith(['--clear', 'v7.0.0']);
    assert.match(result.stderr, /v7\.0\.0/);
  });
});

describe('a preview with --dry-run (unit)', () => {
  test('prints the new tag with its commit\'s short id and subject, fetches, and changes no tag', async () => {
    const git = createFakeGit({ ...onBoth({ 'v1.2.3': 'c1' }), originHead: 'c2', commits: { c1: { shortId: 'abc1234', subject: 'Old' }, ...secondCommit } });
    const before = git.snapshot();
    const result = await run(['--dry-run'], git);
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stderr, '');
    assert.match(result.stdout, /v1\.3\.0/);
    assert.match(result.stdout, /def5678/);
    assert.match(result.stdout, /Fix the build/);
    assert.equal(git.fetched, true);
    assert.deepEqual(git.snapshot(), before);
  });

  test('previews the part it was given', async () => {
    const { result } = await runWith(['major', '--dry-run'], onBoth({ 'v1.2.3': 'c1' }));
    assert.match(result.stdout, /v2\.0\.0/);
  });

  test('prints the tag to clear, its commit, and that it is here and on origin', async () => {
    const { git, result } = await runWith(['--clear', '--dry-run'], onBoth({ 'v1.2.3': 'c1' }));
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /v1\.2\.3/);
    assert.match(result.stdout, /abc1234/);
    assert.match(result.stdout, /\bhere\b/);
    assert.match(result.stdout, /origin/);
    assert.equal(git.fetched, true);
    assert.deepEqual(git.snapshot(), onBoth({ 'v1.2.3': 'c1' }));
  });

  test('says a tag to clear is only here', async () => {
    const { result } = await runWith(['--clear', 'v1.2.3', '--dry-run'], { local: { 'v1.2.3': 'c1' }, remote: {} });
    assert.match(result.stdout, /\bhere\b/);
    assert.doesNotMatch(result.stdout, /origin/);
  });

  test('says a tag to clear is only on origin', async () => {
    const { result } = await runWith(['--clear', 'v1.2.3', '--dry-run'], { local: {}, remote: { 'v1.2.3': 'c1' } });
    assert.match(result.stdout, /origin/);
    assert.doesNotMatch(result.stdout, /\bhere\b/);
  });

  test('shows both commits when the tag to clear differs between here and origin', async () => {
    const { result } = await runWith(['--clear', 'v1.2.3', '--dry-run'], { local: { 'v1.2.3': 'c1' }, remote: { 'v1.2.3': 'c2' }, commits: { c1: { shortId: 'abc1234', subject: 'a' }, ...secondCommit } });
    assert.match(result.stdout, /abc1234/);
    assert.match(result.stdout, /def5678/);
  });

  test('refuses clearing a tag that does not exist, like a real run', async () => {
    const { git, result } = await runWith(['--clear', 'v7.0.0', '--dry-run'], onBoth({ 'v1.0.0': 'c1' }));
    assertRefusal(result, /v7\.0\.0/);
    assert.deepEqual(git.snapshot(), onBoth({ 'v1.0.0': 'c1' }));
  });

  test('previewing does not make a tag that only exists on origin appear here', async () => {
    const { git } = await runWith(['--dry-run'], { local: {}, remote: { 'v0.5.0': 'c1' } });
    assert.deepEqual(git.snapshot(), { local: {}, remote: { 'v0.5.0': 'c1' } });
  });
});

// ---------- more unit tests: exact numbers, git settings, approval, the guarded delete ----------

describe('version parts of any size (unit)', () => {
  const big = '9007199254740993'; // 2^53 + 1, which a floating-point number can't hold
  // The committed changelog has the section of the one tag expected to be made.
  const tagsAt = async (args, tag, made) => {
    const { git, result } = await runWith(args, { ...onBoth({ [tag]: 'c1' }), changelog: changelogWith(made.slice(1)) });
    assert.equal(result.code, 0, result.stderr);
    return Object.keys(git.snapshot().remote).filter((name) => name !== tag);
  };

  test('raises the minor part of a major part above 2^53 without rounding it', async () => {
    assert.deepEqual(await tagsAt(['minor'], `v${big}.0.0`, `v${big}.1.0`), [`v${big}.1.0`]);
  });

  test('raises the patch part above 2^53 by exactly one', async () => {
    assert.deepEqual(await tagsAt(['patch'], `v1.2.${big}`, 'v1.2.9007199254740994'), [`v1.2.9007199254740994`]);
  });

  test('raises the major part above 2^53 by exactly one', async () => {
    assert.deepEqual(await tagsAt(['major'], `v${big}.0.0`, 'v9007199254740994.0.0'), ['v9007199254740994.0.0']);
  });

  test('raises a part of more than 20 digits', async () => {
    assert.deepEqual(await tagsAt(['patch'], 'v1.0.123456789012345678901234567890', 'v1.0.123456789012345678901234567891'), ['v1.0.123456789012345678901234567891']);
  });

  test('tells apart two versions that differ only beyond 2^53', async () => {
    const { git } = await runWith(['patch'], { ...onBoth({ 'v9007199254740992.0.0': 'c1', [`v${big}.0.0`]: 'c1' }), changelog: changelogWith(`${big}.0.1`) });
    assert.ok(Object.hasOwn(git.snapshot().remote, `v${big}.0.1`));
  });

  test('--check prints every digit of a part above 2^53', async () => {
    const { result } = await runWith(['--check', `v${big}.18446744073709551617.0`]);
    assert.deepEqual(result, { code: 0, stdout: `${big}.18446744073709551617.0\n`, stderr: '' });
  });
});

describe('fetching only origin\'s default branch (unit)', () => {
  const trunk = { defaultBranch: 'trunk', branch: 'trunk' };
  for (const [what, args] of [['a make', []], ['a make preview', ['--dry-run']], ['a clear', ['--clear', 'v1.0.0']], ['a clear preview', ['--clear', 'v1.0.0', '--dry-run']]]) {
    test(`${what} asks origin for its default branch first, then fetches that branch alone`, async () => {
      const { git } = await runWith(args, { ...trunk, ...onBoth({ 'v1.0.0': 'c1' }) });
      assert.deepEqual(git.fetchedBranches, ['trunk']);
      assert.ok(git.calls.indexOf('defaultBranch') >= 0 && git.calls.indexOf('defaultBranch') < git.calls.indexOf('fetch'), git.calls.join(','));
    });
  }
});

describe('the push that makes a tag (unit)', () => {
  test('deletes the tag it made here, shows git\'s message, and exits 1 when origin got that tag meanwhile', async () => {
    const { git, result } = await runWith([], { afterRemoteTagsRead: (fake) => fake.setRemoteTag('v0.1.0', commitB) });
    assert.equal(result.code, 1);
    assert.match(result.stderr, /rejected/);
    assert.deepEqual(git.snapshot(), { local: {}, remote: { 'v0.1.0': commitB } });
  });

  test('pushes the tag object it made, not a tag moved here to another commit right after', async () => {
    const { git, result } = await runWith([], { afterCreateTag: (fake) => fake.setLocalTag('v0.1.0', commitB) });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot().remote, { 'v0.1.0': 'c1' });
  });

  test('keeps a tag moved here after the script made it, and exits 1, when the push is rejected', async () => {
    const { git, result } = await runWith([], { refusePush: 'remote: refused by the server', afterCreateTag: (fake) => fake.setLocalTag('v0.1.0', commitB) });
    assert.equal(result.code, 1);
    assert.notEqual(result.stderr, '');
    assert.deepEqual(git.snapshot(), { local: { 'v0.1.0': commitB }, remote: {} });
  });
});

describe('the guarded delete on origin when clearing (unit)', () => {
  test('names the annotated tag\'s own id, not its commit, so origin accepts it', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': commitA }, remote: { 'v1.0.0': commitA } });
    assert.notEqual(git.remoteTagId('v1.0.0'), commitA, 'the fake gives an annotated tag an id of its own');
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('names the commit for a lightweight tag, whose own id is its commit\'s', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': commitA }, remote: { 'v1.0.0': { commit: commitA, id: commitA } } });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('reads origin\'s tags once when the delete works, so the guard comes from the listing the checks read', async () => {
    const { git } = await runWith(['--clear', 'v1.0.0'], onBoth({ 'v1.0.0': commitA }));
    assert.equal(git.calls.filter((call) => call === 'remoteTags').length, 1);
  });

  test('deletes on origin, then here', async () => {
    const { git } = await runWith(['--clear', 'v1.0.0'], onBoth({ 'v1.0.0': commitA }));
    assert.ok(git.calls.indexOf('deleteRemoteTag') >= 0);
    assert.ok(git.calls.indexOf('deleteRemoteTag') < git.calls.indexOf('deleteLocalTag'), git.calls.join(','));
  });

  for (const [what, change] of [
    ['pushed again with another commit', (fake) => fake.setRemoteTag('v1.0.0', commitB)],
    ['pushed again with the same commit in a new tag object', (fake) => fake.setRemoteTag('v1.0.0', { commit: commitA, id: commitA })],
    ['deleted from another clone', (fake) => fake.removeRemoteTag('v1.0.0')],
  ]) {
    test(`refuses with one line naming the tag, deleting no tag, when it was ${what} after the check`, async () => {
      const git = createFakeGit({ ...onBoth({ 'v1.0.0': commitA }), afterRemoteTagsRead: change });
      const result = await run(['--clear', 'v1.0.0'], git);
      assertRefusal(result, /v1\.0\.0/);
      assert.match(result.stderr, /changed on origin/);
      assert.match(result.stderr, /preview/i);
      assert.deepEqual(git.snapshot().local, { 'v1.0.0': commitA });
      assert.ok(git.calls.every((call) => call !== 'deleteLocalTag'));
    });
  }

  test('says the tag changed on origin whatever words git used to refuse, such as another language', async () => {
    const git = createFakeGit({
      ...onBoth({ 'v1.0.0': commitA }),
      refuseRemoteDelete: '! [abgelehnt] v1.0.0 (veraltete Informationen)',
      afterRemoteTagsRead: (fake) => fake.setRemoteTag('v1.0.0', commitB),
    });
    const result = await run(['--clear', 'v1.0.0'], git);
    assertRefusal(result, /v1\.0\.0/);
    assert.match(result.stderr, /changed on origin/);
    assert.deepEqual(git.snapshot().local, { 'v1.0.0': commitA });
  });

  test('shows git\'s own message, not the changed-tag message, when origin refuses for another reason', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { refuseRemoteDelete: 'remote: immutable release', ...onBoth({ 'v1.0.0': commitA }) });
    assertRefusal(result, /immutable release/);
    assert.doesNotMatch(result.stderr, /changed on origin/);
    assert.deepEqual(git.snapshot(), onBoth({ 'v1.0.0': commitA }));
  });

  test('shows git\'s message and exits 1 when the delete here fails after origin\'s worked, and a later clear deletes the rest', async () => {
    const git = createFakeGit({ refuseLocalDelete: 'error: cannot lock ref', ...onBoth({ 'v1.0.0': commitA }) });
    const result = await run(['--clear', 'v1.0.0'], git);
    assert.equal(result.code, 1);
    assert.match(result.stderr, /cannot lock ref/);
    assert.deepEqual(git.snapshot(), { local: { 'v1.0.0': commitA }, remote: {} });

    git.configure({ refuseLocalDelete: undefined });
    const again = await run(['--clear', 'v1.0.0'], git);
    assert.equal(again.code, 0, again.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });
});

describe('the guarded delete here when clearing (unit)', () => {
  for (const [what, args] of [['unapproved', ['--clear', 'v1.0.0']], ['approved', ['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA]]]) {
    test(`deletes the annotated tag here by its own id, not its commit, in an ${what} clear`, async () => {
      const { git, result } = await runWith(args, onBoth({ 'v1.0.0': commitA }));
      assert.notEqual(git.localTagId('v1.0.0'), commitA, 'the fake gives an annotated tag an id of its own');
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
    });
  }

  test('deletes a tag that is only here by its own id', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': commitA }, remote: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('deletes a lightweight tag here, whose own id is its commit\'s', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { local: { 'v1.0.0': { commit: commitA, id: commitA } }, remote: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  for (const [what, change, kept] of [
    ['moved to another commit', (fake) => fake.setLocalTag('v1.0.0', commitB), commitB],
    ['made again on the same commit as a new tag object', (fake) => fake.setLocalTag('v1.0.0', { commit: commitA, id: commitB }), commitA],
  ]) {
    test(`keeps the tag here, says it changed here, and exits 1 when it was ${what} after the check`, async () => {
      const git = createFakeGit({ ...onBoth({ 'v1.0.0': commitA }), afterLocalTagsRead: change });
      const result = await run(['--clear', 'v1.0.0'], git);
      assert.equal(result.code, 1);
      assert.equal(result.stdout, '');
      assert.match(result.stderr, /v1\.0\.0/);
      assert.match(result.stderr, /changed here/);
      assert.deepEqual(git.snapshot(), { local: { 'v1.0.0': kept }, remote: {} });

      git.configure({ afterLocalTagsRead: undefined });
      const again = await run(['--clear', 'v1.0.0'], git);
      assert.equal(again.code, 0, again.stderr);
      assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
    });
  }

  test('says the tag changed here, not on origin, when only the tag here changed', async () => {
    const git = createFakeGit({ ...onBoth({ 'v1.0.0': commitA }), afterLocalTagsRead: (fake) => fake.setLocalTag('v1.0.0', commitB) });
    const result = await run(['--clear', 'v1.0.0'], git);
    assert.doesNotMatch(result.stderr, /changed on origin/);
  });

  test('succeeds when the tag here is already gone by the time it is deleted, as when a git setting made the push delete it', async () => {
    const goneHere = async (fake) => fake.deleteLocalTag('v1.0.0', fake.localTagId('v1.0.0'));
    const { git, result } = await runWith(['--clear', 'v1.0.0'], { ...onBoth({ 'v1.0.0': commitA }), afterLocalTagsRead: goneHere });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('shows git\'s own message, not the changed-here message, when the delete here fails for another reason', async () => {
    const { result } = await runWith(['--clear', 'v1.0.0'], { refuseLocalDelete: 'error: cannot lock ref', ...onBoth({ 'v1.0.0': commitA }) });
    assert.match(result.stderr, /cannot lock ref/);
    assert.doesNotMatch(result.stderr, /changed here/);
  });
});

describe('the command a preview ends with (unit)', () => {
  const command = (...words) => ['node', '.agents/version-tag.mjs', ...words].join(' ');
  const lastLine = (result) => lines(result.stdout).at(-1);
  const argsOf = (line) => {
    const words = line.split(' ');
    assert.deepEqual(words.slice(0, 2), ['node', '.agents/version-tag.mjs']);
    return words.slice(2);
  };
  const makeState = { ...onBoth({ 'v1.2.3': commitA }), originHead: commitA };

  for (const [args, tag] of [[[], 'v1.3.0'], [['minor'], 'v1.3.0'], [['major'], 'v2.0.0'], [['patch'], 'v1.2.4']]) {
    test(`for a make preview of ${args[0] ?? 'no part'} ends with the part, the tag, and the full commit id`, async () => {
      const { result } = await runWith([...args, '--dry-run'], makeState);
      assert.equal(result.code, 0, result.stderr);
      assert.equal(lastLine(result), command(args[0] ?? 'minor', '--expect', tag, '--commit', commitA));
      assert.match(result.stdout, /a1a1a1a/);
    });
  }

  test('for a make preview names a 64-character commit id in full', async () => {
    const { result } = await runWith(['--dry-run'], { originHead: commitC, commits: { [commitC]: { shortId: 'c3c3c3c', subject: 'Big id' } } });
    assert.equal(lastLine(result), command('minor', '--expect', 'v0.1.0', '--commit', commitC));
  });

  for (const [what, options, options2, expected] of [
    ['a tag here and on origin on one commit', onBoth({ 'v1.0.0': commitA }), undefined, ['--here', commitA, '--origin', commitA]],
    ['a tag only here', { local: { 'v1.0.0': commitA }, remote: {} }, undefined, ['--here', commitA]],
    ['a tag only on origin', { local: {}, remote: { 'v1.0.0': commitA } }, undefined, ['--origin', commitA]],
    ['a tag on different commits here and on origin', { local: { 'v1.0.0': commitA }, remote: { 'v1.0.0': commitB } }, undefined, ['--here', commitA, '--origin', commitB]],
  ]) {
    test(`for a clear preview of ${what} ends with one option per place`, async () => {
      const { result } = await runWith(['--clear', 'v1.0.0', '--dry-run'], options);
      assert.equal(result.code, 0, result.stderr);
      assert.equal(lastLine(result), command('--clear', 'v1.0.0', ...expected));
    });
  }

  test('for a clear preview without a tag names the latest tag in the command', async () => {
    const { result } = await runWith(['--clear', '--dry-run'], onBoth({ 'v1.0.0': commitA, 'v1.10.0': commitA }));
    assert.equal(lastLine(result), command('--clear', 'v1.10.0', '--here', commitA, '--origin', commitA));
  });

  test('says a clear\'s commit that only origin has is not here, with its full id and no subject, and succeeds', async () => {
    const { result } = await runWith(['--clear', 'v1.0.0', '--dry-run'], { local: {}, remote: { 'v1.0.0': commitC }, commits: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.equal(result.stderr, '');
    assert.ok(result.stdout.includes(commitC), result.stdout);
    assert.match(result.stdout, /isn't here/);
    assert.equal(lastLine(result), command('--clear', 'v1.0.0', '--origin', commitC));
  });

  test('clears a tag whose commit only origin has', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0', '--origin', commitC], { local: {}, remote: { 'v1.0.0': commitC }, commits: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  describe('run unchanged', () => {
    for (const args of [[], ['patch'], ['major']]) {
      test(`makes what the ${args[0] ?? 'default'} preview showed`, async () => {
        const git = createFakeGit(makeState);
        const preview = await run([...args, '--dry-run'], git);
        const tag = /\bv\d+\.\d+\.\d+\b/.exec(preview.stdout)[0];
        const result = await run(argsOf(lastLine(preview)), git);
        assert.equal(result.code, 0, result.stderr);
        assert.deepEqual(git.snapshot().remote[tag], commitA);
        assert.deepEqual(git.snapshot().local[tag], commitA);
      });
    }

    test('clears what a clear preview showed', async () => {
      const git = createFakeGit(onBoth({ 'v1.0.0': commitA, 'v1.1.0': commitA }));
      const preview = await run(['--clear', 'v1.0.0', '--dry-run'], git);
      const result = await run(argsOf(lastLine(preview)), git);
      assert.equal(result.code, 0, result.stderr);
      assert.deepEqual(git.snapshot(), onBoth({ 'v1.1.0': commitA }));
    });
  });
});

describe('a make with --expect and --commit (unit)', () => {
  const state = { ...onBoth({ 'v1.2.3': commitA }), originHead: commitA };
  const refusesUnchanged = async (args, options, message) => {
    const git = createFakeGit(options);
    const before = git.snapshot();
    const result = await run(args, git);
    assertRefusal(result, message);
    assert.deepEqual(git.snapshot(), before);
  };

  test('makes the tag on that commit when both still match', async () => {
    const { git, result } = await runWith(['minor', '--expect', 'v1.3.0', '--commit', commitA], state);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot().remote['v1.3.0'], commitA);
  });

  test('accepts a 64-character commit id', async () => {
    const { git, result } = await runWith(['--expect', 'v1.3.0', '--commit', commitC], { ...state, originHead: commitC });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot().remote['v1.3.0'], commitC);
  });

  test('refuses when the part gives another tag than the expected one', async () => {
    await refusesUnchanged(['major', '--expect', 'v1.3.0', '--commit', commitA], state, /v1\.3\.0/);
  });

  test('refuses when another next version was pushed meanwhile', async () => {
    await refusesUnchanged(['minor', '--expect', 'v1.3.0', '--commit', commitA], { ...state, remote: { 'v1.2.3': commitA, 'v1.3.0': commitA } });
  });

  test('refuses when origin\'s default branch has a newer commit than the approved one', async () => {
    await refusesUnchanged(['minor', '--expect', 'v1.3.0', '--commit', commitA], { ...state, originHead: 'old', afterFetch: { originHead: commitB } });
  });

  test('goes ahead when only a lower version tag was pushed meanwhile', async () => {
    const git = createFakeGit(state);
    git.setRemoteTag('v1.0.0', commitA);
    const result = await run(['minor', '--expect', 'v1.3.0', '--commit', commitA], git);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot().remote['v1.3.0'], commitA);
  });

  test('still works without approval options', async () => {
    const { result } = await runWith(['minor'], state);
    assert.equal(result.code, 0, result.stderr);
  });

  test('still runs the branch and working tree checks', async () => {
    await refusesUnchanged(['--expect', 'v1.3.0', '--commit', commitA], { ...state, clean: false }, /uncommitted/i);
  });
});

describe('a clear with --here and --origin (unit)', () => {
  const refusesUnchanged = async (args, options) => {
    const git = createFakeGit(options);
    const before = git.snapshot();
    const result = await run(args, git);
    assertRefusal(result, /v1\.0\.0/);
    assert.deepEqual(git.snapshot(), before);
  };
  const both = onBoth({ 'v1.0.0': commitA });

  test('deletes the tag in both places when it is there on those commits', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], both);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('accepts the options in either order', async () => {
    const { result } = await runWith(['--clear', 'v1.0.0', '--origin', commitA, '--here', commitA], both);
    assert.equal(result.code, 0, result.stderr);
  });

  test('deletes a tag that is only here when only --here is given', async () => {
    const { git, result } = await runWith(['--clear', 'v1.0.0', '--here', commitA], { local: { 'v1.0.0': commitA }, remote: {} });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
  });

  test('refuses when the tag here is on another commit', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--here', commitB, '--origin', commitA], both);
  });

  test('refuses when the tag on origin is on another commit', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitB], both);
  });

  test('refuses when the tag is also on origin but only --here was approved', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--here', commitA], both);
  });

  test('refuses when the tag is also here but only --origin was approved', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--origin', commitA], both);
  });

  test('refuses when the tag is no longer on origin', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], { local: { 'v1.0.0': commitA }, remote: {} });
  });

  test('refuses when the tag is no longer here', async () => {
    await refusesUnchanged(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], { local: {}, remote: { 'v1.0.0': commitA } });
  });

  test('deletes the named tag, not a newer one pushed after the preview', async () => {
    const git = createFakeGit(both);
    git.setRemoteTag('v2.0.0', commitA);
    git.setLocalTag('v2.0.0', commitA);
    const result = await run(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], git);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.snapshot(), onBoth({ 'v2.0.0': commitA }));
  });

  test('goes ahead when only a lower version tag was pushed meanwhile', async () => {
    const git = createFakeGit(both);
    git.setRemoteTag('v0.5.0', commitA);
    const result = await run(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], git);
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(Object.keys(git.snapshot().remote), ['v0.5.0']);
  });

  test('still guards the delete on origin with the tag\'s own id', async () => {
    const git = createFakeGit({ ...both, afterRemoteTagsRead: (fake) => fake.setRemoteTag('v1.0.0', { commit: commitA, id: commitB }) });
    const result = await run(['--clear', 'v1.0.0', '--here', commitA, '--origin', commitA], git);
    assertRefusal(result, /changed on origin/);
  });
});

describe('approval options in the command line (unit)', () => {
  const short = 'abc1234';
  const wrong = [
    ['--expect without --commit', ['--expect', 'v1.3.0']],
    ['--commit without --expect', ['--commit', commitA]],
    ['--expect with a missing value', ['--commit', commitA, '--expect']],
    ['--commit with a missing value', ['--expect', 'v1.3.0', '--commit']],
    ['--expect with a tag that is not a version tag', ['--expect', 'proof-1', '--commit', commitA]],
    ['--expect given twice', ['--expect', 'v1.3.0', '--expect', 'v1.3.0', '--commit', commitA]],
    ['--commit given twice', ['--expect', 'v1.3.0', '--commit', commitA, '--commit', commitA]],
    ['--expect with a part and a second part', ['minor', 'major', '--expect', 'v1.3.0', '--commit', commitA]],
    ['a short commit id', ['--expect', 'v1.3.0', '--commit', short]],
    ['an uppercase commit id', ['--expect', 'v1.3.0', '--commit', commitA.toUpperCase()]],
    ['a commit id with a non-hex letter', ['--expect', 'v1.3.0', '--commit', `g${commitA.slice(1)}`]],
    ['a 41-character commit id', ['--expect', 'v1.3.0', '--commit', `${commitA}a`]],
    ['a 63-character commit id', ['--expect', 'v1.3.0', '--commit', commitC.slice(1)]],
    ['a branch name as the commit', ['--expect', 'v1.3.0', '--commit', 'main']],
    ['--expect with --dry-run', ['--expect', 'v1.3.0', '--commit', commitA, '--dry-run']],
    ['--expect with --clear', ['--clear', 'v1.0.0', '--expect', 'v1.3.0', '--commit', commitA]],
    ['--expect with --check', ['--check', 'v1.0.0', '--expect', 'v1.3.0', '--commit', commitA]],
    ['--here with a make', ['minor', '--here', commitA]],
    ['--origin with a make', ['--origin', commitA]],
    ['--here without a tag to clear', ['--clear', '--here', commitA]],
    ['--origin without a tag to clear', ['--clear', '--origin', commitA]],
    ['--here without --clear', ['v1.0.0', '--here', commitA]],
    ['--here with a short commit id', ['--clear', 'v1.0.0', '--here', short]],
    ['--origin with an uppercase commit id', ['--clear', 'v1.0.0', '--origin', commitA.toUpperCase()]],
    ['--here with a missing value', ['--clear', 'v1.0.0', '--here']],
    ['--here given twice', ['--clear', 'v1.0.0', '--here', commitA, '--here', commitA]],
    ['--origin given twice', ['--clear', 'v1.0.0', '--origin', commitA, '--origin', commitA]],
    ['--here with --dry-run', ['--clear', 'v1.0.0', '--here', commitA, '--dry-run']],
    ['--origin with --dry-run', ['--clear', 'v1.0.0', '--origin', commitA, '--dry-run']],
    ['--here with --check', ['--check', 'v1.0.0', '--here', commitA]],
    ['--clear with --expect and --here', ['--clear', 'v1.0.0', '--here', commitA, '--expect', 'v1.3.0']],
  ];
  for (const [what, args] of wrong) {
    test(`fails before any git operation for ${what}`, async () => {
      const { git, result } = await runWith(args, onBoth({ 'v1.0.0': commitA }));
      assertRefusal(result);
      assert.deepEqual(git.calls, []);
    });
  }

  for (const id of [commitA, commitC]) {
    test(`accepts a ${id.length}-character lowercase hex commit id`, async () => {
      const { result } = await runWith(['--expect', 'v0.1.0', '--commit', id], { originHead: id, commits: { [id]: { shortId: id.slice(0, 7), subject: 'x' } } });
      assert.equal(result.code, 0, result.stderr);
    });
  }
});

// ---------- changelog: unit ----------

// The committed changelog of each case the design lists for making v1.3.0 after v1.2.0.
const withEntries = '# Changelog\n\nWhat changed.\n\n## Unreleased\n\n- You can now pick a color.\n\n## 1.2.0\n\n- Older notes.\n';
const withoutEntries = '# Changelog\n\nWhat changed.\n\n## Unreleased\n\n## 1.2.0\n\n- Older notes.\n';
const ready = '# Changelog\n\nWhat changed.\n\n## Unreleased\n\n## 1.3.0\n\n- You can now pick a color.\n\n## 1.2.0\n\n- Older notes.\n';
const notReady = [
  ['has entries under Unreleased and no section for the version', withEntries, /rename/i],
  ['has no section and no entries under Unreleased', withoutEntries, /\badd\b/i],
  ['has no CHANGELOG.md', undefined, /\badd\b/i],
  ['has only the section of a version that merely starts the same, 11.3.0', withoutEntries.replace('## 1.2.0', '## 11.3.0'), /\badd\b/i],
  ['has an empty section for the version', ready.replace('- You can now pick a color.\n\n## 1.2.0', '## 1.2.0'), /write/i],
  ['has an empty section for the version at the end of the file', '## Unreleased\n\n## 1.3.0\n', /write/i],
  ['has the version\'s section twice', `${ready}\n## 1.3.0\n\n- Again.\n`, /write/i],
];
// Entries beside a version's section matter to making its tag, not to reading its notes.
const stillInUnreleased = ['has the version\'s section and entries still under Unreleased', ready.replace('## Unreleased\n', '## Unreleased\n\n- Still here.\n'), /move/i];
const after120 = onBoth({ 'v1.2.0': 'c1' });

describe('the changelog check when making a tag (unit)', () => {
  for (const mode of [[], ['--dry-run']]) {
    const how = mode.length ? 'a preview' : 'a real run';

    for (const [what, changelog, fix] of [...notReady, stillInUnreleased]) {
      test(`${how} refuses when the committed changelog ${what}, naming the version and the fix, and changes no tag`, async () => {
        const git = createFakeGit({ ...after120, changelog });
        const before = git.snapshot();
        const result = await run(mode, git);
        assertRefusal(result, /1\.3\.0/);
        assert.match(result.stderr, fix);
        assert.match(result.stderr, /commit/);
        assert.match(result.stderr, /push/);
        assert.deepEqual(git.snapshot(), before);
      });
    }

    test(`${how} tells to rename Unreleased by that name`, async () => {
      const { result } = await runWith(mode, { ...after120, changelog: withEntries });
      assert.match(result.stderr, /Unreleased/);
    });

    test(`${how} names the version of the part it was given`, async () => {
      const { result } = await runWith(['major', ...mode], { ...after120, changelog: withEntries });
      assertRefusal(result, /2\.0\.0/);
    });

    test(`${how} reads the changelog on the commit it would tag`, async () => {
      const { result } = await runWith(mode, { ...after120, originHead: 'c2', commits: secondCommit, changelog: ready, changelogs: { c2: withEntries } });
      assertRefusal(result, /1\.3\.0/);
    });

    test(`${how} accepts the changelog of the commit it would tag, whatever another commit has`, async () => {
      const { result } = await runWith(mode, { ...after120, originHead: 'c2', commits: secondCommit, changelog: withEntries, changelogs: { c2: ready } });
      assert.equal(result.code, 0, result.stderr);
    });

    test(`${how} reports a wrong branch before a changelog problem`, async () => {
      const { result } = await runWith(mode, { ...after120, branch: 'feature', changelog: withEntries });
      assertRefusal(result, /main/);
    });

    test(`${how} reports uncommitted changes before a changelog problem`, async () => {
      const { result } = await runWith(mode, { ...after120, clean: false, changelog: withEntries });
      assertRefusal(result, /uncommitted/i);
    });

    test(`${how} reports being behind origin before a changelog problem`, async () => {
      const { result } = await runWith(mode, { ...after120, behind: 1, changelog: withEntries });
      assertRefusal(result, /behind/);
    });
  }

  test('makes and pushes the tag when the version\'s section has entries and Unreleased has none', async () => {
    const { git, result } = await runWith([], { ...after120, changelog: ready });
    assert.equal(result.code, 0, result.stderr);
    assert.equal(git.snapshot().remote['v1.3.0'], 'c1');
  });

  test('previews the tag when the version\'s section has entries and Unreleased has none', async () => {
    const { result } = await runWith(['--dry-run'], { ...after120, changelog: ready });
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /v1\.3\.0/);
  });

  test('treats blank lines under Unreleased as no entries', async () => {
    const { result } = await runWith([], { ...after120, changelog: ready.replace('## Unreleased\n', '## Unreleased\n\n   \n\n') });
    assert.equal(result.code, 0, result.stderr);
  });

  test('makes the tag from the committed changelog, whatever the working tree has', async () => {
    const { result } = await runWith([], { ...after120, changelog: ready, workingChangelog: withEntries });
    assert.equal(result.code, 0, result.stderr);
  });

  for (const args of [['--clear'], ['--clear', 'v1.2.0'], ['--clear', '--dry-run']]) {
    test(`${args.join(' ')} ignores the changelog, even a missing one`, async () => {
      const { git, result } = await runWith(args, { ...after120, changelog: undefined });
      assert.equal(result.code, 0, result.stderr);
      if (!args.includes('--dry-run')) assert.deepEqual(git.snapshot(), { local: {}, remote: {} });
    });
  }
});

describe('--notes prints a version\'s notes from the checked-out changelog (unit)', () => {
  const notes = (workingChangelog, tag = 'v1.3.0') => runWith(['--notes', tag], { workingChangelog });

  test('prints the text under the version\'s heading without the heading, and succeeds', async () => {
    const { result } = await notes(ready);
    assert.deepEqual(result, { code: 0, stdout: '- You can now pick a color.\n', stderr: '' });
  });

  test('stops at the next heading and leaves out other versions and Unreleased', async () => {
    const { result } = await notes('## Unreleased\n\n## 1.4.0\n\n- Newer.\n\n## 1.3.0\n\n- This.\n- And this.\n\n## 1.2.0\n\n- Older.\n');
    assert.equal(result.stdout, '- This.\n- And this.\n');
  });

  test('prints the last section of the file', async () => {
    const { result } = await notes('## Unreleased\n\n## 1.3.0\n\n- Only this.\n');
    assert.equal(result.stdout, '- Only this.\n');
  });

  test('trims blank lines at the start and end', async () => {
    const { result } = await notes('## Unreleased\n\n## 1.3.0\n\n\n\n- Padded.\n\n\n\n## 1.2.0\n\n- Older.\n');
    assert.equal(result.stdout, '- Padded.\n');
  });

  test('keeps blank lines and indentation inside the notes', async () => {
    const { result } = await notes('## Unreleased\n\n## 1.3.0\n\n- First.\n  - Nested.\n\n- Second, with **bold** text.\n\n## 1.2.0\n\n- Older.\n');
    assert.equal(result.stdout, '- First.\n  - Nested.\n\n- Second, with **bold** text.\n');
  });

  test('does not take the section of a version that merely starts the same', async () => {
    const { result } = await notes('## Unreleased\n\n## 11.3.0\n\n- Not this.\n\n## 1.3.01\n\n- Nor this.\n');
    assertRefusal(result, /1\.3\.0/);
  });

  test('runs no git, so the checked-out file is what it reads, not the committed one', async () => {
    const { git, result } = await runWith(['--notes', 'v1.3.0'], { workingChangelog: ready, changelog: undefined });
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(git.calls, []);
  });

  for (const tag of ['v1.2', 'proof-1', 'v01.2.3', '1.3.0', 'v1.3.0-rc1']) {
    test(`fails with one error line naming "${tag}" and the form v<major>.<minor>.<patch>, since it is not a version tag`, async () => {
      const { git, result } = await notes(ready, tag);
      assertRefusal(result);
      assert.ok(result.stderr.includes(tag));
      assert.match(result.stderr, /v<major>\.<minor>\.<patch>/);
      assert.deepEqual(git.calls, []);
    });
  }

  for (const [what, changelog, fix] of notReady) {
    test(`fails with one error line naming the version and the fix when the changelog ${what}`, async () => {
      const { git, result } = await notes(changelog);
      assertRefusal(result, /1\.3\.0/);
      assert.match(result.stderr, fix);
      assert.deepEqual(git.calls, []);
    });
  }
});

describe('--notes arguments (unit)', () => {
  const wrong = [
    ['no tag', ['--notes']],
    ['two tags', ['--notes', 'v1.0.0', 'v1.0.1']],
    ['a part', ['--notes', 'v1.0.0', 'major']],
    ['--clear', ['--notes', 'v1.0.0', '--clear']],
    ['--clear first', ['--clear', '--notes', 'v1.0.0']],
    ['--dry-run', ['--notes', 'v1.0.0', '--dry-run']],
    ['--dry-run first', ['--dry-run', '--notes', 'v1.0.0']],
    ['--check', ['--notes', 'v1.0.0', '--check']],
    ['--check first', ['--check', 'v1.0.0', '--notes']],
    ['--notes twice', ['--notes', 'v1.0.0', '--notes']],
    ['a part first', ['major', '--notes', 'v1.0.0']],
    ['an approval option', ['--notes', 'v1.0.0', '--expect', 'v1.0.0', '--commit', commitA]],
  ];
  for (const [what, args] of wrong) {
    test(`fails before any git operation, as --notes takes exactly one tag, when given ${what}`, async () => {
      const { git, result } = await runWith(args, { workingChangelog: ready.replaceAll('1.3.0', '1.0.0') });
      assertRefusal(result, /--notes/);
      assert.doesNotMatch(result.stderr, /unknown option/);
      assert.deepEqual(git.calls, []);
    });
  }
});

// ---------- integration ----------

const gitEnv = {
  // Settings passed through the environment would reach every test's git, so none are inherited.
  ...Object.fromEntries(Object.entries(process.env).filter(([name]) => !/^GIT_CONFIG_(COUNT|KEY_|VALUE_|PARAMETERS)/i.test(name))),
  // Git for Windows rejects os.devNull ('\\.\nul') but maps '/dev/null' on every platform.
  GIT_CONFIG_GLOBAL: '/dev/null',
  GIT_CONFIG_NOSYSTEM: '1',
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Test',
  GIT_AUTHOR_EMAIL: 'test@example.com',
  GIT_COMMITTER_NAME: 'Test',
  GIT_COMMITTER_EMAIL: 'test@example.com',
};

function git(cwd, ...args) {
  const result = spawnSync('git', args, { cwd, env: gitEnv, encoding: 'utf8', timeout: 30_000 });
  assert.ifError(result.error);
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

// A work repository with the script and a CHANGELOG.md committed, and a bare repository as its origin.
// The changelog is valid for every version the tests make, unless a test gives its own text.
function repositories(t, { changelog = validChangelog } = {}) {
  const root = mkdtempSync(join(tmpdir(), 'memoria-version-tag-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const origin = join(root, 'origin.git');
  const work = join(root, 'work');
  mkdirSync(origin);
  mkdirSync(join(work, '.agents'), { recursive: true });
  git(origin, 'init', '--bare', '--initial-branch=main');
  git(work, 'init', '--initial-branch=main');
  git(work, 'config', 'commit.gpgsign', 'false');
  git(work, 'remote', 'add', 'origin', origin);
  copyFileSync(script, join(work, '.agents', 'version-tag.mjs'));
  writeFileSync(join(work, 'CHANGELOG.md'), changelog);
  git(work, 'add', '.');
  git(work, 'commit', '-m', 'Add the script');
  git(work, 'push', '--set-upstream', 'origin', 'main');

  const clone = () => {
    const path = mkdtempSync(join(root, 'clone-'));
    git(path, 'clone', origin, '.');
    git(path, 'config', 'commit.gpgsign', 'false');
    return path;
  };
  const tags = (cwd) => git(cwd, 'tag', '--list').split('\n').filter(Boolean);
  const runScriptWith = (env, cwd, ...args) => {
    const result = spawnSync(process.execPath, [join(work, '.agents', 'version-tag.mjs'), ...args], { cwd, env: { ...gitEnv, ...env }, encoding: 'utf8', timeout: 60_000 });
    assert.ifError(result.error);
    return result;
  };
  const runScript = (cwd, ...args) => runScriptWith({}, cwd, ...args);
  return { root, origin, work, clone, tags, runScript, runScriptWith };
}

describe('version-tag.mjs on real git (integration)', () => {
  test('--check prints the version and exits 0 for a version tag, from a folder that is no repository', (t) => {
    const { root, runScript } = repositories(t);
    const result = runScript(root, '--check', 'v1.2.3');
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, '1.2.3\n');
    assert.equal(result.stderr, '');
  });

  test('--check prints nothing on stdout, one line on stderr, and exits 1 for another tag', (t) => {
    const { root, runScript } = repositories(t);
    const result = runScript(root, '--check', 'proof-1');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.equal(lines(result.stderr).length, 1);
    assert.ok(result.stderr.includes('proof-1'));
  });

  test('makes an annotated tag on the newest commit and pushes it, so origin has the same commit', (t) => {
    const { work, origin, runScript } = repositories(t);
    const result = runScript(work);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /v0\.1\.0/);
    const head = git(work, 'rev-parse', 'HEAD');
    assert.equal(git(work, 'cat-file', '-t', 'v0.1.0'), 'tag');
    assert.equal(git(work, 'rev-parse', 'v0.1.0^{commit}'), head);
    assert.equal(git(origin, 'rev-parse', 'v0.1.0^{commit}'), head);
  });

  test('raises the part it was given from the latest tag', (t) => {
    const { work, origin, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const result = runScript(work, 'patch');
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(git(origin, 'tag', '--list').split('\n').sort(), ['v0.1.0', 'v0.1.1']);
  });

  test('refuses as behind when another clone pushed a commit to origin after the last fetch', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    const other = clone();
    writeFileSync(join(other, 'news.txt'), 'new\n');
    git(other, 'add', '.');
    git(other, 'commit', '-m', 'Another commit');
    git(other, 'push', 'origin', 'main');

    const result = runScript(work);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /behind/);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), []);
  });

  test('refuses when the working tree has an untracked file', (t) => {
    const { work, tags, runScript } = repositories(t);
    writeFileSync(join(work, 'notes.txt'), 'draft\n');
    const result = runScript(work);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /uncommitted/i);
    assert.deepEqual(tags(work), []);
  });

  test('ignored files do not make the working tree unclean', (t) => {
    const { work, origin, runScript } = repositories(t);
    writeFileSync(join(work, '.gitignore'), '*.log\n');
    git(work, 'add', '.');
    git(work, 'commit', '-m', 'Ignore logs');
    git(work, 'push', 'origin', 'main');
    writeFileSync(join(work, 'build.log'), 'output\n');

    const result = runScript(work);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(git(origin, 'tag', '--list'), 'v0.1.0');
  });

  test('--clear deletes the latest version tag from the work repository and from origin', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const result = runScript(work, '--clear');
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), []);
  });

  test('--clear leaves other tags alone', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    git(work, 'tag', 'proof-1');
    git(work, 'push', 'origin', 'proof-1');
    assert.equal(runScript(work, '--clear', 'v0.1.0').status, 0);
    assert.deepEqual(tags(work), ['proof-1']);
    assert.deepEqual(tags(origin), ['proof-1']);
  });

  test('--dry-run leaves tags unchanged, and a tag only on origin stays absent here', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    const other = clone();
    git(other, 'tag', '-a', 'v0.5.0', '-m', 'Made elsewhere');
    git(other, 'push', 'origin', 'v0.5.0');

    const preview = runScript(work, '--dry-run');
    assert.equal(preview.status, 0, preview.stderr);
    assert.match(preview.stdout, /v0\.6\.0/);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), ['v0.5.0']);

    const clearing = runScript(work, '--clear', '--dry-run');
    assert.equal(clearing.status, 0, clearing.stderr);
    assert.match(clearing.stdout, /v0\.5\.0/);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), ['v0.5.0']);
  });

  test('acts on the repository the script sits in, whatever folder it runs from', (t) => {
    const { root, work, origin, tags, runScript } = repositories(t);
    const elsewhere = join(root, 'elsewhere');
    mkdirSync(elsewhere);
    git(elsewhere, 'init', '--initial-branch=main');

    const result = runScript(elsewhere);
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(tags(work), ['v0.1.0']);
    assert.deepEqual(tags(origin), ['v0.1.0']);
    assert.deepEqual(tags(elsewhere), []);
    assert.equal(existsSync(join(elsewhere, '.agents')), false);
  });

  test('--check prints every digit of a part above 2^53', (t) => {
    const { root, runScript } = repositories(t);
    const result = runScript(root, '--check', 'v9007199254740993.0.18446744073709551617');
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, '9007199254740993.0.18446744073709551617\n');
  });
});

describe('version-tag.mjs changelog on real git (integration)', () => {
  test('--notes prints the version\'s notes, from the script\'s repository whatever folder it runs from, and exits 0', (t) => {
    const { root, runScript } = repositories(t, { changelog: ready });
    const result = runScript(root, '--notes', 'v1.3.0');
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, '- You can now pick a color.\n');
    assert.equal(result.stderr, '');
  });

  test('--notes reads the file as checked out, including a change not committed yet', (t) => {
    const { work, runScript } = repositories(t, { changelog: ready });
    writeFileSync(join(work, 'CHANGELOG.md'), ready.replace('pick a color', 'pick two colors'));
    const result = runScript(work, '--notes', 'v1.3.0');
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, '- You can now pick two colors.\n');
  });

  test('--notes prints one error line and exits 1 when the version has no notes', (t) => {
    const { work, runScript } = repositories(t, { changelog: ready });
    const result = runScript(work, '--notes', 'v1.4.0');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.equal(lines(result.stderr).length, 1);
    assert.ok(result.stderr.includes('1.4.0'));
  });

  test('--notes prints one error line and exits 1 for a tag that is not a version tag', (t) => {
    const { work, runScript } = repositories(t, { changelog: ready });
    const result = runScript(work, '--notes', 'proof-1');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.equal(lines(result.stderr).length, 1);
    assert.ok(result.stderr.includes('proof-1'));
  });

  test('makes and pushes the tag when the committed changelog has the version\'s notes', (t) => {
    const { work, origin, runScript } = repositories(t, { changelog: changelogWith('0.1.0') });
    const result = runScript(work);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(git(origin, 'tag', '--list'), 'v0.1.0');
  });

  test('refuses with one line naming the version, and makes no tag, when the committed changelog lacks its notes', (t) => {
    const { work, origin, tags, runScript } = repositories(t, { changelog: withEntries });
    const result = runScript(work);
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.equal(lines(result.stderr).length, 1);
    assert.ok(result.stderr.includes('0.1.0'));
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), []);
  });
});

describe('version-tag.mjs git commands and settings on real git (integration)', () => {
  const traced = (t, root) => {
    const file = join(root, 'trace.txt');
    t.after(() => rmSync(file, { force: true }));
    return { file, env: { GIT_TRACE: file } };
  };
  // The git commands the script ran, as one string per command with quotes removed.
  const commands = (file, name) => readFileSync(file, 'utf8').split('\n').map((line) => line.replaceAll("'", '')).filter((line) => line.includes(`built-in: git ${name} `)).map((line) => line.slice(line.indexOf(`git ${name} `)));
  const otherClonePushesTag = (clone, name) => {
    const other = clone();
    git(other, 'tag', '-a', name, '-m', 'Made elsewhere');
    git(other, 'push', 'origin', name);
  };

  test('fetches only origin\'s default branch with tags and pruning off, and every push turns tag following off', (t) => {
    const { root, work, runScriptWith } = repositories(t);
    const { file, env } = traced(t, root);
    assert.equal(runScriptWith(env, work).status, 0);
    assert.equal(runScriptWith(env, work, '--clear').status, 0);
    assert.ok(commands(file, 'fetch').length >= 1);
    for (const line of commands(file, 'fetch')) {
      assert.equal(line, 'git fetch --no-tags --no-prune --refmap= origin +refs/heads/main:refs/remotes/origin/main');
    }
    const pushes = commands(file, 'push');
    assert.equal(pushes.length, 2);
    for (const line of pushes) assert.match(line, /^git push --no-follow-tags /);
  });

  test('--clear deletes on origin with a lease on the tag\'s own id, not its commit\'s, and then deletes here', (t) => {
    const { root, work, origin, tags, runScriptWith } = repositories(t);
    const { file, env } = traced(t, root);
    assert.equal(runScriptWith(env, work).status, 0);
    const tagId = git(work, 'rev-parse', 'v0.1.0');
    assert.notEqual(tagId, git(work, 'rev-parse', 'v0.1.0^{commit}'));
    assert.equal(runScriptWith(env, work, '--clear').status, 0);
    assert.ok(commands(file, 'push').includes(`git push --no-follow-tags --force-with-lease=refs/tags/v0.1.0:${tagId} origin :refs/tags/v0.1.0`), commands(file, 'push').join('\n'));
    assert.deepEqual(tags(origin), []);
    assert.deepEqual(tags(work), []);
  });

  test('the guarded delete removes an annotated tag for its own id and refuses, deleting nothing, for its commit\'s id or any other', (t) => {
    const { work, origin, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const tagId = git(origin, 'rev-parse', 'refs/tags/v0.1.0');
    const commitId = git(origin, 'rev-parse', 'refs/tags/v0.1.0^{commit}');
    const guarded = (id) => spawnSync('git', ['push', '--no-follow-tags', `--force-with-lease=refs/tags/v0.1.0:${id}`, 'origin', ':refs/tags/v0.1.0'], { cwd: work, env: gitEnv, encoding: 'utf8' });
    for (const id of [commitId, '0'.repeat(40)]) {
      assert.notEqual(guarded(id).status, 0);
      assert.equal(git(origin, 'tag', '--list'), 'v0.1.0');
    }
    assert.equal(guarded(tagId).status, 0);
    assert.equal(git(origin, 'tag', '--list'), '');
  });

  test('the guarded delete here removes an annotated tag for its own id and refuses, deleting nothing, for its commit\'s id or any other', (t) => {
    const { work, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const tagId = git(work, 'rev-parse', 'refs/tags/v0.1.0');
    const commitId = git(work, 'rev-parse', 'refs/tags/v0.1.0^{commit}');
    const guarded = (id) => spawnSync('git', ['update-ref', '-d', 'refs/tags/v0.1.0', id], { cwd: work, env: gitEnv, encoding: 'utf8' });
    // An all-zero old value means "no check" to update-ref -d, and the script never passes one.
    for (const id of [commitId, 'f'.repeat(40)]) {
      assert.notEqual(guarded(id).status, 0);
      assert.equal(git(work, 'tag', '--list'), 'v0.1.0');
    }
    assert.equal(guarded(tagId).status, 0);
    assert.equal(git(work, 'tag', '--list'), '');
  });

  test('the delete here never deletes the branch a symbolic tag ref points to', (t) => {
    const { work } = repositories(t);
    const mainId = git(work, 'rev-parse', 'refs/heads/main');
    git(work, 'symbolic-ref', 'refs/tags/v1.0.0', 'refs/heads/main');

    // The adapter runs in a child process so its git gets the isolated gitEnv, not the user's settings or hooks.
    // Whether the delete succeeds or refuses is open; what matters is that it never reaches main.
    const deleteHere = `const { createGit } = await import(${JSON.stringify(pathToFileURL(script).href)});
      await createGit(process.cwd()).deleteLocalTag('v1.0.0', ${JSON.stringify(mainId)}).catch(() => {});`;
    const child = spawnSync(process.execPath, ['--input-type=module', '-e', deleteHere], { cwd: work, env: gitEnv, encoding: 'utf8', timeout: 30_000 });
    assert.equal(child.status, 0, child.stderr);
    assert.equal(git(work, 'rev-parse', '--verify', 'refs/heads/main'), mainId);
  });

  test('refuses a make as not clean for an untracked file even when status.showUntrackedFiles is no', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    git(work, 'config', 'status.showUntrackedFiles', 'no');
    writeFileSync(join(work, 'notes.txt'), 'draft\n');
    const result = runScript(work);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /uncommitted/i);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), []);
  });

  test('git settings that fetch, prune, or push other tags change nothing beyond the one tag', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    otherClonePushesTag(clone, 'proof-9');
    git(work, 'tag', 'only-here');
    git(work, 'tag', '-a', 'annotated-here', '-m', 'Not pushed');
    git(work, 'config', 'remote.origin.fetch', '+refs/heads/*:refs/remotes/origin/*');
    git(work, 'config', '--add', 'remote.origin.fetch', '+refs/tags/*:refs/tags/*');
    git(work, 'config', 'fetch.prune', 'true');
    git(work, 'config', 'fetch.pruneTags', 'true');
    git(work, 'config', 'push.followTags', 'true');

    const made = runScript(work);
    assert.equal(made.status, 0, made.stderr);
    assert.deepEqual(tags(work).sort(), ['annotated-here', 'only-here', 'v0.1.0']);
    assert.deepEqual(tags(origin).sort(), ['proof-9', 'v0.1.0']);

    const cleared = runScript(work, '--clear');
    assert.equal(cleared.status, 0, cleared.stderr);
    assert.deepEqual(tags(work).sort(), ['annotated-here', 'only-here']);
    assert.deepEqual(tags(origin), ['proof-9']);
  });
});

describe('version-tag.mjs pushing a tag by its object id on real git (integration)', () => {
  test('pushing <id>:refs/tags/<name> creates that tag on origin with that object, even when the tag here moved', (t) => {
    const { work, origin } = repositories(t);
    const head = git(work, 'rev-parse', 'HEAD');
    git(work, 'tag', '--annotate', '--message', 'Version v0.1.0', 'v0.1.0', head);
    const id = git(work, 'rev-parse', 'refs/tags/v0.1.0');
    writeFileSync(join(work, 'news.txt'), 'new\n');
    git(work, 'add', '.');
    git(work, 'commit', '-m', 'Another commit');
    git(work, 'tag', '--annotate', '--force', '--message', 'Moved', 'v0.1.0', 'HEAD');

    git(work, 'push', '--no-follow-tags', 'origin', `${id}:refs/tags/v0.1.0`);
    assert.equal(git(origin, 'rev-parse', 'refs/tags/v0.1.0'), id);
    assert.equal(git(origin, 'rev-parse', 'v0.1.0^{commit}'), head);
  });
});

describe('version-tag.mjs preview and approved run on real git (integration)', () => {
  const lastLine = (result) => lines(result.stdout).at(-1);
  const approved = (result) => {
    const words = lastLine(result).split(' ');
    assert.deepEqual(words.slice(0, 2), ['node', '.agents/version-tag.mjs']);
    return words.slice(2);
  };

  test('the last line of a make preview, run unchanged, makes what the preview showed', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    const preview = runScript(work, 'patch', '--dry-run');
    assert.equal(preview.status, 0, preview.stderr);
    assert.deepEqual(tags(work), []);
    const head = git(work, 'rev-parse', 'HEAD');
    assert.equal(lastLine(preview), `node .agents/version-tag.mjs patch --expect v0.1.0 --commit ${head}`);

    const result = runScript(work, ...approved(preview));
    assert.equal(result.status, 0, result.stderr);
    assert.equal(git(origin, 'rev-parse', 'v0.1.0^{commit}'), head);
    assert.deepEqual(tags(work), ['v0.1.0']);
  });

  test('an approved make refuses, changing no tag, when origin\'s default branch got a commit after the preview', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    const preview = runScript(work, '--dry-run');
    const other = clone();
    writeFileSync(join(other, 'news.txt'), 'new\n');
    git(other, 'add', '.');
    git(other, 'commit', '-m', 'Another commit');
    git(other, 'push', 'origin', 'main');

    const result = runScript(work, ...approved(preview));
    assert.equal(result.status, 1);
    assert.equal(lines(result.stderr).length, 1);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), []);
  });

  test('an approved make refuses, changing no tag, when another version tag changed the next one', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    const preview = runScript(work, '--dry-run');
    const other = clone();
    git(other, 'tag', '-a', 'v0.1.0', '-m', 'Made elsewhere');
    git(other, 'push', 'origin', 'v0.1.0');

    const result = runScript(work, ...approved(preview));
    assert.equal(result.status, 1);
    assert.deepEqual(tags(work), []);
    assert.deepEqual(tags(origin), ['v0.1.0']);
  });

  test('the last line of a clear preview, run unchanged, clears what the preview showed, and a newer tag stays', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const head = git(work, 'rev-parse', 'HEAD');
    const preview = runScript(work, '--clear', 'v0.1.0', '--dry-run');
    assert.equal(preview.status, 0, preview.stderr);
    assert.equal(lastLine(preview), `node .agents/version-tag.mjs --clear v0.1.0 --here ${head} --origin ${head}`);

    git(work, 'tag', '-a', 'v0.2.0', '-m', 'Newer');
    git(work, 'push', 'origin', 'v0.2.0');
    const result = runScript(work, ...approved(preview));
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(tags(work), ['v0.2.0']);
    assert.deepEqual(tags(origin), ['v0.2.0']);
  });

  test('an approved clear refuses, changing no tag, when the tag is on another commit', (t) => {
    const { work, origin, tags, runScript } = repositories(t);
    assert.equal(runScript(work).status, 0);
    const result = runScript(work, '--clear', 'v0.1.0', '--here', '1'.repeat(40), '--origin', git(work, 'rev-parse', 'HEAD'));
    assert.equal(result.status, 1);
    assert.equal(lines(result.stderr).length, 1);
    assert.deepEqual(tags(work), ['v0.1.0']);
    assert.deepEqual(tags(origin), ['v0.1.0']);
  });

  test('previews and clears a tag whose commit only origin has, without its subject', (t) => {
    const { work, origin, clone, tags, runScript } = repositories(t);
    const other = clone();
    git(other, 'switch', '--create', 'side');
    writeFileSync(join(other, 'side.txt'), 'side\n');
    git(other, 'add', '.');
    git(other, 'commit', '-m', 'Only on a side branch');
    const sideId = git(other, 'rev-parse', 'HEAD');
    git(other, 'tag', '-a', 'v0.5.0', '-m', 'Side release');
    git(other, 'push', 'origin', 'v0.5.0');

    const preview = runScript(work, '--clear', 'v0.5.0', '--dry-run');
    assert.equal(preview.status, 0, preview.stderr);
    assert.ok(preview.stdout.includes(sideId), preview.stdout);
    assert.doesNotMatch(preview.stdout, /Only on a side branch/);
    assert.match(preview.stdout, /isn't here/);
    assert.equal(lastLine(preview), `node .agents/version-tag.mjs --clear v0.5.0 --origin ${sideId}`);

    const result = runScript(work, ...approved(preview));
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(tags(origin), []);
    assert.deepEqual(tags(work), []);
  });
});
