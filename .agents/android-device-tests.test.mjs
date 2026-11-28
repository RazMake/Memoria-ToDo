import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { deviceConfiguration, deviceFilter, validateResults } from './android-device-tests.mjs';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

describe('Android device test results (unit)', () => {
  const passing = { total: 3, executed: 3, passed: 3, failed: 0, notExecuted: 0, resultCount: 3, passedResultCount: 3, outcome: 'Completed' };
  test('accepts a complete passing report and successful process', () => {
    assert.doesNotThrow(() => validateResults(passing, 0));
  });

  for (const [reason, result, exit] of [
    ['no matching tests', { ...passing, total: 0, executed: 0, passed: 0, resultCount: 0, passedResultCount: 0 }, 0],
    ['a missing report', null, 0],
    ['a failed test', { ...passing, failed: 1, passed: 2, passedResultCount: 2 }, 1],
    ['a skipped test', { ...passing, executed: 2, passed: 2, notExecuted: 1, passedResultCount: 2 }, 0],
    ['missing individual results', { ...passing, resultCount: 2 }, 0],
    ['a failed process despite a passing report', passing, 1],
    ['an interrupted report', { ...passing, outcome: 'Aborted' }, 0],
  ]) {
    test(`rejects ${reason}`, () => assert.throws(() => validateResults(result, exit)));
  }
});

describe('Android device runner configuration (unit)', () => {
  test('isolates VSTest while retaining the repository SDK pin and roll-forward policy', () => {
    const original = { sdk: { version: '11.0.100', workloadVersion: '11.0.100.1', rollForward: 'latestPatch' }, test: { runner: 'Microsoft.Testing.Platform' } };
    const device = deviceConfiguration(original);
    assert.deepEqual(device.sdk, original.sdk);
    assert.equal(device.test.runner, 'VSTest');
    assert.equal(original.test.runner, 'Microsoft.Testing.Platform');
  });
  test('uses the test-kind source and limits native execution to selected modules', () => {
    const kinds = 'TraitName = "Kind"; UxKind = "Screen"; IntegrationKind = "Boundary";';
    assert.equal(deviceFilter(kinds, ['Example.Configure', 'Example.Tasks']), '(Kind=Screen|Kind=Boundary)&(FullyQualifiedName~Example.Configure.|FullyQualifiedName~Example.Tasks.)');
  });
  test('rejects a missing test-kind definition before running the device', () => {
    assert.throws(() => deviceFilter('', []), /test kind/i);
  });
});

describe('reading real device reports (integration)', () => {
  const reader = fileURLToPath(new URL('./android-device-tests/read-trx.ps1', import.meta.url));
  const report = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult outcome="Passed"/></Results><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0"/></ResultSummary></TestRun>';
  const read = text => {
    const directory = mkdtempSync(join(tmpdir(), 'memoria-trx-'));
    try {
      const file = join(directory, 'report.trx');
      if (text !== undefined) writeFileSync(file, text);
      return JSON.parse(execFileSync('pwsh', ['-NoProfile', '-File', reader, file], {
        encoding: 'utf8', timeout: 120000, stdio: ['ignore', 'pipe', 'pipe'],
      }));
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  };
  test('reads and validates an actual namespaced passing TRX report', () => {
    const counts = read(report);
    assert.equal(counts.total, 1);
    assert.equal(counts.resultCount, 1);
    assert.equal(counts.passedResultCount, 1);
    validateResults(counts, 0);
  });
  for (const [reason, text] of [
    ['a missing file', undefined],
    ['malformed XML', '<TestRun>'],
    ['an external entity declaration', `<!DOCTYPE TestRun [<!ENTITY external SYSTEM "file:///not-a-test-resource">]>${report}`],
    ['a nonnumeric counter', report.replace('total="1"', 'total="unknown"')],
  ]) {
    test(`rejects ${reason}`, () => assert.throws(() => read(text)));
  }
});

describe('Android test framework selection (integration)', () => {
  const root = fileURLToPath(new URL('..', import.meta.url));
  for (const [target, framework] of [['pc', 'net11.0'], ['android', 'net11.0-android']]) {
    test(`selects only ${framework} before choosing sources and references`, () => {
      for (const project of ['src\\Android\\Configure\\Memoria.ToDo.Android.Configure.csproj', 'tests\\Android\\Tests\\Memoria.ToDo.Android.Tests.csproj']) {
        const result = JSON.parse(execFileSync('dotnet', ['msbuild', project, `-p:MemoriaAndroidTestTarget=${target}`,
          '-getProperty:TargetFramework,TargetFrameworks,DefaultItemExcludes'], { cwd: root, encoding: 'utf8', timeout: 120000 })).Properties;
        assert.equal(result.TargetFramework, framework);
        assert.equal(result.TargetFrameworks, '');
        assert.equal(result.DefaultItemExcludes.includes('**/*.Android.cs'), target === 'pc');
      }
    });

  }
});

describe('Android hosted toolchain setup (integration)', () => {
  test('creates private emulator homes without overwriting PowerShells built-in HOME', () => {
    const workflow = readFileSync(new URL('../.github/workflows/android-tests.yml', import.meta.url), 'utf8');
    const step = workflow.split('      - name: Install the pinned Android SDK and JDK into private directories')[1]
      .split('      - name:')[0];
    const body = step.match(/run:\s*\|\r?\n((?:          .*(?:\r?\n|$))+)/)[1].replace(/^ {10}/gm, '');
    const directory = mkdtempSync(join(tmpdir(), 'memoria-toolchain-'));
    try {
      const envFile = join(directory, 'environment');
      const source = [
        "$ErrorActionPreference='Stop'",
        'function dotnet { $global:LASTEXITCODE = 0 }',
        body,
        'Get-Content -LiteralPath $env:GITHUB_ENV',
      ].join('\n');
      const output = execFileSync('pwsh', ['-NoProfile', '-Command', source], {
        encoding: 'utf8', timeout: 120000, stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, RUNNER_TEMP: directory, GITHUB_ENV: envFile, EMULATOR_PORT: '5554' },
      });
      assert.ok(output.includes(`ANDROID_USER_HOME=${join(directory, 'memoria-android-user')}`));
      assert.ok(output.includes('DEVICE=emulator-5554'));
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });
});
