import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { runPlan } from './platform-tests.mjs';

describe('running a platform test selection (unit)', () => {
  const native = 'node .agents/android-device-tests.mjs --project tests/Android/Tests/Android.Tests.csproj';
  const plan = {
    desktopCheck: false,
    commands: ['dotnet test --project tests/Shared/Tests/Shared.Tests.csproj --filter-namespace "Example.Tests*"', native, 'npm run build --prefix client'],
    extras: [{ command: 'npm run build --prefix client' }],
  };
  test('runs the PC selection without native device commands and builds clients before their tests', async () => {
    const calls = [];
    await runPlan(plan, true, { execute: async (file, args) => calls.push([file, args]), write: () => {} });
    assert.equal(calls.length, 2);
    assert.equal(calls[0][0], 'npm');
    assert.equal(calls[1][0], 'dotnet');
    assert.ok(calls[1][1].includes('Example.Tests*'));
  });
  test('propagates a failed selection instead of continuing as a passing gate', async () => {
    await assert.rejects(runPlan(plan, true, { execute: async () => { throw new Error('Tests failed'); }, write: () => {} }), /Tests failed/);
  });
});
