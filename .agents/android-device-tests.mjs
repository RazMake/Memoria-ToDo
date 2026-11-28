import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createCommands, messageOf } from './github-runs.mjs';
import { withCleanup } from './with-cleanup.mjs';

export function deviceConfiguration(config) {
  if (!config?.sdk?.version) throw new Error('The repository has no SDK pin.');
  return { ...config, sdk: { ...config.sdk }, test: { ...config.test, runner: 'VSTest' } };
}

export function deviceFilter(kinds, namespaces, filter) {
  const value = name => {
    const found = new RegExp(`${name} = "([^"]+)"`).exec(kinds)?.[1];
    if (!found) throw new Error(`The test kind ${name} is missing.`);
    return found;
  };
  const trait = value('TraitName');
  const selectedKinds = `(${trait}=${value('UxKind')}|${trait}=${value('IntegrationKind')})`;
  if (filter) return filter;
  if (namespaces.some(name => !/^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$/.test(name))) throw new Error('A device namespace is invalid.');
  return namespaces.length ? `${selectedKinds}&(${namespaces.map(name => `FullyQualifiedName~${name}.`).join('|')})` : selectedKinds;
}

export function validateResults(result, exitCode) {
  const counts = ['total', 'executed', 'passed', 'failed', 'notExecuted', 'resultCount', 'passedResultCount'];
  if (!result || counts.some(key => !Number.isSafeInteger(result[key]) || result[key] < 0)) throw new Error('The device test report is missing or invalid.');
  if (exitCode !== 0 || result.total === 0 || result.total !== result.executed || result.total !== result.passed
    || result.failed !== 0 || result.notExecuted !== 0 || result.resultCount !== result.total
    || result.passedResultCount !== result.total || !['Completed', 'Passed'].includes(result.outcome)) {
    throw new Error(`Device tests did not all pass: ${JSON.stringify(result)}, process exit ${exitCode}.`);
  }
}

async function main(args) {
  const values = { '--namespace': [] };
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i], value = args[i + 1];
    if (!['--project', '--namespace', '--device', '--filter', '--results-directory'].includes(name) || !value
      || (name !== '--namespace' && values[name])) throw new Error('Invalid Android device test arguments.');
    if (name === '--namespace') values[name].push(value);
    else values[name] = value;
  }
  if (!values['--project']) throw new Error('--project is required.');
  const device = values['--device'] ?? process.env.DEVICE;
  if (!device) throw new Error('Select an attached Android device with --device or DEVICE.');
  const root = process.cwd();
  const project = resolve(values['--project']);
  const configuration = deviceConfiguration(JSON.parse(await readFile('global.json', 'utf8')));
  const props = await readFile('Directory.Build.props', 'utf8');
  const minutes = Number(/--hangdump-timeout (\d+)m/.exec(props)?.[1]);
  if (!Number.isSafeInteger(minutes) || minutes <= 0) throw new Error('The repository has no test hang bound.');
  const filter = deviceFilter(await readFile(join(root, 'tests', 'Testing', 'Kinds', 'TestKinds.cs'), 'utf8'),
    values['--namespace'], values['--filter']);
  await mkdir(join(root, 'test-results'), { recursive: true });
  const results = values['--results-directory'] ? resolve(values['--results-directory']) : await mkdtemp(join(root, 'test-results', 'android-'));
  if (values['--results-directory']) await mkdir(results);
  const directory = await mkdtemp(join(tmpdir(), 'memoria-device-runner-'));
  const execute = createCommands(directory);
  const properties = ['-p:MemoriaAndroidTestTarget=android'];
  if (process.env.ANDROID_HOME) properties.push(`-p:AndroidSdkDirectory=${process.env.ANDROID_HOME}`);
  if (process.env.JAVA_HOME) properties.push(`-p:JavaSdkDirectory=${process.env.JAVA_HOME}`);
  let applicationId;
  let started = false;
  await withCleanup(async () => {
    await writeFile(join(directory, 'global.json'), JSON.stringify(configuration));
    const selected = JSON.parse(await execute('dotnet', ['msbuild', project, ...properties,
      '-getProperty:TargetFramework,ApplicationId,MemoriaIsTestProject'])).Properties;
    const framework = selected?.TargetFramework;
    if (!/^net\d+\.\d+-android$/.test(framework)) throw new Error(`The device project selected ${framework}, not Android.`);
    if (selected.MemoriaIsTestProject !== 'true') throw new Error('The selected Android project is not a test host.');
    applicationId = selected.ApplicationId;
    if (!/^[a-zA-Z]\w*(?:\.[a-zA-Z]\w*)+$/.test(applicationId)) throw new Error('The test host has no valid ApplicationId.');
    process.stdout.write(`Building Android test host for ${framework}\n`);
    await execute('dotnet', ['build', project, ...properties, '-f', framework], { live: true });
    const report = join(results, 'test-results.trx');
    const limited = createCommands(directory, AbortSignal.timeout(minutes * 60_000));
    process.stdout.write(`Running ${filter} on ${device}\n`);
    started = true;
    await limited('dotnet', ['test', project, ...properties, '-f', framework, '--no-build',
      `-p:DeviceRunnersDevice=${device}`, '--filter', filter, '--logger', 'trx', '--results-directory', results], { live: true });
    const counts = JSON.parse(await execute('pwsh', ['-NoProfile', '-File', join(root, '.agents', 'android-device-tests', 'read-trx.ps1'), report]));
    validateResults(counts, 0);
    process.stdout.write(`Android device tests passed: ${counts.passed}, skipped: ${counts.notExecuted}. Report: ${report}\n`);
  }, async () => {
    await withCleanup(async () => {
      if (started) {
        process.stdout.write(`Stopping test host ${applicationId} on ${device}\n`);
        const adb = process.env.ANDROID_HOME ? join(process.env.ANDROID_HOME, 'platform-tools', process.platform === 'win32' ? 'adb.exe' : 'adb') : 'adb';
        await execute(adb, ['-s', device, 'shell', 'am', 'force-stop', applicationId]);
      }
    }, () => rm(directory, { recursive: true, force: true }));
  });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    process.stderr.write(`${messageOf(error)}\n`);
    process.exitCode = 1;
  }
}
