import { createHash, X509Certificate } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdtemp, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createCommands, createGitHubRuns, messageOf } from './github-runs.mjs';
import { checkedVersion, platformOf, versionCodeOf } from './release-platforms.mjs';
import { withCleanup } from './with-cleanup.mjs';

export function validateApk(actual, expected) {
  checkContents(actual);
  const metadata = /^package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'/m.exec(actual.badging);
  if (!metadata) throw new Error('The APK has no readable package metadata.');
  for (const [index, key] of [[1, 'applicationId'], [2, 'versionCode'], [3, 'version']]) {
    if (metadata[index] !== expected[key]) throw new Error(`APK ${key} is ${metadata[index]}, expected ${expected[key]}.`);
  }

  const abi = /^native-code: (.+)$/m.exec(actual.badging)?.[1].trim();
  if (abi !== `'${expected.abi}'`) throw new Error(`The APK must contain only ${expected.abi}.`);
  const certificates = [...actual.signature.matchAll(/^(?:Signer #\d+|V3\.0 Signer:) certificate SHA-256 digest: ([0-9a-f]{64})\s*$/gmi)];
  if (certificates.length !== 1 || certificates[0][1].toLowerCase() !== expected.certificate.toLowerCase()) {
    throw new Error(`The APK certificate is ${certificates.map(match => match[1]).join(', ') || 'missing'}, expected ${expected.certificate}.`);
  }
}

function checkContents(actual) {
  if (!Number.isSafeInteger(actual?.size) || actual.size <= 0) throw new Error('The APK is empty or missing.');
  if (typeof actual.sha256 !== 'string' || !/^[0-9a-f]{64}$/i.test(actual.sha256)) throw new Error('The APK contents have no valid SHA-256.');
}

export function validateUploadedApk(release, expected) {
  checkContents(expected);
  const asset = release?.assets?.[0];
  if (release?.tag_name !== expected.tag || release.draft !== true || release.prerelease !== true
    || !Array.isArray(release.assets) || release.assets.length !== 1 || asset?.name !== expected.name
    || asset.state !== 'uploaded' || asset.size !== expected.size
    || asset.digest !== `sha256:${expected.sha256.toLowerCase()}`) {
    throw new Error('The prerelease draft does not contain exactly the completed, validated APK.');
  }
}

export async function publishApk(file, expected, ports) {
  ports.write(`Validating APK ${file}\n`);
  const actual = await ports.inspect(file);
  validateApk(actual, expected);
  ports.write(`Validated ${expected.applicationId} ${expected.version} (${expected.versionCode}); ABI ${expected.abi}; certificate ${expected.certificate}; APK SHA-256 ${actual.sha256}\n`);
  await ports.createDraft();
  await ports.upload(file);
  ports.write(`Verifying uploaded APK ${file}\n`);
  await ports.verifyUpload(file, actual);
  await ports.publish();
  ports.write(`Published Android prerelease ${expected.version}\n`);
}

async function sha256Of(file) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest('hex');
}

export async function signingCertificate(keystore, alias, jdk, execute) {
  const pem = await execute(join(resolve(jdk), 'bin', 'keytool.exe'), [
    '-J-Dkeystore.type.compat=false', '-exportcert', '-rfc', '-storetype', 'PKCS12',
    '-keystore', resolve(keystore), '-alias', alias, '-storepass:env', 'ANDROID_KEYSTORE_PASSWORD',
  ]);
  return new X509Certificate(pem).fingerprint256.replaceAll(':', '').toLowerCase();
}

export async function inspectApk(file, sdk, jdk, execute) {
  const toolsRoot = join(resolve(sdk), 'build-tools');
  const versions = (await readdir(toolsRoot, { withFileTypes: true })).filter(entry => entry.isDirectory() && /^\d+\.\d+\.\d+$/.test(entry.name));
  versions.sort((a, b) => b.name.localeCompare(a.name, 'en', { numeric: true }));
  if (!versions.length) throw new Error('The private Android SDK has no release build tools.');
  const tools = join(toolsRoot, versions[0].name);
  process.stdout.write(`Inspecting APK with ${tools}\n`);
  const size = (await stat(file)).size;
  const sha256 = await sha256Of(file);
  const badging = await execute(join(tools, 'aapt2.exe'), ['dump', 'badging', file]);
  const signature = await execute(join(resolve(jdk), 'bin', 'java.exe'),
    ['-jar', join(tools, 'lib', 'apksigner.jar'), 'verify', '--verbose', '--print-certs', file]);
  if (sha256 !== await sha256Of(file)) throw new Error('The APK changed while it was being inspected.');
  return { size, badging, signature, sha256 };
}

async function main(args) {
  if (args.length === 2 && args[0] === '--build-properties') {
    const platform = platformOf('android');
    process.stdout.write(JSON.stringify({
      version: checkedVersion(args[1], platform).join('.'), versionCode: versionCodeOf(args[1], platform),
      runtimeIdentifier: platform.runtimeIdentifier,
    }) + '\n');
    return;
  }
  const values = {};
  for (let i = 0; i < args.length; i += 2) {
    if (!['--tag', '--apk', '--keystore', '--sdk', '--jdk'].includes(args[i]) || !args[i + 1] || values[args[i]]) {
      throw new Error('usage: android-apk.mjs --tag <tag> --apk <file> --keystore <file> --sdk <directory> --jdk <directory>');
    }
    values[args[i]] = args[i + 1];
  }
  if (Object.keys(values).length !== 5) throw new Error('Every APK publication argument is required.');
  const platform = platformOf('android');
  const version = checkedVersion(values['--tag'], platform).join('.');
  const versionCode = versionCodeOf(values['--tag'], platform);
  const execute = createCommands(process.cwd());
  if (!process.env.ANDROID_KEY_ALIAS || !process.env.ANDROID_KEYSTORE_PASSWORD || !process.env.GH_TOKEN || !process.env.GITHUB_REPOSITORY) {
    throw new Error('The signing configuration, GitHub token, or repository is missing.');
  }
  const product = /<Product>([^<]+)<\/Product>/.exec(await readFile('Directory.Build.props', 'utf8'))?.[1];
  const applicationId = /<ApplicationId>([^<]+)<\/ApplicationId>/.exec(await readFile(resolve(platform.roots[0]), 'utf8'))?.[1];
  if (!product || !applicationId) throw new Error('The app has no Product or ApplicationId.');
  const certificate = await signingCertificate(values['--keystore'], process.env.ANDROID_KEY_ALIAS, values['--jdk'], execute);
  const github = createGitHubRuns(execute, { repository: process.env.GITHUB_REPOSITORY });
  const notes = await execute('node', ['.agents/version-tag.mjs', '--platform', 'android', '--notes', values['--tag']]);
  let notesDirectory;
  await withCleanup(async () => {
    await publishApk(resolve(values['--apk']), { applicationId, version, versionCode, abi: platform.abi, certificate }, {
      write: text => process.stdout.write(text),
      inspect: file => inspectApk(file, values['--sdk'], values['--jdk'], execute),
      async createDraft() {
        notesDirectory = await mkdtemp(join(tmpdir(), 'memoria-android-notes-'));
        const notesFile = join(notesDirectory, 'notes.md');
        await writeFile(notesFile, notes);
        await execute('gh', ['release', 'create', values['--tag'], '--repo', process.env.GITHUB_REPOSITORY,
          '--verify-tag', '--draft', '--prerelease', '--latest=false', '--title', `${product} Android ${version}`, '--notes-file', notesFile]);
      },
      upload: file => execute('gh', ['release', 'upload', values['--tag'], file, '--repo', process.env.GITHUB_REPOSITORY]),
      verifyUpload: async (file, actual) => validateUploadedApk(await github.release(values['--tag']),
        { tag: values['--tag'], name: basename(file), size: actual.size, sha256: actual.sha256 }),
      publish: () => execute('gh', ['release', 'edit', values['--tag'], '--repo', process.env.GITHUB_REPOSITORY,
        '--draft=false', '--prerelease', '--latest=false']),
    });
  }, async () => { if (notesDirectory) await rm(notesDirectory, { recursive: true, force: true }); });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    process.stderr.write(`${messageOf(error)}\n`);
    process.exitCode = 1;
  }
}
