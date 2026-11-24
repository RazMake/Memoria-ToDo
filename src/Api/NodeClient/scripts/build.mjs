// Builds the package: generates TypeScript types from the contract's .proto files, compiles the TypeScript, then
// copies the .proto files into dist/proto.
// The .proto files live only in src/Api/Contract; this copy and the generated types are made at build, never kept in source control.
import { spawnSync } from 'node:child_process';
import { cpSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const contractRoot = fileURLToPath(new URL('../../Contract/', import.meta.url));
const contract = `${contractRoot}memoria`;
const generator = fileURLToPath(new URL('../node_modules/@grpc/proto-loader/build/bin/proto-loader-gen-types.js', import.meta.url));
const tsc = fileURLToPath(new URL('../node_modules/typescript/bin/tsc', import.meta.url));

function run(script, args) {
  const result = spawnSync(process.execPath, [script, ...args], { stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

// The generated types follow the options the client loads the .proto files with, which src/loader-options.json holds.
const options = JSON.parse(readFileSync(`${root}src/loader-options.json`, 'utf8'));
const optionArguments = Object.entries(options).map(([name, value]) =>
  value === true ? `--${name}` : `--${name}=${value}`);
const protoFiles = readdirSync(`${contract}/todo/v1`).map((file) => `memoria/todo/v1/${file}`);

rmSync(`${root}dist`, { recursive: true, force: true });
rmSync(`${root}src/generated`, { recursive: true, force: true });
run(generator, [...optionArguments, '--grpcLib=@grpc/grpc-js', '--importFileExtension=.js',
  '--includeDirs', contractRoot, '--outDir', `${root}src/generated`, ...protoFiles]);
run(tsc, ['-p', root]);
cpSync(contract, `${root}dist/proto/memoria`, { recursive: true });
