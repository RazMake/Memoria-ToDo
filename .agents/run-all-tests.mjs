#!/usr/bin/env node
// Starts Run All tests (all-tests.yml) on the current branch and follows it, as docs/architecture/releasing.md describes.
//
// run(args, { write, waitForGitHub, git, github }) -> Promise<{ code, stderr }>
//   args: only `--help` is accepted.
//   git: the version tag script's port (currentBranch, isClean, fetch, aheadBehind, originCommit); github: github-runs.mjs's component.
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, createTerminalOutput, messageOf, messageWithRun } from './github-runs.mjs';
import { createGit } from './version-tag.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const usage = 'usage: node .agents\\run-all-tests.mjs';
const workflowFile = 'all-tests.yml';

// Your branch, and its newest commit, read right after the check that it matches origin's.
async function branchToTest(git) {
  const branch = await git.currentBranch();
  if (branch === undefined) throw new Error('HEAD is detached; check out the branch to test.');
  await git.fetch(branch);
  const { ahead, behind } = await git.aheadBehind(branch);
  if (ahead) throw new Error(`${branch} has ${ahead} commit(s) origin doesn't have; push first, so the run tests your commit.`);
  if (behind) throw new Error(`${branch} is ${behind} commit(s) behind origin/${branch}; pull first, so the run tests your commit.`);
  return { branch, commit: await git.originCommit(branch) };
}

export async function run(args, { write, waitForGitHub, git, github }) {
  try {
    if (args.length === 1 && args[0] === '--help') {
      write(`${usage}\nStarts Run All tests on your current branch, which must match origin, and follows the run. Exits 0 only when every test passed.\n`);
      return { code: 0, stderr: '' };
    }
    if (args.length) throw new Error(usage);
    const { branch, commit } = await branchToTest(git);
    if (!(await git.isClean())) write('Warning: uncommitted changes are not tested; the run tests your last pushed commit.\n');

    waitForGitHub('Waiting for GitHub to check your sign-in and origin repository. If sign-in is needed, run gh auth login in another terminal.\n');
    await github.connect();
    waitForGitHub(`Waiting for GitHub to start Run All tests on ${branch}.\n`);
    const started = await github.dispatch(workflowFile, branch);
    write(`${started.url}\n`);
    if (started.headSha !== commit) {
      throw new Error(`The run is for commit ${started.headSha}, not your commit ${commit} (${started.url}); ${branch} changed on origin. Run the command again.`);
    }

    waitForGitHub('Waiting for GitHub to finish the run of every test.\n');
    let finished;
    try {
      finished = await github.follow(started);
    } catch (error) {
      throw new Error(messageWithRun(error, started));
    }
    if (finished.conclusion === 'success') {
      write(`Every test passed: ${finished.url}\n`);
      return { code: 0, stderr: '' };
    }
    const failed = (await github.failedJobs(finished)).map((job) => `\n- ${job.name}: ${job.url}`).join('');
    return { code: 1, stderr: `The run ended with ${finished.conclusion}: ${finished.url}${failed}\n` };
  } catch (error) {
    return { code: 1, stderr: `${messageOf(error)}\n` };
  }
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const controller = new AbortController();
  const terminal = createInterface({ input: process.stdin, output: process.stdout });
  terminal.on('SIGINT', () => controller.abort());
  const execute = createCommands(root, controller.signal);
  try {
    const { write, waitForGitHub } = createTerminalOutput(terminal, process.stdout, controller.signal);
    const result = await run(process.argv.slice(2), { write, waitForGitHub, git: createGit(root, execute), github: createGitHubRuns(execute) });
    if (result.stderr) process.stderr.write(result.stderr);
    process.exitCode = result.code;
  } finally {
    terminal.close();
  }
}
