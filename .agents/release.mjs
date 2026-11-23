#!/usr/bin/env node
import { execFile } from 'node:child_process';
import { mkdtemp, readdir, rm, stat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, basename } from 'node:path';
import { createInterface } from 'node:readline/promises';
import { setTimeout } from 'node:timers/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { styleText } from 'node:util';
import { checkFullRun, createSource, messageOf } from './full-run-check.mjs';
import { approvalOf, createFiles, createGit, defaultPart, parts, run as tagCommand } from './version-tag.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const usage = `usage: node .agents\\release.mjs [${parts.join('|')}]`;
const runWaitMs = 120_000;
const pollMs = 2_000;
const commandTimeoutMs = 120_000;
const liveTimeoutMs = 30 * 60_000;

export async function run(args, services) {
  const { interactive, ask, write, waitForGitHub, git, files, github, testTooling } = services;
  let publishedTag;
  let attemptedTag;
  let repository;
  try {
    if (args.length === 1 && args[0] === '--help') {
      write(`${usage}\nRuns the Node tooling tests, checks that a manual GitHub run passed every test on the commit (waiting for it, or starting it, when needed), asks before publishing, downloads the installer, and guides manual checks.\n`);
      return { code: 0, stderr: '' };
    }
    if (args.length > 1 || (args.length === 1 && !parts.includes(args[0]))) throw new Error(usage);
    if (!interactive) throw new Error('Run the release command in an interactive terminal, or use the VS Code "release a version" task.');
    const part = args[0] ?? ((await ask(`Version part (${parts.join('/')}; Enter for ${defaultPart}): `)).trim() || defaultPart);
    if (!parts.includes(part)) throw new Error(usage);

    waitForGitHub('Waiting for GitHub to check your sign-in and origin repository. If sign-in is needed, run gh auth login in another terminal.\n');
    repository = await github.connect();
    write('Running the Node tooling tests.\n');
    await testTooling();

    let preview;
    for (;;) {
      preview = await tagCommand([part, '--dry-run'], git, files);
      if (preview.code === 0) break;
      write(preview.stderr);
      const answer = await ask('Fix the problem in another terminal: review/edit files, commit and push as needed. Enter retries; anything else cancels: ');
      if (answer !== '') {
        write('Cancelled. No version tag was pushed.\n');
        return { code: 0, stderr: '' };
      }
    }

    write(preview.stdout);
    const approval = approvalOf(preview.stdout);
    const branch = await git.defaultBranch();
    // Every .NET test runs in the cloud, in a manual run of the build workflow on exactly the commit the tag will name.
    waitForGitHub(`Waiting for GitHub to look for a manual run of every test on ${approval.commit}.\n`);
    const full = await github.fullRun(approval.commit);
    const watchRun = async (manual) => {
      write(`${manual.url}\n`);
      waitForGitHub(`Waiting for GitHub to finish the manual run of every test on ${approval.commit}.\n`);
      await github.watch(manual);
    };
    // Each state of the manual run says whether publishing needs the user's yes; true means the run already passed.
    const fullRunStates = {
      passed: async ({ run: manual }) => {
        write(`The manual run of every test on ${approval.commit} already passed: ${manual.url}\nPublishing happens automatically.\n`);
        return true;
      },
      running: async ({ run: manual }) => {
        write(`The manual run of every test on ${approval.commit} is not finished yet, so the command waits for it.\n`);
        await watchRun(manual);
        return false;
      },
      none: async () => {
        write(`No manual run of every test exists for ${approval.commit}, so the command starts one on ${branch}.\n`);
        waitForGitHub(`Waiting for GitHub to start a manual run of every test on ${branch}.\n`);
        const started = await github.startFullRun(branch);
        if (started.headSha !== approval.commit) {
          throw new Error(`The manual run GitHub started on ${branch} is for commit ${started.headSha}, not ${approval.commit} (${started.url}); ${branch} moved. Run the release command to preview again. Nothing was tagged.`);
        }
        await watchRun(started);
        return false;
      },
      failed: async ({ run: manual }) => {
        throw new Error(`The manual run of every test on ${approval.commit} did not pass; inspect ${manual.url}. Fix the problem, then start a new manual run yourself with gh workflow run build.yml --ref ${branch}; the release command never starts another after a failure. Nothing was tagged.`);
      },
    };
    const alreadyPassed = await fullRunStates[full.state](full);
    waitForGitHub(`Waiting for GitHub to list earlier workflows for ${approval.tag}.\n`);
    const previous = (await github.runs(approval.tag, approval.commit)).map((job) => job.databaseId);

    if (!alreadyPassed) {
      const yes = await ask(`Publishing ${approval.tag} pushes its tag and makes an installer available on GitHub. Type exactly yes to publish: `);
      if (yes !== 'yes') {
        write('Cancelled. No version tag was pushed.\n');
        return { code: 0, stderr: '' };
      }
    }
    write(`Publishing the approved tag. If interrupted, inspect ${repository.url}/actions before trying again.\n`);
    await github.ensureOrigin();
    const publishingGit = { ...git, pushTag: (name, id) => git.pushTag(name, id, repository.pushUrl) };
    attemptedTag = approval.tag;
    const made = await tagCommand(approval.args, publishingGit, files);
    if (made.code !== 0) throw new Error(made.stderr);
    publishedTag = approval.tag;
    write(made.stdout);
    write(`${repository.url}/releases/tag/${publishedTag}\n`);

    waitForGitHub(`Waiting for GitHub to start the workflow for ${publishedTag}.\n`);
    const job = await github.waitForRun(publishedTag, approval.commit, previous);
    write(`${job.url}\n`);
    waitForGitHub(`Waiting for GitHub to finish the workflow for ${publishedTag}.\n`);
    await github.watch(job);
    waitForGitHub(`Waiting for GitHub to fetch the release for ${publishedTag}.\n`);
    const release = await github.release(publishedTag);
    const installer = installerOf(release.assets);
    waitForGitHub(`Waiting for GitHub to download the installer ${installer.name}.\n`);
    const path = await github.download(publishedTag, installer.name);
    write(`Published: ${release.url}\nInstaller: ${path}\n`);
    write('Manual step: the installer is unsigned, so Windows may warn. Inspect the downloaded file before running it. The command never launches it.\n');
    const version = (await tagCommand(['--check', publishedTag], git, files)).stdout.trim();
    const verified = await ask(`Run the installer. Check the tray icon, version ${version} in Task View, creating/editing a test task, restart, and sync with a test repository. Type yes after these manual checks pass: `);
    if (verified !== 'yes') throw new Error('The release is already published; manual installation checks are not confirmed. Inspect it and fix forward if needed.');
    write(`Release ${publishedTag} is published. You confirmed the manual checks.\n`);
    return { code: 0, stderr: '' };
  } catch (error) {
    const tag = publishedTag ?? attemptedTag;
    const recovery = tag
      ? ` The tag ${tag} may remain; a release may already be public, even if a push was interrupted. Inspect ${repository.url}/actions and ${repository.url}/releases/tag/${tag}. Do not retag blindly; fix forward or review clearing the tag and deleting the release separately.`
      : '';
    return { code: 1, stderr: `${messageOf(error)}${recovery}\n` };
  }
}

export function createTerminalOutput(terminal, output, signal) {
  return {
    ask: (question) => terminal.question(styleText('yellow', question, { stream: output }), { signal }),
    write: (text) => output.write(text),
    waitForGitHub: (text) => output.write(styleText('cyan', text, { stream: output })),
  };
}

export function createCommands(cwd, signal) {
  return (file, args, { live = false, input = '' } = {}) => new Promise((resolve, reject) => {
    let outcome;
    const child = execFile(file, args, {
      cwd, encoding: 'utf8', signal,
      timeout: live ? liveTimeoutMs : commandTimeoutMs,
      maxBuffer: 8 * 1024 * 1024,
      env: { ...process.env, GH_PROMPT_DISABLED: '1', GH_PAGER: 'cat', GIT_TERMINAL_PROMPT: '0' },
    }, (error, stdout, stderr) => {
      outcome = { error, stdout, stderr };
    });
    child.once('close', () => {
      if (!outcome) reject(new Error(`${basename(file)} closed without reporting its result.`));
      else if (outcome.error) reject(new Error(`${basename(file)} ${args.slice(0, 2).join(' ')} failed: ${outcome.stderr.trim() || outcome.error.message}`));
      else resolve(outcome.stdout);
    });
    child.stdin.end(input);
    if (live) {
      child.stdout.on('data', (text) => process.stdout.write(text));
      child.stderr.on('data', (text) => process.stderr.write(text));
    }
  });
}

function repositoryOf(address) {
  const text = address.trim();
  const normalized = text.startsWith('git@github.com:') ? `ssh://git@github.com/${text.slice('git@github.com:'.length)}` : text;
  let url;
  try {
    url = new URL(normalized);
  } catch {
    throw new Error('origin must use a GitHub HTTPS or SSH URL on github.com.');
  }
  const segments = url.pathname.replace(/^\//, '').replace(/\.git$/, '').split('/');
  if (url.hostname !== 'github.com' || !['https:', 'ssh:'].includes(url.protocol) || url.port || url.search || url.hash
    || segments.length !== 2 || segments.some((segment) => !/^[a-zA-Z0-9_.-]+$/.test(segment) || segment === '.' || segment === '..')) {
    throw new Error('origin must name one repository on github.com using HTTPS or SSH.');
  }
  return segments.join('/');
}

function installerOf(assets) {
  const installers = assets.filter((asset) => typeof asset?.name === 'string' && /-Setup\.exe$/i.test(asset.name));
  if (installers.length !== 1 || !(installers[0].size > 0) || basename(installers[0].name) !== installers[0].name) {
    throw new Error('The release must contain exactly one nonempty installer.');
  }
  return installers[0];
}

export function createGitHub(execute, clock = { now: Date.now, wait: setTimeout }) {
  let repository;
  const target = () => {
    if (!repository) throw new Error('Check the GitHub connection before using the release boundary.');
    return repository.nameWithOwner;
  };
  const json = async (args) => JSON.parse(await execute('gh', args));
  const readOrigin = async () => {
    const name = repositoryOf(await execute('git', ['remote', 'get-url', 'origin']));
    const destinations = (await execute('git', ['remote', 'get-url', '--push', '--all', 'origin'])).trim().split(/\r?\n/);
    if (destinations.length !== 1 || repositoryOf(destinations[0]).toLowerCase() !== name.toLowerCase()) {
      throw new Error('origin must push to exactly the same GitHub repository it fetches.');
    }
    return { name, pushUrl: destinations[0] };
  };
  const github = {
    async connect() {
      const { name, pushUrl } = await readOrigin();
      try {
        await execute('gh', ['auth', 'status', '--hostname', 'github.com']);
      } catch (error) {
        throw new Error(`${messageOf(error)} Install GitHub CLI if missing, then run gh auth login and retry.`);
      }
      const info = await json(['repo', 'view', name, '--json', 'nameWithOwner,url']);
      if (typeof info?.nameWithOwner !== 'string' || info.nameWithOwner.toLowerCase() !== name.toLowerCase()
        || info.url !== `https://github.com/${info.nameWithOwner}`) throw new Error('GitHub returned an unexpected origin repository.');
      repository = { nameWithOwner: info.nameWithOwner, url: info.url, pushUrl };
      return repository;
    },
    async ensureOrigin() {
      const origin = await readOrigin();
      if (origin.name.toLowerCase() !== target().toLowerCase()) throw new Error('The origin repository changed; restart the release command.');
    },
    async runs(ref, commit) {
      const jobs = await json(['run', 'list', '--repo', target(), '--workflow', 'build.yml', '--commit', commit, '--event', 'push', '--limit', '100',
        '--json', 'databaseId,headBranch,headSha,status,conclusion,url']);
      if (!Array.isArray(jobs) || jobs.some((job) => !Number.isSafeInteger(job?.databaseId) || typeof job.headBranch !== 'string'
        || typeof job.headSha !== 'string' || typeof job.status !== 'string' || typeof job.url !== 'string')) {
        throw new Error('GitHub returned an invalid workflow response.');
      }
      return jobs.filter((job) => job.headBranch === ref && job.headSha === commit).sort((a, b) => b.databaseId - a.databaseId);
    },
    async waitForRun(ref, commit, previous) {
      const deadline = clock.now() + runWaitMs;
      for (;;) {
        const jobs = (await github.runs(ref, commit)).filter((job) => !previous?.includes(job.databaseId));
        if (previous && jobs.length > 1) throw new Error(`Multiple new workflows match ${ref}; inspect ${repository.url}/actions.`);
        if (jobs.length) return jobs[0];
        if (clock.now() >= deadline) throw new Error(`No workflow appeared for ${ref}; inspect ${repository.url}/actions and check Actions permissions.`);
        await clock.wait(pollMs);
      }
    },
    fullRun: (commit) => checkFullRun(commit, createSource(execute, target())),
    async startFullRun(branch) {
      const body = await execute('gh', ['api', '-X', 'POST', `repos/${target()}/actions/workflows/build.yml/dispatches`, '-f', `ref=${branch}`, '-F', 'return_run_details=true']);
      const id = body.trim() ? JSON.parse(body)?.workflow_run_id : undefined;
      if (!Number.isSafeInteger(id)) throw new Error(`GitHub did not say which run it started on ${branch}; inspect ${repository.url}/actions and do not start another blindly.`);
      return createSource(execute, target()).run(id);
    },
    async watch(job) {
      await execute('gh', ['run', 'watch', String(job.databaseId), '--repo', target(), '--exit-status'], { live: true });
      const result = await json(['run', 'view', String(job.databaseId), '--repo', target(), '--json', 'status,conclusion']);
      if (result?.status !== 'completed' || result.conclusion !== 'success') {
        throw new Error(`Workflow ${job.databaseId} failed or did not finish successfully (${result?.conclusion}); inspect ${job.url}.`);
      }
    },
    async release(tag) {
      const result = await json(['release', 'view', tag, '--repo', target(), '--json', 'tagName,isDraft,isPrerelease,url,assets']);
      if (result?.tagName !== tag || result.isDraft !== false || result.isPrerelease !== false
        || typeof result.url !== 'string' || !Array.isArray(result.assets)) throw new Error(`The release for ${tag} is missing, draft, prerelease, or invalid.`);
      installerOf(result.assets);
      return result;
    },
    async download(tag, name) {
      if (basename(name) !== name || /[/\\]/.test(name)) throw new Error('The installer must be a file name, not a path.');
      const directory = await mkdtemp(join(tmpdir(), 'memoria-release-'));
      try {
        await execute('gh', ['release', 'download', tag, '--repo', target(), '--pattern', name, '--dir', directory]);
        const path = join(directory, name);
        const info = await stat(path);
        if (!info.isFile() || info.size === 0) throw new Error('The downloaded installer is missing or empty.');
        return path;
      } catch (error) {
        await rm(directory, { recursive: true, force: true });
        throw error;
      }
    },
  };
  return github;
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const controller = new AbortController();
  const terminal = createInterface({ input: process.stdin, output: process.stdout });
  terminal.on('SIGINT', () => controller.abort());
  const execute = createCommands(root, controller.signal);
  try {
    const result = await run(process.argv.slice(2), {
      interactive: Boolean(process.stdin.isTTY && process.stdout.isTTY),
      ...createTerminalOutput(terminal, process.stdout, controller.signal),
      git: createGit(root, execute),
      files: createFiles(root),
      github: createGitHub(execute),
      testTooling: async () => {
        const directory = join(root, '.agents');
        const tests = (await readdir(directory)).filter((name) => name.endsWith('.test.mjs')).sort().map((name) => join(directory, name));
        if (!tests.length) throw new Error('No Node tooling tests were found.');
        await execute(process.execPath, ['--test', ...tests], { live: true });
      },
    });
    if (result.stderr) process.stderr.write(result.stderr);
    process.exitCode = result.code;
  } finally {
    terminal.close();
  }
}
