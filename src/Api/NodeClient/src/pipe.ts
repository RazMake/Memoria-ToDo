// The pipe name rule, the same as LocalApiPipe in Memoria.ToDo.Api.Contract. The two clients share no code,
// so an integration test checks that both name the same pipe.

function required(value: string | undefined, what: string): string {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`The pipe name needs the ${what}, but it is missing or blank.`);
  }
  return value;
}

/** `Memoria-ToDo.LocalApi.<domain>.<user>`. Throws for a missing or blank argument. */
export function pipeNameFor(domain: string, user: string): string {
  return `Memoria-ToDo.LocalApi.${required(domain, 'domain')}.${required(user, 'user')}`;
}

/** The name for the signed-in Windows account, from its domain (USERDOMAIN) and user (USERNAME). */
export function pipeNameForCurrentUser(env: NodeJS.ProcessEnv = process.env): string {
  return pipeNameFor(required(env.USERDOMAIN, 'domain (USERDOMAIN)'), required(env.USERNAME, 'user (USERNAME)'));
}

/** The `@grpc/grpc-js` address of the named pipe: `unix:\\.\pipe\<name>`. */
export function pipeAddress(pipeName: string): string {
  return `unix:\\\\.\\pipe\\${pipeName}`;
}
