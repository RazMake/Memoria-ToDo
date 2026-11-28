import { messageOf } from './github-runs.mjs';

export async function withCleanup(operation, cleanup) {
  let failed = false;
  let failure;
  try {
    return await operation();
  } catch (error) {
    failed = true;
    failure = error;
    throw error;
  } finally {
    try {
      await cleanup();
    } catch (error) {
      if (failed) throw new AggregateError([failure, error], `${messageOf(failure)}; cleanup failed: ${messageOf(error)}`);
      throw error;
    }
  }
}
