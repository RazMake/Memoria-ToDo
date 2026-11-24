import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { loadBuilt } from './support.mjs';

const { pipeNameFor, pipeNameForCurrentUser, pipeAddress } = await loadBuilt('dist/pipe.js');

describe('The Node.js client names the pipe like the .NET client (unit)', () => {
  it('names it Memoria-ToDo.LocalApi, then the domain, then the user', () => {
    assert.equal(pipeNameFor('CORP', 'razvan'), 'Memoria-ToDo.LocalApi.CORP.razvan');
  });

  it('keeps the domain and user as they are spelled', () => {
    assert.equal(pipeNameFor('MyPc', 'Ana Lee'), 'Memoria-ToDo.LocalApi.MyPc.Ana Lee');
  });

  it('gives different users different names', () => {
    assert.notEqual(pipeNameFor('PC', 'ana'), pipeNameFor('PC', 'bob'));
  });

  it('gives different domains different names', () => {
    assert.notEqual(pipeNameFor('PC1', 'ana'), pipeNameFor('PC2', 'ana'));
  });

  for (const [what, domain, user] of [
    ['a missing domain', undefined, 'ana'],
    ['a blank domain', ' ', 'ana'],
    ['an empty user', 'PC', ''],
    ['a missing user', 'PC', undefined],
  ]) {
    it(`rejects ${what}`, () => {
      assert.throws(() => pipeNameFor(domain, user));
    });
  }

  it('takes the domain and user of the signed-in account from USERDOMAIN and USERNAME', () => {
    assert.equal(
      pipeNameForCurrentUser({ USERDOMAIN: 'CORP', USERNAME: 'razvan' }),
      'Memoria-ToDo.LocalApi.CORP.razvan');
  });

  it('rejects a signed-in account whose domain is unknown', () => {
    assert.throws(() => pipeNameForCurrentUser({ USERNAME: 'razvan' }));
  });

  it('rejects a signed-in account whose user is unknown', () => {
    assert.throws(() => pipeNameForCurrentUser({ USERDOMAIN: 'CORP' }));
  });

  it('reaches the pipe through a unix: address of its Windows pipe path', () => {
    assert.equal(pipeAddress('Memoria-ToDo.LocalApi.CORP.razvan'), 'unix:\\\\.\\pipe\\Memoria-ToDo.LocalApi.CORP.razvan');
  });
});
