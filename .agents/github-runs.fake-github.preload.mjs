// Preload for the integration tests that run a command as a child process (`node --import <this file> <script>`).
// It answers every `gh` call and `git remote get-url` call of the script from the fake GitHub in github-runs.fake-github.mjs,
// so no test reaches GitHub, and lets every other program, such as real git, run. Settings come from the environment:
//   FAKE_GITHUB_OPTIONS  JSON of createFakeGitHub's options, plus `tty: true` to make stdin and stdout look like a terminal
//   FAKE_GITHUB_RECORD   a file path; at exit it receives { calls, dispatches } as JSON
import cp from 'node:child_process';
import { EventEmitter } from 'node:events';
import { writeFileSync } from 'node:fs';
import { syncBuiltinESMExports } from 'node:module';
import { basename } from 'node:path';
import { createFakeGitHub } from './github-runs.fake-github.mjs';

const options = JSON.parse(process.env.FAKE_GITHUB_OPTIONS ?? '{}');
const fake = createFakeGitHub(options);
if (options.tty) {
  process.stdin.isTTY = true;
  process.stdout.isTTY = true;
}
if (process.env.FAKE_GITHUB_RECORD) {
  process.on('exit', () => writeFileSync(process.env.FAKE_GITHUB_RECORD, JSON.stringify({ calls: fake.calls, dispatches: fake.dispatches })));
}

const real = cp.execFile;
cp.execFile = (file, args, settings, callback) => {
  const name = basename(file).replace(/\.exe$/i, '');
  if (name !== 'gh' && !(name === 'git' && args[0] === 'remote' && args[1] === 'get-url')) return real(file, args, settings, callback);
  const child = Object.assign(new EventEmitter(), { stdin: { end() {} }, stdout: new EventEmitter(), stderr: new EventEmitter() });
  fake.execute(name, args).then(
    (stdout) => callback(null, stdout, ''),
    (error) => callback(error, '', error.message),
  ).then(() => child.emit('close'));
  return child;
};
syncBuiltinESMExports();
