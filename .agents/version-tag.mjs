#!/usr/bin/env node
// Makes the next version tag and pushes it, clears a version tag, or checks a tag's form, as
// docs/architecture/version-tags.md describes. It needs only Node.js's own modules, so the release job runs it as is.
// Usage: node .agents/version-tag.mjs [major|minor|patch] [--dry-run | --expect <tag> --commit <id>]
//        node .agents/version-tag.mjs --clear [<tag>] [--dry-run | --here <id> --origin <id>]
//        node .agents/version-tag.mjs --check <tag>
//        node .agents/version-tag.mjs --notes <tag>
import { execFile } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const script = fileURLToPath(import.meta.url);
const usage = 'usage: node .agents/version-tag.mjs [major|minor|patch] [--dry-run | --expect <tag> --commit <id>] | --clear [<tag>] [--dry-run | --here <id> --origin <id>] | --check <tag> | --notes <tag>';
export const parts = Object.freeze(['major', 'minor', 'patch']);
export const defaultPart = 'minor';
const versionTag = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const commitId = /^([0-9a-f]{40}|[0-9a-f]{64})$/;
const command = (...words) => ['node', '.agents/version-tag.mjs', ...words].join(' ');

/** A problem that makes the script refuse; its message is the one error line. */
class Refusal extends Error {}
const refuse = (message) => { throw new Refusal(message); };

const versionOf = (tag) => versionTag.exec(tag)?.slice(1).map(BigInt);
function clearTagOf(input) {
  let tag = input.startsWith('v') ? input : `v${input}`;
  if (tag.split('.').length === 2) tag += '.0';
  if (versionTag.exec(tag)?.[0] !== tag) refuse(`"${input}" is not a version tag; use [v]<major>.<minor>[.<patch>], such as 1.0 or v1.0.0`);
  return tag;
}

const compare = (a, b) => {
  for (let i = 0; i < 3; i++) if (a[i] !== b[i]) return a[i] < b[i] ? -1 : 1;
  return 0;
};

const flagOptions = ['--clear', '--dry-run'];
const valueOptions = { '--expect': versionTag, '--commit': commitId, '--here': commitId, '--origin': commitId };

function parseArguments(args) {
  if (args.includes('--notes')) {
    if (args.length !== 2 || args[0] !== '--notes') refuse(`--notes takes exactly one tag; ${usage}`);
    return { command: 'notes', tag: args[1] };
  }
  if (args.includes('--check')) {
    if (args.length !== 2 || args[0] !== '--check') refuse(`--check takes exactly one tag; ${usage}`);
    return { command: 'check', tag: args[1] };
  }
  const flags = new Set();
  const values = new Map();
  const words = [];
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (!arg.startsWith('--')) {
      words.push(arg);
    } else if (arg in valueOptions) {
      const value = args[++i];
      if (values.has(arg)) refuse(`${arg} is given twice; ${usage}`);
      if (value === undefined || !valueOptions[arg].test(value)) refuse(`${arg} needs ${arg === '--expect' ? 'a version tag' : 'a full lowercase commit id of 40 or 64 characters'}; ${usage}`);
      values.set(arg, value);
    } else if (flagOptions.includes(arg)) {
      if (flags.has(arg)) refuse(`${arg} is given twice; ${usage}`);
      flags.add(arg);
    } else {
      refuse(`unknown option ${arg}; ${usage}`);
    }
  }
  const dryRun = flags.has('--dry-run');
  const [expect, commit, here, origin] = ['--expect', '--commit', '--here', '--origin'].map((option) => values.get(option));
  if ((expect === undefined) !== (commit === undefined)) refuse(`--expect and --commit go together; ${usage}`);
  if (dryRun && values.size) refuse(`--dry-run can't be combined with an approval option; ${usage}`);

  if (flags.has('--clear')) {
    if (expect !== undefined) refuse(`--expect and --commit are for making a tag; ${usage}`);
    if (words.length > 1 || parts.includes(words[0])) refuse(`--clear takes at most one tag; ${usage}`);
    const tag = words[0] === undefined ? undefined : clearTagOf(words[0]);
    if ((here !== undefined || origin !== undefined) && words.length === 0) refuse(`--here and --origin need the tag to clear; ${usage}`);
    return { command: 'clear', tag, dryRun, here, origin };
  }
  if (here !== undefined || origin !== undefined) refuse(`--here and --origin are for --clear; ${usage}`);
  if (words.length > 1 || (words.length === 1 && !parts.includes(words[0]))) refuse(`give at most one of major, minor, patch; ${usage}`);
  return { command: 'make', part: words[0] ?? defaultPart, dryRun, expect, commit };
}

export function approvalOf(preview) {
  const line = preview.trimEnd().split('\n').at(-1);
  const prefix = command('');
  if (!line?.startsWith(prefix)) refuse('the preview has no approved version-tag command');
  const args = line.slice(prefix.length).split(' ');
  const parsed = parseArguments(args);
  if (parsed.command !== 'make' || parsed.dryRun || !parsed.expect) refuse('the preview has no guarded make command');
  return { args, tag: parsed.expect, commit: parsed.commit };
}

function check(tag) {
  const version = versionOf(tag);
  if (!version) refuse(`"${tag}" is not a version tag; use the form v<major>.<minor>.<patch>, such as v1.2.3`);
  return `${version.join('.')}\n`;
}

/**
 * Finds the notes of `version`, such as '1.3.0', in the text of CHANGELOG.md (or undefined for none), as docs/contributing/changelog.md shapes it.
 * Returns the text of its one `## <version>` section, trimmed of blank lines at both ends, and whether `## Unreleased` still has entries.
 * Refuses, naming the version and the fix, when there is no such section, it is empty, or it appears twice.
 */
function notesOf(changelog, version) {
  const sections = [];
  const unreleased = [];
  let current;
  for (const line of (changelog ?? '').split(/\r?\n/)) {
    const heading = /^## (.+?)\s*$/.exec(line)?.[1];
    if (heading === undefined) current?.push(line);
    else if (heading === 'Unreleased') current = unreleased;
    else current = heading === version ? sections[sections.push([]) - 1] : undefined;
  }
  const hasUnreleased = unreleased.some((line) => line.trim());
  const where = 'then commit and push';
  if (!sections.length) {
    refuse(hasUnreleased
      ? `CHANGELOG.md has no "## ${version}" section; rename "## Unreleased" to "## ${version}", ${where}`
      : `CHANGELOG.md has no "## ${version}" section; add it with what changed for users, or one line saying nothing users notice changed, ${where}`);
  }
  const notes = sections[0].join('\n').replace(/^(?:[ \t]*\n)+/, '').trimEnd();
  if (sections.length > 1 || !notes) refuse(`CHANGELOG.md's "## ${version}" section is empty or appears twice; write its entries in one "## ${version}" section, ${where}`);
  return { notes, hasUnreleased };
}

const versionTagsOf = (...maps) => [...new Set(maps.flatMap((map) => [...map.keys()]))].filter((tag) => versionTag.test(tag));
const latestOf = (tags) => tags.reduce((best, tag) => (!best || compare(versionOf(tag), versionOf(best)) > 0 ? tag : best), undefined);

function next(latest, part) {
  if (!latest) return 'v1.0.0';
  const [major, minor, patch] = versionOf(latest);
  return { major: `v${major + 1n}.0.0`, minor: `v${major}.${minor + 1n}.0`, patch: `v${major}.${minor}.${patch + 1n}` }[part];
}

/** Fetches origin's default branch alone and returns its name. */
async function fetchDefaultBranch(git) {
  const branch = await git.defaultBranch();
  await git.fetch(branch);
  return branch;
}

async function checkReadyToTag(git, branch, local, remote) {
  const current = await git.currentBranch();
  if (current === undefined) refuse(`HEAD is not on a branch; check out ${branch}`);
  if (current !== branch) refuse(`you are on ${current}; check out ${branch}, origin's default branch`);
  if (!(await git.isClean())) refuse('the working tree has uncommitted changes; commit or discard them');

  const { ahead, behind } = await git.aheadBehind(branch);
  if (ahead && behind) refuse(`${branch} has diverged from origin/${branch}: ${ahead} ahead and ${behind} behind`);
  if (behind) refuse(`${branch} is ${behind} behind origin/${branch}; update it first`);
  if (ahead) refuse(`${branch} is ${ahead} ahead of origin/${branch}; push or discard those commits first`);

  const mismatched = versionTagsOf(local).filter((tag) => remote.get(tag)?.commit !== local.get(tag).commit);
  if (mismatched.length) refuse(`version tags missing on origin or pointing to another commit there: ${mismatched.join(', ')}; fix or clear them`);
}

async function make(git, { part, dryRun, expect, commit: approvedCommit }) {
  const branch = await fetchDefaultBranch(git);
  const [local, remote] = [await git.localTags(), await git.remoteTags()];
  await checkReadyToTag(git, branch, local, remote);
  const tag = next(latestOf(versionTagsOf(local, remote)), part);
  const commit = await git.originCommit(branch);
  if (expect !== undefined && (tag !== expect || commit !== approvedCommit)) {
    refuse(`${part} would now make ${tag} on ${commit}, not the approved ${expect} on ${approvedCommit}; nothing was changed, preview again`);
  }
  const version = tag.slice(1);
  const { hasUnreleased } = notesOf(await git.changelogAt(commit), version);
  if (hasUnreleased) refuse(`CHANGELOG.md still has entries under "## Unreleased" besides "## ${version}"; move them under "## ${version}", then commit and push`);

  if (dryRun) {
    const { shortId, subject } = await git.commitInfo(commit);
    return `Would make ${tag} on ${shortId} ${subject} and push it to origin.\n${command(part, '--expect', tag, '--commit', commit)}\n`;
  }
  const id = await git.createTag(tag, commit, `Version ${tag}`);
  try {
    await git.pushTag(tag, id);
  } catch (error) {
    try {
      await git.deleteLocalTag(tag, id);
    } catch (cleanupError) {
      throw new Refusal(`${error.message}\nThe local tag ${tag} was kept because cleanup failed: ${cleanupError.message}`);
    }
    throw error;
  }
  return `Made ${tag} and pushed it to origin.\n`;
}

async function describeCommit(git, id) {
  const info = await git.commitInfo(id);
  return info ? `${info.shortId} ${info.subject}` : `${id}, a commit that isn't here`;
}

async function describePlaces(git, tag, places) {
  if (new Set(places.map(([, id]) => id)).size === 1) return `${tag} (${await describeCommit(git, places[0][1])}) ${places.map(([where]) => where).join(' and ')}`;
  const each = await Promise.all(places.map(async ([where, id]) => `${where} at ${await describeCommit(git, id)}`));
  return `${tag}: ${each.join(', ')}`;
}

async function deleteOnOrigin(git, tag, id) {
  try {
    await git.deleteRemoteTag(tag, id);
  } catch (error) {
    // Reading origin's tags again tells why, whatever git's wording or language.
    const now = await git.remoteTags().then((tags) => tags.get(tag), () => ({ id }));
    if (now?.id === id) throw error;
    refuse(`${tag} changed on origin since the check; no tag was deleted; preview again`);
  }
}

async function deleteHere(git, tag, id) {
  try {
    await git.deleteLocalTag(tag, id);
  } catch (error) {
    // Reading the tags again tells why, whatever git's wording: a git setting can make the push delete the tag here first.
    const now = (await git.localTags()).get(tag);
    if (!now) return;
    if (now.id === id) throw error;
    refuse(`${tag} changed here since the check and was kept; clear again, or preview again`);
  }
}

async function clear(git, { tag: named, dryRun, here, origin }) {
  await fetchDefaultBranch(git);
  const [local, remote] = [await git.localTags(), await git.remoteTags()];
  const tag = named ?? latestOf(versionTagsOf(local, remote));
  if (!tag) refuse('there is no version tag to clear');
  if (!local.has(tag) && !remote.has(tag)) refuse(`${tag} is not here or on origin`);
  const found = { here: local.get(tag)?.commit, origin: remote.get(tag)?.commit };
  if ((here !== undefined || origin !== undefined) && (found.here !== here || found.origin !== origin)) {
    refuse(`${tag} is no longer in exactly the places and on the commits approved; nothing was changed, preview again`);
  }

  if (dryRun) {
    const places = [['here', found.here], ['on origin', found.origin]].filter(([, id]) => id !== undefined);
    const options = [['--here', found.here], ['--origin', found.origin]].filter(([, id]) => id !== undefined).flat();
    return `Would delete ${await describePlaces(git, tag, places)}.\n${command('--clear', tag, ...options)}\n`;
  }
  let done = '';
  // Origin first: when it refuses, the tag here stays.
  if (remote.has(tag)) {
    await deleteOnOrigin(git, tag, remote.get(tag).id);
    done += `Deleted ${tag} on origin.\n`;
  }
  if (local.has(tag)) {
    await deleteHere(git, tag, local.get(tag).id);
    done += `Deleted ${tag} here.\n`;
  }
  return done;
}

/** Runs one command line against `git` and `files` (see createGit and createFiles for their operations) and returns what to print and the exit code. */
export async function run(args, git, files) {
  try {
    const parsed = parseArguments(args);
    let stdout;
    if (parsed.command === 'check') stdout = check(parsed.tag);
    else if (parsed.command === 'notes') stdout = `${notesOf(await files.readChangelog(), check(parsed.tag).trim()).notes}\n`;
    else stdout = await (parsed.command === 'clear' ? clear : make)(git, parsed);
    return { code: 0, stdout, stderr: '' };
  } catch (error) {
    const message = String(error.message).trim().replace(/\s*\n\s*/g, ' ');
    return { code: 1, stdout: '', stderr: `${message}\n` };
  }
}

/** The real git operations, run in `cwd`. A failed operation rejects with git's message. */
export function createGit(cwd, execute) {
  const gitWithInput = (input, ...args) => execute ? execute('git', args, { input }) : new Promise((resolve, reject) => {
    const child = execFile('git', args, { cwd, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, env: { ...process.env, GIT_TERMINAL_PROMPT: '0' } }, (error, stdout, stderr) => {
      if (error) reject(new Error(stderr.trim() || error.message));
      else resolve(stdout);
    });
    child.stdin.end(input);
  });
  const git = (...args) => gitWithInput('', ...args);
  const rows = (text) => text.split('\n').filter(Boolean).map((line) => line.split('\t'));

  return {
    // Tags and pruning are off, so a fetch updates only this branch, whatever the git settings say.
    fetch: (branch) => git('fetch', '--no-tags', '--no-prune', '--refmap=', 'origin', `+refs/heads/${branch}:refs/remotes/origin/${branch}`),
    async defaultBranch() {
      const match = /^ref: refs\/heads\/(.+)\tHEAD$/m.exec(await git('ls-remote', '--symref', 'origin', 'HEAD'));
      if (!match) throw new Error("origin doesn't report a default branch");
      return match[1];
    },
    async currentBranch() {
      return (await git('branch', '--show-current')).trim() || undefined;
    },
    async isClean() {
      return (await git('status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none')).trim() === '';
    },
    async aheadBehind(branch) {
      const [ahead, behind] = (await git('rev-list', '--left-right', '--count', `refs/heads/${branch}...refs/remotes/origin/${branch}`)).trim().split(/\s+/).map(Number);
      return { ahead, behind };
    },
    async originCommit(branch) {
      return (await git('rev-parse', '--verify', `refs/remotes/origin/${branch}^{commit}`)).trim();
    },
    async localTags() {
      // %(*objectname) is empty for a lightweight tag, whose own ID is its commit's.
      const listing = await git('for-each-ref', '--format=%(refname)%09%(objectname)%09%(*objectname)', 'refs/tags');
      return new Map(rows(listing).map(([ref, id, peeled]) => [ref.slice('refs/tags/'.length), { id, commit: peeled || id }]));
    },
    async remoteTags() {
      // An annotated tag has a second line, "<tag>^{}", with its commit; a lightweight tag's own ID is its commit's.
      const tags = new Map();
      for (const [id, ref] of rows(await git('ls-remote', '--tags', 'origin'))) {
        const name = ref.slice('refs/tags/'.length);
        if (name.endsWith('^{}')) tags.get(name.slice(0, -3)).commit = id;
        else tags.set(name, { id, commit: id });
      }
      return tags;
    },
    // ls-tree tells a missing file from a failed git: it prints nothing for the first and rejects for the second, whatever git's language.
    async changelogAt(commit) {
      if (!(await git('ls-tree', '--name-only', commit, '--', 'CHANGELOG.md')).trim()) return undefined;
      return git('show', `${commit}:CHANGELOG.md`);
    },
    async commitInfo(id) {
      try {
        await git('cat-file', '-e', `${id}^{commit}`);
      } catch {
        return undefined;
      }
      const [shortId, subject] = (await git('log', '-1', '--format=%h%x09%s', id)).trim().split('\t');
      return { shortId, subject };
    },
    // Writes the tag object first, so its ID is known, then points the tag at it; the empty old value makes update-ref refuse an existing tag.
    async createTag(name, commit, message) {
      const tagger = (await git('var', 'GIT_COMMITTER_IDENT')).trim();
      const id = (await gitWithInput(`object ${commit}\ntype commit\ntag ${name}\ntagger ${tagger}\n\n${message}\n`, 'mktag')).trim();
      await git('update-ref', '--no-deref', `refs/tags/${name}`, id, '');
      return id;
    },
    // Pushing the ID sends the tag object that was made, whatever the tag here is by now.
    pushTag: (name, id, destination = 'origin') => git('push', '--no-follow-tags', destination, `${id}:refs/tags/${name}`),
    // update-ref deletes the tag only while its own ID is still `id`, and never what a symbolic tag points to.
    deleteLocalTag: (name, id) => git('update-ref', '--no-deref', '-d', `refs/tags/${name}`, id),
    // The lease makes origin delete the tag only while its own ID is still `id`.
    deleteRemoteTag: (name, id) => git('push', '--no-follow-tags', `--force-with-lease=refs/tags/${name}:${id}`, 'origin', `:refs/tags/${name}`),
  };
}

/** The real files run() reads, in the repository at `root`. A failed read rejects, except a missing file, which is undefined. */
export function createFiles(root) {
  return {
    async readChangelog() {
      try {
        return await readFile(join(root, 'CHANGELOG.md'), 'utf8');
      } catch (error) {
        if (error.code === 'ENOENT') return undefined;
        throw error;
      }
    },
  };
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const root = join(dirname(script), '..');
  const { code, stdout, stderr } = await run(process.argv.slice(2), createGit(root), createFiles(root));
  process.stdout.write(stdout);
  process.stderr.write(stderr);
  process.exitCode = code;
}
