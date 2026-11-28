import { mkdir } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, messageOf } from './github-runs.mjs';
import { platformOf, versionPartsOf } from './release-platforms.mjs';

export function latestDesktopRelease(releases) {
  if (!Array.isArray(releases)) throw new Error('GitHub returned an invalid release list.');
  const platform = platformOf();
  const candidates = [];
  for (const release of releases) {
    if (typeof release?.tag_name !== 'string' || typeof release.draft !== 'boolean'
      || typeof release.prerelease !== 'boolean' || !Array.isArray(release.assets)
      || release.assets.some(asset => typeof asset?.name !== 'string')) throw new Error('GitHub returned an invalid release.');
    const version = versionPartsOf(release.tag_name, platform);
    if (!version || release.draft || release.prerelease) continue;
    const names = release.assets.map(asset => asset.name);
    if (!names.includes('releases.win.json') || !names.some(name => /-full\.nupkg$/i.test(name))) continue;
    candidates.push({ version, tag: release.tag_name, assets: names.filter(name => name === 'releases.win.json' || /-(?:full|delta)\.nupkg$/i.test(name)) });
  }
  candidates.sort((a, b) => {
    for (let index = 0; index < 3; index++) if (a.version[index] !== b.version[index]) return a.version[index] > b.version[index] ? -1 : 1;
    return 0;
  });
  const latest = candidates[0];
  return latest && { tag: latest.tag, assets: latest.assets };
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  try {
    if (process.argv.length !== 2) throw new Error('desktop-release-files.mjs takes no arguments.');
    const execute = createCommands(process.cwd());
    const github = createGitHubRuns(execute, { repository: process.env.GITHUB_REPOSITORY });
    if (!process.env.GITHUB_REPOSITORY) await github.connect();
    process.stdout.write('Looking for the previous desktop release across all release pages.\n');
    const latest = latestDesktopRelease(await github.releases());
    if (latest) {
      await mkdir('Releases', { recursive: true });
      await execute('gh', ['release', 'download', latest.tag, '--dir', 'Releases', ...latest.assets.flatMap(name => ['--pattern', name])]);
      process.stdout.write(`Downloaded desktop delta inputs from ${latest.tag}.\n`);
    } else {
      process.stdout.write('No previous desktop release has update files; this is a full package only.\n');
    }
  } catch (error) {
    process.stderr.write(`${messageOf(error)}\n`);
    process.exitCode = 1;
  }
}
