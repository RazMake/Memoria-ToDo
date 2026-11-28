import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { withCleanup } from './with-cleanup.mjs';

describe('operation cleanup (unit)', () => {
  test('cleans up after success without replacing the result', async () => {
    let cleaned = false;
    const result = await withCleanup(async () => 42, async () => { cleaned = true; });
    assert.equal(cleaned, true);
    assert.equal(result, 42);
  });
  test('cleans up after failure and preserves that failure', async () => {
    let cleaned = false;
    const failure = new Error('Operation failed');
    await assert.rejects(withCleanup(async () => { throw failure; }, async () => { cleaned = true; }), error => error === failure);
    assert.equal(cleaned, true);
  });
  test('fails when cleanup fails after an otherwise successful operation', async () => {
    const failure = new Error('Cleanup failed');
    await assert.rejects(withCleanup(async () => 42, async () => { throw failure; }), error => error === failure);
  });
  test('reports both failures when operation and cleanup fail', async () => {
    const operation = new Error('Operation failed');
    const cleanup = new Error('Cleanup failed');
    await assert.rejects(withCleanup(async () => { throw operation; }, async () => { throw cleanup; }), error => {
      assert.ok(error instanceof AggregateError);
      assert.deepEqual(error.errors, [operation, cleanup]);
      assert.ok(error.message.includes(operation.message));
      assert.ok(error.message.includes(cleanup.message));
      return true;
    });
  });
});
