import { readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { loadPackageDefinition } from '@grpc/grpc-js';
import type { ChannelCredentials, ClientOptions, ServiceDefinition } from '@grpc/grpc-js';
import { loadSync } from '@grpc/proto-loader';
import type { ContactsClient, ContactsDefinition } from './generated/memoria/todo/v1/Contacts.js';
import type { SettingsClient, SettingsDefinition } from './generated/memoria/todo/v1/Settings.js';
import type { SyncClient, SyncDefinition } from './generated/memoria/todo/v1/Sync.js';
import type { TasksClient, TasksDefinition } from './generated/memoria/todo/v1/Tasks.js';
import options from './loader-options.json' with { type: 'json' };

const protoRoot = fileURLToPath(new URL('./proto/', import.meta.url));
const contractFolder = 'memoria/todo/v1';
const valueTypes: Record<string, StringConstructor> = { String };

/** A loaded service: its client class, typed by the generated types, and its definition. */
interface LoadedService<Client, Definition> {
  new (address: string, credentials: ChannelCredentials, options?: Partial<ClientOptions>): Client;
  service: Definition;
}

/** The contract's services, as `@grpc/grpc-js` loads them from the .proto files. */
export interface Contract {
  Tasks: LoadedService<TasksClient, TasksDefinition>;
  Contacts: LoadedService<ContactsClient, ContactsDefinition>;
  Sync: LoadedService<SyncClient, SyncDefinition>;
  Settings: LoadedService<SettingsClient, SettingsDefinition>;
}

/** The four services of the contract, loaded from the .proto files the build copied into dist/proto. */
export interface Services {
  tasks: ServiceDefinition;
  contacts: ServiceDefinition;
  sync: ServiceDefinition;
  settings: ServiceDefinition;
}

/** The loaded contract. */
export function loadContract(): Contract {
  const files = readdirSync(`${protoRoot}${contractFolder}`).map((file) => `${contractFolder}/${file}`);
  // The options file names the types of long and enum values for the type generator; the loader takes their constructors.
  const loaderOptions = { ...options, longs: valueTypes[options.longs], enums: valueTypes[options.enums], includeDirs: [protoRoot] };
  const definition = loadSync(files, loaderOptions);
  return (loadPackageDefinition(definition) as any).memoria.todo.v1 as Contract;
}

/** The four services of the contract, loaded with @grpc/proto-loader. */
export function loadServices(): Services {
  const contract = loadContract();
  return {
    tasks: contract.Tasks.service,
    contacts: contract.Contacts.service,
    sync: contract.Sync.service,
    settings: contract.Settings.service,
  };
}
