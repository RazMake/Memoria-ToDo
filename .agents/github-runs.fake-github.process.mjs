// Shared setup of the integration tests that run a command of .agents as a child process against the fake GitHub
// (release.test.mjs, run-all-tests.test.mjs, full-run-check.test.mjs).
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { changelogWith } from './version-tag.fake-git.mjs';

const agents = dirname(fileURLToPath(import.meta.url));
const preload = pathToFileURL(join(agents, 'github-runs.fake-github.preload.mjs')).href;
// A question ends in ": ", followed by the cursor-moving and color codes the terminal prompt writes after it.
const QUESTION_END = /: (?:\u001b\[[0-9;]*[A-Za-z])*$/;
// A command that has not ended by then is stopped, so a missed answer fails the test instead of hanging the run.
const COMMAND_BOUND_MS = 100_000;

/**
 * A real temporary git repository with a copy of the tooling scripts in its .agents folder, a committed CHANGELOG.md for 1.0.0,
 * and a bare repository as origin, all removed after the test. Returns { root, origin, work, git(cwd, ...args), head, script(name) }.
 */
export function repositoryWithTooling(t) {
  const root = mkdtempSync(join(tmpdir(), 'memoria-command-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const origin = join(root, 'origin.git');
  const work = join(root, 'work');
  mkdirSync(origin);
  mkdirSync(join(work, '.agents'), { recursive: true });
  const env = {
    ...process.env, GIT_CONFIG_GLOBAL: '/dev/null', GIT_CONFIG_NOSYSTEM: '1', GIT_TERMINAL_PROMPT: '0',
    GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'safe.bareRepository', GIT_CONFIG_VALUE_0: 'all', // the test reads the bare origin directly
    GIT_AUTHOR_NAME: 'Test', GIT_AUTHOR_EMAIL: 'test@example.com', GIT_COMMITTER_NAME: 'Test', GIT_COMMITTER_EMAIL: 'test@example.com',
  };
  const git = (cwd, ...args) => {
    const result = spawnSync('git', args, { cwd, env, encoding: 'utf8', timeout: 30_000 });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout.trim();
  };
  git(origin, 'init', '--bare', '--initial-branch=main');
  git(work, 'init', '--initial-branch=main');
  git(work, 'config', 'commit.gpgsign', 'false');
  git(work, 'remote', 'add', 'origin', origin);
  for (const name of readdirSync(agents)) {
    if (name.endsWith('.mjs') && !/\.test\.mjs$|\.fake-github|\.process\.mjs$/.test(name)) copyFileSync(join(agents, name), join(work, '.agents', name));
  }
  copyFileSync(join(agents, 'release-platforms.json'), join(work, '.agents', 'release-platforms.json'));
  // The release command runs the Node tests of its own .agents folder first; this one passes at once.
  writeFileSync(join(work, '.agents', 'tooling.test.mjs'), "import { test } from 'node:test';\ntest('the tooling works', () => {});\n");
  writeFileSync(join(work, 'CHANGELOG.md'), changelogWith('1.0.0'));
  git(work, 'add', '.');
  git(work, 'commit', '-m', 'Add the tooling');
  git(work, 'push', '--set-upstream', 'origin', 'main');
  return { root, origin, work, git, head: git(work, 'rev-parse', 'HEAD'), script: (name) => join(work, '.agents', name) };
}

/**
 * Starts `node <script> <args>` with the fake GitHub (createFakeGitHub's options, plus `tty: true` for a command that needs a terminal).
 * `answers` are typed one by one, each the moment a question ends in ": ", since a question that isn't pending drops input.
 * Returns a promise of { code, stdout, stderr, asked, record() }; record() is { calls, dispatches } as the fake saw them.
 */
export function runCommand(directory, script, args, fakeOptions, answers = [], environment = {}) {
  const record = join(dirname(directory), 'record.json');
  const child = spawn(process.execPath, ['--import', preload, script, ...args], {
    cwd: directory,
    env: { ...process.env, NO_COLOR: '1', ...environment, FAKE_GITHUB_OPTIONS: JSON.stringify(fakeOptions), FAKE_GITHUB_RECORD: record },
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  let stdout = '';
  let stderr = '';
  let asked = 0;
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  child.stdout.on('data', (chunk) => {
    stdout += chunk;
    if (asked < answers.length && QUESTION_END.test(stdout)) {
      child.stdin.write(`${answers[asked++]}\n`);
      stdout += '\n';
    }
  });
  if (!answers.length) child.stdin.end();
  const bound = setTimeout(() => child.kill(), COMMAND_BOUND_MS);
  return new Promise((resolve) => child.once('close', (code) => {
    clearTimeout(bound);
    resolve({ code, stdout, stderr, asked, record: () => JSON.parse(readFileSync(record, 'utf8')) });
  }));
}
