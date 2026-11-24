#!/usr/bin/env node
// Previews the next version tag, asks once, starts Test then Tag and Release on GitHub, follows it, and guides the manual checks,
// as docs/architecture/releasing.md describes.
//
// run(args, { interactive, ask, write, waitForGitHub, git, files, github, releases, testTooling }) -> Promise<{ code, stderr }>
//   git, files: the version tag script's ports; github: github-runs.mjs's component;
//   releases: { view(tag), download(tag, name) } of createReleases; testTooling(): runs the Node tooling tests.
import { mkdtemp, readdir, rm, stat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join } from 'node:path';
import { createInterface } from 'node:readline/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, createTerminalOutput, messageOf, messageWithRun } from './github-runs.mjs';
import { approvalOf, createFiles, createGit, defaultPart, parts, run as tagCommand } from './version-tag.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const usage = `usage: node .agents\\release.mjs [${parts.join('|')}]`;
const workflowFile = 'release.yml';

// What a failed run leaves, by the job it failed in: the failure table of docs/architecture/releasing.md.
const failureRows = [
  [/^Plan$/, () => 'Left behind: no tag, nothing published. Fix the problem, commit and push, then start a new release.'],
  [/^Tests\b/, () => 'Left behind: no tag, nothing published. Fix the problem, commit and push, then start a new release.'],
  [/^Tag$/, () => 'Left behind: no new tag on origin (the script deletes the tag it made when its push fails). Start a new release.'],
  [/^Publish$/, (tag) => `Left behind: the tag on origin, no published release, and a draft release of it if the upload started. Delete that draft on GitHub, then re-run the failed jobs on GitHub, which re-runs only Publish and keeps the other jobs' results, or clear the tag with node .agents/version-tag.mjs --clear ${tag} and start a new release.`],
];

function installerOf(assets) {
  const installers = assets.filter((asset) => typeof asset?.name === 'string' && /-Setup\.exe$/i.test(asset.name));
  if (installers.length !== 1 || !(installers[0].size > 0) || basename(installers[0].name) !== installers[0].name) {
    throw new Error('The release must contain exactly one nonempty installer.');
  }
  return installers[0];
}

function installerOfRelease(release, tag) {
  if (release?.tagName !== tag || release.isDraft !== false || release.isPrerelease !== false
    || typeof release.url !== 'string' || !Array.isArray(release.assets)) throw new Error(`The release for ${tag} is missing, draft, prerelease, or invalid.`);
  return installerOf(release.assets);
}

async function failureReport({ run, tag, git, github }) {
  const failed = await github.failedJobs(run);
  const onOrigin = (await git.remoteTags()).has(tag);
  const row = failureRows.findLast(([stage]) => failed.some((job) => stage.test(job.name)))?.[1](tag);
  return [
    `The release run ended with ${run.conclusion}: ${run.url}`,
    ...failed.map((job) => `- ${job.name}: ${job.url}`),
    `${tag} ${onOrigin ? 'exists' : 'does not exist'} on origin.`,
    ...(row ? [row] : []),
  ].join('\n');
}

export async function run(args, { interactive, ask, write, waitForGitHub, git, files, github, releases, testTooling }) {
  try {
    if (args.length === 1 && args[0] === '--help') {
      write(`${usage}\nRuns the Node tooling tests, previews the next version tag, asks once, starts Test then Tag and Release on GitHub, follows it, downloads the installer, and guides manual checks.\n`);
      return { code: 0, stderr: '' };
    }
    if (args.length > 1 || (args.length === 1 && !parts.includes(args[0]))) throw new Error(usage);
    if (!interactive) throw new Error('Run the release command in an interactive terminal, or use the VS Code "release a version" task.');
    const part = args[0] ?? ((await ask(`Version part (${parts.join('/')}; Enter for ${defaultPart}): `)).trim() || defaultPart);
    if (!parts.includes(part)) throw new Error(usage);

    waitForGitHub('Waiting for GitHub to check your sign-in and origin repository. If sign-in is needed, run gh auth login in another terminal.\n');
    await github.connect();
    write('Running the Node tooling tests.\n');
    await testTooling();

    let preview;
    for (;;) {
      preview = await tagCommand([part, '--dry-run'], git, files);
      if (preview.code === 0) break;
      write(preview.stderr);
      const answer = await ask('Fix the problem in another terminal: review/edit files, commit and push as needed. Enter retries; anything else cancels: ');
      if (answer !== '') {
        write('Cancelled. Nothing was started.\n');
        return { code: 0, stderr: '' };
      }
    }
    write(preview.stdout);
    const { tag, commit } = approvalOf(preview.stdout);
    const branch = await git.defaultBranch();

    const yes = await ask(`Type exactly yes to test, tag, and publish ${tag} from commit ${commit} on GitHub: `);
    if (yes !== 'yes') {
      write('Cancelled. Nothing was started.\n');
      return { code: 0, stderr: '' };
    }

    waitForGitHub(`Waiting for GitHub to start Test then Tag and Release on ${branch}.\n`);
    const started = await github.dispatch(workflowFile, branch, { part, tag, commit });
    write(`${started.url}\n`);
    if (started.headSha !== commit) {
      throw new Error(`${branch} moved: the run is on commit ${started.headSha}, not the previewed ${commit} (${started.url}). Its Plan refuses it, so nothing is tagged. Run the release command again.`);
    }

    waitForGitHub(`Waiting for GitHub to finish the release run of ${tag}.\n`);
    let finished;
    try {
      finished = await github.follow(started);
    } catch (error) {
      throw new Error(messageWithRun(error, started));
    }
    if (finished.conclusion !== 'success') return { code: 1, stderr: `${await failureReport({ run: finished, tag, git, github })}\n` };

    waitForGitHub(`Waiting for GitHub to fetch the release for ${tag}.\n`);
    const release = await releases.view(tag);
    const installer = installerOfRelease(release, tag);
    waitForGitHub(`Waiting for GitHub to download the installer ${installer.name}.\n`);
    const path = await releases.download(tag, installer.name);
    write(`Published: ${release.url}\nInstaller: ${path}\n`);
    write('Manual step: the installer is unsigned, so Windows may warn. Inspect the downloaded file before running it. The command never launches it.\n');
    const version = (await tagCommand(['--check', tag], git, files)).stdout.trim();
    const verified = await ask(`Run the installer. Check the tray icon, version ${version} in Task View, creating/editing a test task, restart, and sync with a test repository. Type yes after these manual checks pass: `);
    if (verified !== 'yes') throw new Error('The release is already published; manual installation checks are not confirmed. Inspect it and fix forward if needed.');
    write(`Release ${tag} is published. You confirmed the manual checks.\n`);
    return { code: 0, stderr: '' };
  } catch (error) {
    return { code: 1, stderr: `${messageOf(error)}\n` };
  }
}

/** Reads and downloads releases of the repository `repoName` ("owner/name") through `gh`. */
export function createReleases(execute, repoName) {
  const repo = `github.com/${repoName}`;
  return {
    async view(tag) {
      return JSON.parse(await execute('gh', ['release', 'view', tag, '--repo', repo, '--json', 'tagName,isDraft,isPrerelease,url,assets']));
    },
    // Each download goes to its own new temporary folder, which is removed again when the download fails.
    async download(tag, name) {
      if (basename(name) !== name || /[/\\]/.test(name)) throw new Error('The installer must be a file name, not a path.');
      const directory = await mkdtemp(join(tmpdir(), 'memoria-release-'));
      try {
        await execute('gh', ['release', 'download', tag, '--repo', repo, '--pattern', name, '--dir', directory]);
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
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const controller = new AbortController();
  const terminal = createInterface({ input: process.stdin, output: process.stdout });
  terminal.on('SIGINT', () => controller.abort());
  const execute = createCommands(root, controller.signal);
  try {
    const github = createGitHubRuns(execute);
    // The releases need the repository name, which github.connect() finds.
    let repoName;
    const connect = github.connect;
    github.connect = async () => {
      const repository = await connect();
      repoName = repository.name;
      return repository;
    };
    const result = await run(process.argv.slice(2), {
      interactive: Boolean(process.stdin.isTTY && process.stdout.isTTY),
      ...createTerminalOutput(terminal, process.stdout, controller.signal),
      git: createGit(root, execute),
      files: createFiles(root),
      github,
      releases: {
        view: (tag) => createReleases(execute, repoName).view(tag),
        download: (tag, name) => createReleases(execute, repoName).download(tag, name),
      },
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
