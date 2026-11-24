import type { Client, ClientReadableStream, ServiceDefinition, requestCallback } from '@grpc/grpc-js';

/** A service client whose unary calls take the request's fields and return a Promise. */
export type PromiseClient = Record<string, (...args: any[]) => any>;

/**
 * The calls of a generated `@grpc/grpc-js` client type, in camelCase: a unary call takes its request and returns a
 * Promise of the reply, and a server-streaming one returns the client's own stream. Each call's last overload is the
 * one that takes only the request (and the callback).
 */
export type Promisified<C> = {
  [K in keyof C as K extends keyof Client ? never : K extends Uncapitalize<K & string> ? K : never]:
    C[K] extends (request: infer Request, callback: requestCallback<infer Reply>) => any
      ? (request: Request) => Promise<Reply>
      : C[K] extends (request: infer Request, ...rest: any[]) => ClientReadableStream<infer Reply>
        ? (request: Request) => ClientReadableStream<Reply>
        : never;
};

/**
 * Wraps every call of the service definition, by walking it: a Promise for each unary call, the client's own stream
 * for a streaming one. A call rejects with the gRPC status error, unchanged.
 */
export function promisifyClient(definition: ServiceDefinition, client: object): PromiseClient {
  const calls = client as Record<string, (...args: any[]) => any>;
  const wrapped: PromiseClient = {};
  for (const method of Object.values(definition)) {
    const name = method.originalName;
    if (name === undefined) {
      throw new Error(`A call of the contract has no name: ${method.path}`);
    }
    wrapped[name] = method.responseStream
      ? (request) => calls[name](request)
      : (request) => new Promise((resolve, reject) => {
          calls[name](request, (error: Error | null, reply: unknown) => (error ? reject(error) : resolve(reply)));
        });
  }
  return wrapped;
}
