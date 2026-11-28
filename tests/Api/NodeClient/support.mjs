// Shared by the Node.js client's tests: where the built package is, and its gRPC status codes.
import { createRequire } from 'node:module';

const packageFolder = new URL('../../../src/Api/NodeClient/', import.meta.url);

/** The built package's file, such as `dist/pipe.js`, loaded as a module. Needs `npm run build` first. */
export const loadBuilt = (relativePath) => import(new URL(relativePath, packageFolder));

/** The package's own `@grpc/grpc-js`, so the tests see the status codes the client sees. */
export const grpc = createRequire(new URL('package.json', packageFolder))('@grpc/grpc-js');

export { packageFolder };

/**
 * The outcome of `promise`, or a failure when it hasn't settled within `milliseconds`. The timer is cleared when the promise
 * settles first, so a test waits on the promise and the timer only bounds a failure.
 */
export function settledWithin(promise, milliseconds) {
  let timer;
  const bound = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`The call hadn't settled within ${milliseconds} ms.`)), milliseconds);
  });
  return Promise.race([promise, bound]).finally(() => clearTimeout(timer));
}
