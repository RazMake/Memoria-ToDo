import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { grpc, loadBuilt } from './support.mjs';

const { loadServices } = await loadBuilt('dist/contract.js');
const { promisifyClient } = await loadBuilt('dist/promisify.js');
const { connectTo } = await loadBuilt('dist/connect.js');

/** A stand-in for a grpc-js client: unary calls answer through the callback; streaming ones return `stream`. */
function fakeClient(definition, { reply = {}, error = null, stream = { fake: 'stream' }, requests = [] } = {}) {
  const client = {};
  for (const [name, method] of Object.entries(definition)) {
    const call = method.responseStream
      ? (request) => { requests.push(request); return stream; }
      : (request, callback) => { requests.push(request); callback(error, error ? undefined : reply); };
    client[name] = call;
    client[method.originalName] = call;
  }
  return client;
}

const unaryCallsOf = (definition) =>
  Object.values(definition).filter((method) => !method.responseStream).map((method) => method.originalName);

/** A service call as `@grpc/grpc-js` describes it, built in memory. */
const call = (name, responseStream = false) =>
  ({ originalName: name, path: `/memoria.test.v1.Things/${name}`, requestStream: false, responseStream });

/** Service definitions built in memory, so the unit tests read no .proto file. */
const things = {
  ResolveThing: call('resolveThing'),
  GetThing: call('getThing'),
  ListThings: call('listThings'),
  WatchThings: call('watchThings', true),
};
const others = { Ping: call('ping') };

describe('The Node.js client wraps every call of a service definition in a Promise (unit)', () => {
  it('gives the client a function for each call of the definition, named in camelCase', () => {
    const api = promisifyClient(things, fakeClient(things));

    assert.deepEqual(Object.keys(api).sort(), ['getThing', 'listThings', 'resolveThing', 'watchThings']);
  });

  it('sends the request fields it was given and resolves with the reply', async () => {
    const requests = [];
    const reply = { id: 't1', title: 'Buy milk' };
    const api = promisifyClient(things, fakeClient(things, { reply, requests }));

    const result = await api.resolveThing({ id: 't1' });

    assert.deepEqual(result, reply);
    assert.deepEqual(requests, [{ id: 't1' }]);
  });

  it('returns a Promise from each unary call', () => {
    const api = promisifyClient(things, fakeClient(things));

    assert.ok(api.listThings({}) instanceof Promise);
  });

  it('rejects with the gRPC status error itself, so its code and message pass through unchanged', async () => {
    const codes = ['CANCELLED', 'INVALID_ARGUMENT', 'NOT_FOUND', 'ABORTED', 'FAILED_PRECONDITION',
      'RESOURCE_EXHAUSTED', 'UNIMPLEMENTED', 'INTERNAL', 'UNAVAILABLE'];
    for (const name of codes) {
      const error = Object.assign(new Error(`${grpc.status[name]} ${name}: the failure's own message`),
        { code: grpc.status[name], details: "the failure's own message" });
      const api = promisifyClient(things, fakeClient(things, { error }));

      await assert.rejects(api.getThing({ id: 'x' }), (rejected) => {
        assert.equal(rejected, error);
        assert.equal(rejected.code, grpc.status[name]);
        assert.equal(rejected.details, "the failure's own message");
        return true;
      });
    }
  });

  it('hands a streaming call the client\'s own stream, to read with for await', () => {
    const stream = { fake: 'stream' };
    const api = promisifyClient(things, fakeClient(things, { stream }));

    assert.equal(api.watchThings({}), stream);
  });

  it('wraps only the calls of the definition it was given, so no call is written twice', () => {
    const api = promisifyClient(others, fakeClient(others));

    assert.equal(typeof api.ping, 'function');
    assert.equal(api.resolveThing, undefined);
  });
});

describe('The Node.js client loads the contract from its .proto files (integration)', () => {
  const services = loadServices();

  it('loads the four services, with the calls the contract names', () => {
    assert.deepEqual(Object.keys(services).sort(), ['contacts', 'settings', 'sync', 'tasks']);
    assert.ok(unaryCallsOf(services.tasks).includes('resolveTask'));
    assert.ok(unaryCallsOf(services.contacts).includes('searchContacts'));
    assert.ok(unaryCallsOf(services.sync).includes('syncNow'));
    assert.ok(unaryCallsOf(services.settings).includes('getSettings'));
  });

  for (const [service, definition] of Object.entries(loadServices())) {
    it(`gives ${service} a function for each of its calls, named in camelCase`, () => {
      const api = promisifyClient(definition, fakeClient(definition));
      for (const method of Object.values(definition)) {
        assert.equal(typeof api[method.originalName], 'function', method.originalName);
      }
    });
  }
});

describe('connect() gives a client for each service and a close (integration)', () => {
  it('returns tasks, contacts, sync, and settings, with the calls of each', () => {
    const api = connectTo('Memoria-ToDo.Test.shape');
    try {
      assert.equal(typeof api.tasks.listTasks, 'function');
      assert.equal(typeof api.contacts.searchContacts, 'function');
      assert.equal(typeof api.sync.getStatus, 'function');
      assert.equal(typeof api.settings.getSettings, 'function');
    } finally {
      api.close();
    }
  });

  it('closes without a call having been made, and a second close does no harm', () => {
    const api = connectTo('Memoria-ToDo.Test.close');

    api.close();

    api.close();
  });
});
