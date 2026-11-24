import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, it } from 'node:test';
import { grpc, loadBuilt, packageFolder, settledWithin } from './support.mjs';

const { connectTo } = await loadBuilt('dist/connect.js');

const folder = fileURLToPath(packageFolder);
const contractFolder = fileURLToPath(new URL('../../../src/Api/Contract/memoria/', import.meta.url));

function filesUnder(root, skip = []) {
  return readdirSync(root, { recursive: true })
    .filter((path) => !skip.some((name) => path.split(/[\\/]/).includes(name)))
    .filter((path) => statSync(join(root, path)).isFile())
    .map((path) => path.replaceAll('\\', '/'))
    .sort();
}

describe('The Node.js package takes the contract and its pins from one place each (integration)', () => {
  it('builds its .proto files from the contract project, byte for byte', () => {
    const contract = filesUnder(contractFolder);
    const packaged = filesUnder(join(folder, 'dist/proto/memoria'));

    assert.ok(contract.length > 0);
    assert.deepEqual(packaged, contract);
    for (const file of contract) {
      assert.deepEqual(readFileSync(join(folder, 'dist/proto/memoria', file)), readFileSync(join(contractFolder, file)), file);
    }
  });

  it('keeps no .proto file in its own source', () => {
    assert.deepEqual(filesUnder(folder, ['node_modules', 'dist']).filter((file) => file.endsWith('.proto')), []);
  });

  it('pins its dependencies exactly, needs Node.js 22 or later, and commits its lock file', () => {
    const manifest = JSON.parse(readFileSync(join(folder, 'package.json'), 'utf8'));

    assert.equal(manifest.name, '@memoria-todo/client');
    assert.equal(manifest.engines.node, '>=22');
    for (const section of [manifest.dependencies, manifest.devDependencies]) {
      for (const [name, version] of Object.entries(section)) {
        assert.match(version, /^\d+\.\d+\.\d+$/, name);
      }
    }
    assert.ok('@grpc/grpc-js' in manifest.dependencies && '@grpc/proto-loader' in manifest.dependencies && 'typescript' in manifest.devDependencies);
    assert.ok(existsSync(join(folder, 'package-lock.json')));
  });
});

describe('The Node.js client with no app running (integration)', () => {
  it('fails a call at once with UNAVAILABLE', async () => {
    const api = connectTo(`Memoria-ToDo.Test.${process.pid}.${Date.now()}`);
    try {
      const started = Date.now();

      await assert.rejects(settledWithin(api.tasks.listTasks({}), 5000), (failure) => failure.code === grpc.status.UNAVAILABLE);

      assert.ok(Date.now() - started < 3000, `took ${Date.now() - started} ms`);
    } finally {
      api.close();
    }
  });
});
