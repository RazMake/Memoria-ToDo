import { Channel, credentials } from '@grpc/grpc-js';
import { loadContract } from './contract.js';
import type { ContactsClient } from './generated/memoria/todo/v1/Contacts.js';
import type { SettingsClient } from './generated/memoria/todo/v1/Settings.js';
import type { SyncClient } from './generated/memoria/todo/v1/Sync.js';
import type { TasksClient } from './generated/memoria/todo/v1/Tasks.js';
import { pipeAddress, pipeNameForCurrentUser } from './pipe.js';
import { promisifyClient } from './promisify.js';
import type { Promisified } from './promisify.js';

// The contract allows replies of up to 64 MB, while gRPC's default is 4 MB.
const maxReplyBytes = 64 * 1024 * 1024;

/** The local API of the app running for this Windows account. A call fails with status UNAVAILABLE when the app isn't running. */
export interface MemoriaClient {
  tasks: Promisified<TasksClient>;
  contacts: Promisified<ContactsClient>;
  sync: Promisified<SyncClient>;
  settings: Promisified<SettingsClient>;
  /** Closes the connection. Closing twice does no harm. */
  close(): void;
}

/** Connects on the first call to the pipe with the given name. */
export function connectTo(pipeName: string): MemoriaClient {
  const contract = loadContract();
  const address = pipeAddress(pipeName);
  const insecure = credentials.createInsecure();
  const channel = new Channel(address, insecure, { 'grpc.max_receive_message_length': maxReplyBytes });
  const options = { channelOverride: channel };
  let closed = false;
  return {
    tasks: promisifyClient(contract.Tasks.service, new contract.Tasks(address, insecure, options)) as Promisified<TasksClient>,
    contacts: promisifyClient(contract.Contacts.service, new contract.Contacts(address, insecure, options)) as Promisified<ContactsClient>,
    sync: promisifyClient(contract.Sync.service, new contract.Sync(address, insecure, options)) as Promisified<SyncClient>,
    settings: promisifyClient(contract.Settings.service, new contract.Settings(address, insecure, options)) as Promisified<SettingsClient>,
    close() {
      if (!closed) {
        closed = true;
        channel.close();
      }
    },
  };
}

/** Connects to the app of the signed-in Windows account. */
export function connect(): MemoriaClient {
  return connectTo(pipeNameForCurrentUser());
}
