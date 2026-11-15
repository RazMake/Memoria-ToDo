// Tests for desktop-check.mjs. Unit tests feed `decide` made-up reports and `run` a fake probe, platform and arguments.
// Integration tests ask the real Windows through `probeWindows()` and run the script as a real process.
//
// Contract the tests rely on:
//   export function decide(report): { code: 0 | 1 | 2, message: string }
//   export async function run({ args, platform, probe }): Promise<{ code, message }>
//   export async function probeWindows(): Promise<report>
//   Importing the module runs nothing; the script runs only when node starts it directly.
//   Report: { connectState, connectError, inputDesktop, inputDesktopError, cursorError }, each an int, a string or null.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { decide, probeWindows, run } from './desktop-check.mjs';

const script = join(dirname(fileURLToPath(import.meta.url)), 'desktop-check.mjs');

const usable = { connectState: 0, connectError: null, inputDesktop: 'Default', inputDesktopError: null, cursorError: null };
const report = (changes) => ({ ...usable, ...changes });

// ---------- decide ----------

describe('desktop-check decide (unit)', () => {
  test('accepts a connected session whose Default desktop takes input and whose pointer is readable', () => {
    const result = decide(usable);
    assert.equal(result.code, 0);
    assert.equal(typeof result.message, 'string');
  });

  test('says the session is not connected when its connect state is not active', () => {
    const result = decide(report({ connectState: 4 }));
    assert.equal(result.code, 1);
    assert.match(result.message, /connected/i);
    assert.match(result.message, /Remote Desktop/);
    assert.match(result.message, /window open/i);
  });

  test('says the PC is locked when the input desktop cannot be opened', () => {
    const result = decide(report({ inputDesktop: null, inputDesktopError: 5 }));
    assert.equal(result.code, 1);
    assert.match(result.message, /locked/i);
    assert.match(result.message, /unlock/i);
  });

  test('says the PC is locked when the input desktop is not Default', () => {
    const result = decide(report({ inputDesktop: 'Winlogon' }));
    assert.equal(result.code, 1);
    assert.match(result.message, /locked/i);
    assert.match(result.message, /unlock/i);
  });

  test('says the pointer is unavailable when its position cannot be read', () => {
    const result = decide(report({ cursorError: 5 }));
    assert.equal(result.code, 1);
    assert.match(result.message, /pointer/i);
  });

  test('reports the connection first when the connection, the desktop and the pointer all fail', () => {
    const result = decide(report({ connectState: 4, inputDesktop: 'Winlogon', cursorError: 5 }));
    assert.equal(result.code, 1);
    assert.match(result.message, /connected/i);
    assert.doesNotMatch(result.message, /pointer/i);
  });

  test('reports the desktop before the pointer when both fail', () => {
    const result = decide(report({ inputDesktop: null, inputDesktopError: 5, cursorError: 5 }));
    assert.equal(result.code, 1);
    assert.match(result.message, /locked/i);
    assert.doesNotMatch(result.message, /pointer/i);
  });

  test('fails the check itself, with the error, when the session query failed unexpectedly', () => {
    const result = decide(report({ connectState: null, connectError: 1722 }));
    assert.equal(result.code, 2);
    assert.match(result.message, /1722/);
  });

  test('fails the check itself when the session query failed even though other checks would fail too', () => {
    const result = decide(report({ connectState: null, connectError: 1722, cursorError: 5 }));
    assert.equal(result.code, 2);
  });

  for (const [name, bad] of [
    ['is not an object', null],
    ['is a string', 'ok'],
    ['has no fields', {}],
    ['lacks the connect state', { ...usable, connectState: undefined }],
    ['has a connect state that is not a number', report({ connectState: '0' })],
    ['has an input desktop that is not a string', report({ inputDesktop: 7 })],
    ['has a cursor error that is not a number', report({ cursorError: 'denied' })],
  ]) {
    test(`fails the check itself when the report ${name}`, () => {
      const result = decide(bad);
      assert.equal(result.code, 2);
      assert.equal(typeof result.message, 'string');
    });
  }
});

// ---------- run ----------

function fakeProbe(outcome) {
  const probe = async () => {
    probe.calls++;
    if (outcome instanceof Error) throw outcome;
    return outcome;
  };
  probe.calls = 0;
  return probe;
}

describe('desktop-check run (unit)', () => {
  test('refuses any argument with code 2 and a usage message, without asking Windows', async () => {
    const probe = fakeProbe(usable);
    const result = await run({ args: ['--fast'], platform: 'win32', probe });
    assert.equal(result.code, 2);
    assert.match(result.message, /usage/i);
    assert.equal(probe.calls, 0);
  });

  test('exits 1 on a system other than Windows, without asking Windows', async () => {
    const probe = fakeProbe(usable);
    const result = await run({ args: [], platform: 'linux', probe });
    assert.equal(result.code, 1);
    assert.match(result.message, /Windows/);
    assert.equal(probe.calls, 0);
  });

  test('exits 0 when the probe reports a usable desktop', async () => {
    const probe = fakeProbe(usable);
    const result = await run({ args: [], platform: 'win32', probe });
    assert.equal(result.code, 0);
    assert.equal(probe.calls, 1);
  });

  test('passes the probe report through the decision, so a failed check exits 1', async () => {
    const result = await run({ args: [], platform: 'win32', probe: fakeProbe(report({ cursorError: 5 })) });
    assert.equal(result.code, 1);
    assert.match(result.message, /pointer/i);
  });

  test('exits 2 naming the error when the probe throws', async () => {
    const result = await run({ args: [], platform: 'win32', probe: fakeProbe(new Error('powershell.exe not found')) });
    assert.equal(result.code, 2);
    assert.match(result.message, /powershell\.exe not found/);
  });
});

// ---------- real Windows ----------

const isInt = (value) => Number.isInteger(value);
const intOrNull = (value) => value === null || isInt(value);

describe('desktop-check against the real Windows (integration)', () => {
  test('probeWindows returns a well-formed report', async () => {
    const result = await probeWindows();
    assert.deepEqual(Object.keys(result).sort(), ['connectError', 'connectState', 'cursorError', 'inputDesktop', 'inputDesktopError']);
    assert.ok(intOrNull(result.connectState), 'connectState');
    assert.ok(intOrNull(result.connectError), 'connectError');
    assert.ok(result.inputDesktop === null || typeof result.inputDesktop === 'string', 'inputDesktop');
    assert.ok(intOrNull(result.inputDesktopError), 'inputDesktopError');
    assert.ok(intOrNull(result.cursorError), 'cursorError');
  });

  test('probeWindows reports the session as active exactly when Windows lists it as Active', async () => {
    const sessions = spawnSync('query', ['session'], { encoding: 'utf8' }).stdout;
    const current = sessions.split(/\r?\n/).find((line) => line.startsWith('>'));
    assert.ok(current, `no current session in:\n${sessions}`);
    const listedActive = /\sActive\s/.test(`${current} `);
    const result = await probeWindows();
    assert.equal(result.connectState === 0, listedActive, `connectState ${result.connectState}, listed as: ${current}`);
  });

  test('the script prints one line and exits 0 or 1, never 2', () => {
    const result = spawnSync(process.execPath, [script], { encoding: 'utf8' });
    assert.ok(result.status === 0 || result.status === 1, `exit ${result.status}: ${result.stdout}${result.stderr}`);
    const printed = (result.stdout + result.stderr).split('\n').filter((line) => line !== '');
    assert.equal(printed.length, 1, `one line, got: ${result.stdout}${result.stderr}`);
  });

  test('the script exits 2 when given an argument', () => {
    const result = spawnSync(process.execPath, [script, '--fast'], { encoding: 'utf8' });
    assert.equal(result.status, 2, `${result.stdout}${result.stderr}`);
  });
});
