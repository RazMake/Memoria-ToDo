# How other programs reach your tasks through the local API

Status: Decided

The desktop app answers calls from your own programs on this PC, such as scripts, over gRPC on a Windows named pipe that only your Windows account can open. Two client packages, one for .NET and one for Node.js, are generated from one contract. With them, a program can read and change tasks, contacts, and shared settings, and can read and start sync, without touching the app's files. [What programs can call](local-api-contract.md) holds the contract.

## Behavior

The API is a desktop module, Local API, over the shared [Tasks](tasks.md), [Contacts](contacts.md), and [Sync](sync.md) modules. You decided the first list below before the design began, and on November 23, 2026, you accepted every other proposal on this page and on the contract page.

### You decided the transport, the scope, and the delivery

- **Only your Windows account, on this PC, can call the API.** There is no token and no other sign-in. The API isn't reachable over TCP or from a browser, and there's no gRPC-Web.
- **The calls travel over a Windows named pipe, not a Unix domain socket**, after the [proof below](#a-named-pipe-carries-the-calls-and-only-your-account-opens-it) showed that Node.js can't reach a Unix domain socket on Windows.
- **The TypeScript client runs on Node.js only.**
- **Version 1 covers four areas**: tasks; contacts; sync status and Sync now; and the shared settings, which are the deletion delay, tag colors, and tag order. It leaves out device setup and Configure, SSH and device keys, drafts, app updates, windows and Shell, and logs.
- **When the app isn't running, a call fails at once with the gRPC status `UNAVAILABLE`.** Clients never start the app.
- **Each GitHub release carries the .NET package (`.nupkg`) and the Node.js package (`.tgz`)** as files, and is [published only once every file is attached](technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag). No package feed is used.
- **One contract, in `.proto` files, is the only home of the API's shape.** Both clients and the app's server code are generated from it, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) asks.

### A named pipe carries the calls, and only your account opens it

A throwaway proof ran on your PC on November 23, 2026. It sent one call and one stream over each transport, served by Kestrel, ASP.NET Core's web server. It used the .NET 10.0.404 SDK, Node.js 26.13.0, `@grpc/grpc-js` 1.14.7, and `@grpc/proto-loader` 0.8.1. The table shows what each client did:

| Transport | .NET client | Node.js client |
| --- | --- | --- |
| Unix domain socket, in a folder only your account can open | Worked: first call 275 ms, then 0.55 ms on average over 1,000 calls | Failed: `14 UNAVAILABLE ... connect EACCES` |
| Named pipe, with Kestrel's default settings | Worked: first call 157 ms, then 0.46 ms on average | Worked: first call 141 ms, then 0.55 ms on average, and the stream |

The likely cause, not confirmed: Node.js on Windows treats every socket path as a named pipe. `@grpc/grpc-js` reaches a named pipe through a `unix:` address, such as `unix:\\.\pipe\<name>` ([grpc-node issue 2649](https://github.com/grpc/grpc-node/issues/2649)).

- **The app listens with Kestrel's named pipe listener, HTTP/2 only** ([gRPC over named pipes](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess-namedpipes)).
- **The pipe's access list keeps callers to your account on this PC.** You decided this on November 23, 2026. The list allows your account and denies the `NETWORK` group (`S-1-5-2`), which Windows gives every connection from another computer ([well-known SIDs](https://learn.microsoft.com/en-us/windows/win32/secauthz/well-known-sids)). So a program on another computer is refused over the network, even signed in as you. Kestrel's default list limits only the account, and .NET never sets `PIPE_REJECT_REMOTE_CLIENTS` ([CreateNamedPipe](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createnamedpipea)), so the app sets the list through Kestrel's `PipeSecurity` ([NamedPipeTransportOptions](https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.NamedPipes/src/NamedPipeTransportOptions.cs)). An integration test reads the live pipe's list and finds only those two rules; no test connects from another computer.
- **The pipe's name is `Memoria-ToDo.LocalApi.<domain>.<user>`**, from Windows' domain and user name. Both clients compute it the same way, so a program needs no setting. The rule's one .NET home is a public `LocalApiPipe` class in the contract project, which the composition root and the .NET client both call; the Node.js client holds the same rule in TypeScript, and an integration test checks that both name the same pipe. Decided by the implementation, to confirm.
- **No other program can take the name while the app listens.** Kestrel makes its first pipe instance with `FirstPipeInstance` and always keeps one instance waiting ([NamedPipeConnectionListener](https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.NamedPipes/src/Internal/NamedPipeConnectionListener.cs)). A named pipe leaves no file behind, so there's no stale file to clean up.
- **When the name is already taken, the API doesn't start.** That happens when another process holds it, such as the app in a second sign-in session of your account. The Local API logs an Error naming the pipe, and the app keeps running without the API, with no notification.
- **The .NET client checks who owns the pipe.** It connects with `PipeOptions.CurrentUserOnly`, which refuses a pipe another account made ([PipeOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions)). It waits at most 1 second to connect.
- **The Node.js client can't check the owner**, since `@grpc/grpc-js` offers no way to. So a program of another account on this PC could pose as the app, if it took the name before the app started. You accepted this risk for a personal PC.
- **An elevated program can't call an app that isn't elevated**, or the other way round, and gets `UNAVAILABLE`. Not confirmed by a run.

### Shell starts the API once this is the only instance, off the UI thread

- **Shell starts the Local API through its lifetime, as it starts [Updates](desktop/updates.md)**. It starts it after it wins the [one-instance claim](desktop/shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view) and after Sync's [initial local publication](sync.md#one-owned-worker-keeps-synchronous-git-and-ssh-off-the-ui-thread). So a later instance never touches the pipe, and a client never sees the empty list of a store that's still loading. A failed initialization still starts the API.
- **Starting returns at once.** Kestrel starts on a background task the UI thread never waits for.
- **Shell stops the Local API first when the app ends**, before Sync's disposal. Stopping ends open streams and waits at most 5 seconds for running calls.
- **The server's cost is measured before the step ships**, in a Release build on your PC, and recorded here:
  - The tray icon appears no later than today, within the measurement's noise.
  - The idle app's private memory grows by at most 25 MB.
  - The installer's growth is measured and reported, since ASP.NET Core's shared framework ships inside the [self-contained app](technology-stack.md#the-desktop-app-carries-its-own-net-runtime).
- **The API has no on/off setting.** Only your account can reach it.

### Screens update on their own thread, as they do after a sync

Tasks' and Contacts' `Changed` notices are raised on the thread of the call that made the change, and API calls run on background threads, as sync's reloads do. Every desktop screen that listens already moves to its UI thread: [Task View](../../src/Desktop/TaskList/TaskListWindow.cs) and the [Contacts window](../../src/Desktop/ContactList/ContactsWindow/ContactsMainWindow.cs) through their dispatcher, and the Task editor's [tag look](../../src/Desktop/TaskEditor/TagLook.cs) through `OnElementThread`. So the Local API moves nothing to the UI thread.

### Every call is logged where it ends

The Local API follows [Logging's](logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) rules, under the component name `Local API`:

- **Each call logs one Information entry when it ends**, with its method, the IDs it acts on, its status, and its duration. The IDs include `GetContact`'s ID or alias and `SaveContact`'s contact ID, never a contact's other data. The change itself is logged once by Tasks or Contacts.
- **A call that ends with a status the program caused**, such as `NOT_FOUND` or `ABORTED`, logs a Warning. `INTERNAL` logs an Error with the exception. A call the program cancelled isn't a failure, so its one entry stays at Information.
- **The server's start and stop log at Information**, with the pipe's name. A server that can't start logs an Error.
- **A stream logs when it opens and when it closes**, never once per notice. A stream the app's stop cuts short ends with `UNAVAILABLE`, and its close logs at Information, since the program didn't cause it. Decided by the implementation, to confirm.

### The contract and clients form a part of their own

A fourth part, `Api`, sits beside the shared library and the two apps, in `src/Api/` and `tests/Api/`. The table lists each new project:

| Project | Folder | Holds |
| --- | --- | --- |
| `Memoria.ToDo.Api.Contract` | `src/Api/Contract/` | The `.proto` files, in `memoria/todo/v1/`, and the C# that Grpc.Tools generates from them at build |
| `Memoria.ToDo.Api.Client`, package `Memoria.ToDo.Client` | `src/Api/Client/` | `MemoriaClient.Connect`, which hides the pipe; its package holds the contract's assembly too |
| `@memoria-todo/client`, not a .NET project | `src/Api/NodeClient/` | `connect()`, the TypeScript types generated from the `.proto` files, and the compiled JavaScript |
| `Memoria.ToDo.Desktop.LocalAPI` | `src/Desktop/LocalAPI/` | The desktop module: the server, the mapping to the modules, the error table, and the streams |

- **The Node.js client loads the `.proto` files at run time with `@grpc/proto-loader`**, and its build generates TypeScript types with `proto-loader-gen-types`, which ships in the same package ([proto-loader](https://github.com/grpc/grpc-node/tree/master/packages/proto-loader)). It needs no `protoc` compiler, and the proof used it. The package's build copies the `.proto` files in, never by hand. `connect()` wraps each call in a Promise by walking the loaded service definitions, so no call is written twice.
- **Both packages take the release's version** from its [version tag](version-tags.md), such as `1.4.0` for `v1.4.0`. The [technology stack](technology-stack.md#the-local-api-adds-a-part-its-packages-nodejs-and-two-release-files) lists the packages they use and how the release packs them.

### Tests check the mapping with fakes, and a few the real pipe

| Tests | Kind | Where |
| --- | --- | --- |
| Each call's mapping to the modules, the error table, the streams' notices, and the log entries, with the shared fakes and no pipe. They call the service classes directly, with a fake call context, and check start and stop through a fake host; no unit test starts the web server | Unit | `tests/Desktop/Tests/LocalAPI/` |
| The direct task changes | Unit | `tests/Shared/Tests/Tasks/` |
| The .NET client's pipe name and options | Unit | `tests/Api/Tests/Client/`, in a new test project, `Memoria.ToDo.Api.Tests` |
| The Node.js client's pipe name, its Promise wrapping, and how it passes statuses on, over service definitions built in memory | Unit | `tests/Api/NodeClient/` |
| The Node.js client loading the contract's `.proto` files from disk, and the built package's wiring | Integration | `tests/Api/NodeClient/` |
| About six tests over the real pipe: a task's round trip and a stream; `UNAVAILABLE` with no server; a name that's already taken; the pipe's access list; and one round trip with the Node.js client, run with `node` | Integration | `tests/Desktop/Tests/LocalAPI/` |

Each integration test uses its own pipe name and its own temporary clone. The .NET ones call the shipped client's own connect path with only the pipe name injected, never a copy of it. The [technology stack](technology-stack.md#the-local-api-adds-a-part-its-packages-nodejs-and-two-release-files) gives the Node.js tests' commands. [Test selection](test-selection.md#each-changed-path-selects-tests-by-one-rule) has rules for the new folders.

## Public interface

Programs use the [contract and the two clients](local-api-contract.md#public-interface). Tasks' [direct task changes](tasks.md#direct-task-changes-are-a-capability-of-their-own) and Shell's [lifetime argument](desktop/shell.md#public-interface) are declared on their own pages. The desktop module, which only Shell and the composition root use:

```csharp
namespace Memoria.ToDo.Desktop.LocalAPI;

/// Starts and stops the local API.
public interface ILocalApiLifetime
{
    /// Starts listening in the background and returns at once. Logs a failure to start; never throws it.
    /// Repeated calls do nothing.
    void Start();

    /// Stops listening, ends open streams, and waits at most 5 seconds for running calls.
    /// Works when Start never ran; repeated calls join the same stop.
    System.Threading.Tasks.Task StopAsync();
}

public static class LocalApiServer
{
    /// Throws ArgumentNullException for a null argument, and ArgumentException for an empty pipe name.
    public static ILocalApiLifetime Create(Memoria.ToDo.Shared.Tasks.ITaskStore tasks,
        Memoria.ToDo.Shared.Contacts.IContacts contacts, Memoria.ToDo.Shared.Sync.ISyncService sync,
        string pipeName, Serilog.ILogger logger);
}
```

## Failure modes

- **The app isn't running, or is still starting**: the call fails at once with `UNAVAILABLE`. The program tries again later; nothing starts the app.
- **Another process holds the pipe's name when the app starts**: the API doesn't start, and the Local API logs an Error, and the app works as before. A .NET client then gets `UNAVAILABLE` if the holder is another account; a Node.js client can't tell, as [above](#a-named-pipe-carries-the-calls-and-only-your-account-opens-it).
- **The app ends during a call or stream**: the program sees `UNAVAILABLE`, or the stream ends.

The contract page lists the [failures a call reports](local-api-contract.md#failure-modes).

## Ruled out

- Transport:
  - **A Unix domain socket**: Node.js couldn't connect on Windows in the [proof](#a-named-pipe-carries-the-calls-and-only-your-account-opens-it), failing with `EACCES`.
  - **TCP on the loopback address with a token**: any local program could reach the port, and you chose no tokens.
  - **Kestrel's default access list alone**: it limits the account, but that account could still connect from another computer.
  - **gRPC-Web, for browsers**: you chose Node.js only.
  - **A server on the deprecated `Grpc.Core` package**: it's in maintenance only, replaced by grpc-dotnet ([the future of gRPC in C#](https://grpc.io/blog/grpc-csharp-future/)).
- Pipe name:
  - **The account's security ID (SID)**: Node.js can read it only by running `whoami /user` on every connect.
  - **A random name at each start, written to a file only your account reads**: it would protect Node.js clients too, but adds a file to keep, and a stale file after a crash.
- Layout:
  - **A `clients/` folder at the root**: it would sit outside the naming rules, the architecture test, and test selection.
  - **The contract and clients inside the desktop part**: the packages aren't desktop modules, and a module would export a package.
- Hosting:
  - **The composition root starts and stops the server, around Shell**: Shell would have to publish whether it won the claim and when Sync loaded.
  - **An on/off setting**: only your account can reach the API.
  - **The Local API moves each call to the UI thread**: screens already move notices themselves.
- Node.js client:
  - **`ts-proto`, generating classes at build time**: it needs the `protoc` compiler and a third-party plugin ([ts-proto](https://github.com/stephenh/ts-proto)).
  - **`@grpc/proto-loader` without generated types**: programs would get no types.
- Delivery:
  - **A package feed**: you chose release files only.
  - **Clients that start the app**: you chose that they fail at once.
