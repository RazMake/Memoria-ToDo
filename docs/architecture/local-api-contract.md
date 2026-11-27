# What programs can call through the local API

Status: Decided

This page holds the [local API's](local-api.md) contract: the calls your programs make, what each returns, how failures show, and how the contract grows. Both client packages are generated from it.

## Behavior

The contract lives in `.proto` files in `Memoria.ToDo.Api.Contract`, as [Local API](local-api.md#the-contract-and-clients-form-a-part-of-their-own) places it. Each call maps to one operation that [Tasks](tasks.md), [Contacts](contacts.md), or [Sync](sync.md) already publish, so the API adds no rule of its own. You accepted every proposal on this page on November 23, 2026.

### Four services cover version 1

The contract's package is `memoria.todo.v1`. The table lists each service, its calls, and the module each call uses:

| Service | Calls | Uses |
| --- | --- | --- |
| `Tasks` | `ListTasks`, `GetTask`, `CreateTask`, `UpdateTask`, `MoveTask`, `ResolveTask`, `ReopenTask`, `DeleteTask`, `AddImage`, `ListTags`, `WatchTasks` | Tasks: `ITasks` and its [direct task changes](tasks.md#direct-task-changes-are-a-capability-of-their-own) |
| `Contacts` | `ListGroups`, `GetMe`, `SearchContacts`, `GetContact`, `SaveContact`, `DeleteContact`, `MoveGroup`, `MentionOf`, `WatchContacts` | Contacts: `IContacts` |
| `Sync` | `GetStatus`, `SyncNow`, `WatchStatus` | Sync: `ISyncService` |
| `Settings` | `GetSettings`, `SetDeletionDelay`, `SetTagColor`, `MoveTag` | Tasks' settings, tag colors, and tag order, and Rendering's [30 tag colors](rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) |

Each call follows these rules:

- **`ListTasks` returns open tasks in rank order**, or resolved ones too when asked. Its query matches as [Task View's search](tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) does, so a word matches any of a task's tags, and tags narrow it, as [below](#a-tasks-tags-travel-as-a-list-beside-the-older-one-tag-field). The whole list comes in one reply, with no pages.
- **Replies may be up to 64 MiB (mebibytes)**, instead of gRPC's default of 4 MiB. An image sent to `AddImage` may be up to 32 MiB before shrinking, and a whole request up to 33 MiB, so that image and its fields fit. Decided by the implementation, to confirm.
- **A task carries its ID, title, description, tags, created, updated, and resolved times, and its link address.** Its tags come in the task's order, and its group tag, the first, also comes alone, as [below](#a-tasks-tags-travel-as-a-list-beside-the-older-one-tag-field). Its rank stays inside Tasks: the list's order shows it, and `MoveTask` names neighbors, as [`ITasks.Move`](../../src/Shared/Tasks/ITasks.cs) does.
- **A contact carries its fields by their file names, with the text the file holds**, through Contacts' public [`ContactFields`](../../src/Shared/Contacts/Fields/ContactFields.cs). The contract never lists the fields, and a contact read and then saved keeps every stored value, including an empty nickname.
- **`SaveContact` replaces the whole contact**, as the Contact list does, with no check for a newer version, since a contact has no updated time. It replies with the contact as sent, which is what the store saved, without reading it again.
- **`GetContact` and `MentionOf` find a contact by its ID or its alias.**
- **`DeleteContact` with a group that doesn't exist fails with `INVALID_ARGUMENT`**; with a known group that has no such member, `NOT_FOUND`. `MoveGroup` naming a group whose file was skipped fails with `FAILED_PRECONDITION`.
- **`GetSettings` returns the deletion delay, each tag in use in the tags' order with its color, and the 30 color names.** Each tag's color is either the one you kept or Rendering's default, and is marked as which. `SetTagColor` accepts only those 30 names, spelled exactly, case included; any other fails with `INVALID_ARGUMENT` before anything is written.
- **Settings calls never answer from defaults that hide a skipped file.** When `settings.json` or `tags.json` was skipped, `GetSettings` fails with `FAILED_PRECONDITION`. `SetDeletionDelay`, `SetTagColor`, and `MoveTag` first check that every file their reply needs can be read; if one was skipped, they fail the same way and change nothing. Each asks Tasks through [`RequireReadableSettings`](tasks.md#a-caller-can-require-readable-settings-files).

Decided by the implementation, to confirm: `SaveContact`'s reply, `MentionOf`'s alias, `DeleteContact`'s two statuses, and `SetTagColor`'s exact spelling.
- **`SyncNow` waits for the sync pass and returns its status.** A call that's cancelled stops only its own wait, as [`SyncNowAsync`](../../src/Shared/Sync/ISyncService.cs) does.

### Task calls never touch your draft or open edit

Task calls use Tasks' [direct task changes](tasks.md#direct-task-changes-are-a-capability-of-their-own), so a program's add or image never disturbs what you're typing. `MoveTask`, `ResolveTask`, `ReopenTask`, and `DeleteTask` use `ITasks` as it is. `DeleteTask` also deletes the images waiting in the task's open edit, as Task View's delete does.

- **`UpdateTask` needs the task's updated time as the program read it.** When the task has changed since, the call fails with `ABORTED` and changes nothing, and the program reads the task again. A field the call leaves out keeps its value, and tags change as [below](#a-tasks-tags-travel-as-a-list-beside-the-older-one-tag-field).
- **An open edit in Task View still merges.** Its save is the [three-way merge](tasks.md#two-versions-of-a-task-merge-field-by-field) against the task as the editor got it. So a program's change to another field survives, and your save wins for the same field.
- **`AddImage` adds the image and its link in one save.** It shrinks the image, writes it into the task's image folder, and appends its Markdown link on a new line at the end of the description.

### A task's tags travel as a list, beside the older one-tag field

A task has [several tags in order](tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), and the first one groups it. Version 1 [only grows](#version-1-only-grows), so each message keeps its one-tag `tag` field and gains a list beside it. A program written before the list keeps working, and one written after uses the list. The user decided these rules:

- **`TaskItem.tags` holds every tag in the task's order**, and `TaskItem.tag` still holds the first, the group tag, or is absent when there's none.
- **`CreateTaskRequest.tags` sets the new task's tags in that order.** The older `tag` still sets one tag, and an empty `tag` still means none.
- **`UpdateTaskRequest.tags` replaces the whole list when present**, and an empty list removes every tag. It's a small message holding the list, since proto3 can't tell an empty list from one left out. The older `tag` still replaces the whole list with that one tag, and an empty `tag` still removes them all, as it does today.
- **`ListTasksRequest.tags` keeps the tasks that have every tag it names**, each anywhere in the task's list, as [Tasks' filter](tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) does. The older `tag` keeps the tasks that have that tag anywhere in their list, as it does today.
- **A request that sets both `tag` and `tags` fails with `INVALID_ARGUMENT`**, before anything is written, so a program never has to guess which one won.
- **Every tag a program sends is [normalized](tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case)**, in `tag` or `tags`, in every call: `My Work` is stored and matched as `my-work`. A repeated tag in a list is dropped, keeping its first place, with no error. Tasks does both, so the API adds no rule.
- **An app not yet updated ignores `tags`**, as protobuf ignores a field it doesn't know. So a newer program talking to it gets an empty `tags` in every task, and a create or update that sent only `tags` sets no tags.

Decided by the session, each a decision to confirm, as Tasks' `ArgumentException` maps:

- **A tag that's empty once normalized fails with `INVALID_ARGUMENT`**, before anything is written: an empty string or only spaces in `tags`, or only spaces in `tag`. An empty `tag` still means none, as above.
- **More than 15 tags, after normalizing and dropping repeats, fails with `INVALID_ARGUMENT`** in `CreateTask` and `UpdateTask`, before anything is written. A filter in `ListTasks` has no limit.
- **"Sets both `tag` and `tags`" means the `tag` field is present, even when empty, and `tags` is non-empty**, or in `UpdateTask` present, even when its list is empty. So `tag = ""` with a non-empty `tags` fails with `INVALID_ARGUMENT` too.
- **`SetTagColor` and `MoveTag` normalize the tags they name too**, and `GetSettings` returns each tag normalized.
### Watch streams send notices, and a slow reader gets one, not a queue

- **`WatchTasks` and `WatchContacts` send an empty notice at once, then one after each change.** The program then reads whatever it needs.
- **A reader that falls behind gets a single notice for all the changes it missed**, never a queue, so a slow program costs the app no memory.
- **`WatchStatus` sends the full sync status at once, then each new one.** A reader that falls behind gets only the newest.

### Each failure maps to one gRPC status

The Local API maps each failure by its type, through one registration table, most specific type first ([status codes](https://grpc.io/docs/guides/status-codes/)). The status's message is the failure's own message. The user decided that the module that knows a "not ready" case throws a type for it, so the API never matches file paths or messages. The table lists each case:

| Failure | Status |
| --- | --- |
| The app isn't running, or another account owns the pipe; in the .NET client, any failure to connect | `UNAVAILABLE`, from the client |
| The app is stopping, including a stream its stop cuts short | `UNAVAILABLE` |
| `TaskNotFoundException`, or a contact or own contact the call names isn't there | `NOT_FOUND` |
| The task changed since `base_updated` | `ABORTED` |
| `ArgumentException`, `DeleteContact`'s unknown group, an unknown color name, an image that isn't one or can't be shrunk to 1 MB, or a request missing a field the call needs, such as `UpdateTask`'s `base_updated` | `INVALID_ARGUMENT` |
| Only these "not ready" cases: no repository folder (`NoRepositoryFolderException`); sync not set up, such as no repository or key for `SyncNow` (Sync's [`SyncNotReadyException`](sync.md#public-interface)); a skipped file the call needs, such as `tags.json` (the platform's [`SkippedFileException`](modules.md#a-call-that-needs-a-skipped-file-throws-one-type)) | `FAILED_PRECONDITION` |
| A request over 33 MiB, or an image over 32 MiB | `RESOURCE_EXHAUSTED` |
| The program cancelled the call | `CANCELLED` |
| Anything else, such as a failure inside the app no other row names | `INTERNAL`, with the exception logged |

A change the app saved before a notice failed isn't a failure: the call succeeds, as [Failure modes](#failure-modes) says, so the table gains no status for it.

That the .NET client reports every failure to connect as `UNAVAILABLE` is decided by the implementation, to confirm.

### Version 1 only grows

- **`memoria.todo.v1` changes only by adding** new calls, services, fields, and enum values. A field's number, name, and type never change, and nothing is removed ([updating a message](https://protobuf.dev/programming-guides/proto3/#updating)). So any client works with any newer app, and a newer client's new call fails with `UNIMPLEMENTED` on an older app.
- **A change that can't be made by adding gets a new package, `memoria.todo.v2`**, which the app serves beside `v1` until a design file retires `v1`.
- **Every request and reply has a message of its own**, even an empty one, so each can gain fields later. Enums start with an `_UNSPECIFIED` value at 0.

## Public interface

The contract, in outline; every message has the fields this page names:

```proto
syntax = "proto3";
package memoria.todo.v1;
option csharp_namespace = "Memoria.ToDo.Api.V1";
import "google/protobuf/timestamp.proto";

service Tasks {
  rpc ListTasks(ListTasksRequest) returns (TaskList);   // query, optional tag, include_resolved, tags
  rpc GetTask(GetTaskRequest) returns (TaskItem);
  rpc CreateTask(CreateTaskRequest) returns (TaskItem); // title, description, optional tag, tags
  rpc UpdateTask(UpdateTaskRequest) returns (TaskItem); // id, base_updated, optional title, description, tag, tags
  rpc MoveTask(MoveTaskRequest) returns (TaskItem);     // id, optional previous_id, optional next_id
  rpc ResolveTask(ResolveTaskRequest) returns (TaskItem);
  rpc ReopenTask(ReopenTaskRequest) returns (TaskItem);
  rpc DeleteTask(DeleteTaskRequest) returns (DeleteTaskResponse);
  rpc AddImage(AddImageRequest) returns (TaskItem);     // id, image bytes, alt_text
  rpc ListTags(ListTagsRequest) returns (TagList);      // each tag in use and its count
  rpc WatchTasks(WatchTasksRequest) returns (stream ChangeNotice);
}

service Contacts {
  rpc ListGroups(ListGroupsRequest) returns (GroupList);
  rpc GetMe(GetMeRequest) returns (Contact);
  rpc SearchContacts(SearchContactsRequest) returns (ContactList);
  rpc GetContact(GetContactRequest) returns (Contact);   // by ID or alias
  rpc SaveContact(SaveContactRequest) returns (Contact); // group, contact
  rpc DeleteContact(DeleteContactRequest) returns (DeleteContactResponse);
  rpc MoveGroup(MoveGroupRequest) returns (GroupList);
  rpc MentionOf(MentionOfRequest) returns (Mention);     // contact_id, template; the Markdown link
  rpc WatchContacts(WatchContactsRequest) returns (stream ChangeNotice);
}

service Sync {
  rpc GetStatus(GetStatusRequest) returns (SyncStatus);
  rpc SyncNow(SyncNowRequest) returns (SyncStatus);
  rpc WatchStatus(WatchStatusRequest) returns (stream SyncStatus);
}

service Settings {
  rpc GetSettings(GetSettingsRequest) returns (SharedSettings);
  rpc SetDeletionDelay(SetDeletionDelayRequest) returns (SharedSettings);
  rpc SetTagColor(SetTagColorRequest) returns (SharedSettings);
  rpc MoveTag(MoveTagRequest) returns (SharedSettings);
}

message TaskItem {
  string id = 1; string title = 2; string description = 3; optional string tag = 4; // tag: the first of tags
  google.protobuf.Timestamp created = 5; google.protobuf.Timestamp updated = 6;
  google.protobuf.Timestamp resolved = 7; string link = 8;
  repeated string tags = 9;                       // every tag, in the task's order
}

// Row 21 adds these fields; every other field stays as it is.
message ListTasksRequest { /* query = 1, tag = 2, include_resolved = 3 */ repeated string tags = 4; } // tasks with every tag
message CreateTaskRequest { /* title = 1, description = 2, tag = 3 */ repeated string tags = 4; }
message UpdateTaskRequest { /* id = 1 ... tag = 5 */ TagNames tags = 6; }      // absent: unchanged
message TagNames { repeated string names = 1; }                                // empty: no tags

message Contact { map<string, string> fields = 1; } // file field name -> file text, such as "reportsTo"

message SyncStatus {
  SyncState state = 1; SyncFailure failure = 2; string details = 3;
  google.protobuf.Timestamp last_successful_sync = 4; bool has_pending_changes = 5;
  repeated SkippedFile skipped_files = 6; // relative_path, reason, requires_update
}

message SharedSettings {
  int32 deletion_delay_days = 1;
  repeated TagLook tags = 2;      // tag, color, color_is_default; in the tags' order
  repeated string colors = 3;     // Rendering's 30 tag color names
}
```

The .NET client, in the `Memoria.ToDo.Client` package, keeps its project's namespace, as the [naming rules](technology-stack.md#code-and-tests-sit-in-mirrored-trees-by-part-in-one-solution) say:

```csharp
namespace Memoria.ToDo.Api.Client;

/// The local API of the app running for this Windows account. Connects on the first call.
/// A call fails with StatusCode.Unavailable when the app isn't running, another account owns the pipe,
/// or the elevation differs. Dispose closes the connection.
public sealed class MemoriaClient : System.IDisposable
{
    public static MemoriaClient Connect();
    public Memoria.ToDo.Api.V1.Tasks.TasksClient Tasks { get; }
    public Memoria.ToDo.Api.V1.Contacts.ContactsClient Contacts { get; }
    public Memoria.ToDo.Api.V1.Sync.SyncClient Sync { get; }
    public Memoria.ToDo.Api.V1.Settings.SettingsClient Settings { get; }
    public void Dispose();
}
```

The Node.js client, in the `@memoria-todo/client` package. Each call takes the request's fields and returns a Promise, such as `api.tasks.resolveTask({ id })`. A call that fails rejects with its gRPC status. Streams are `@grpc/grpc-js` streams, which a program reads with `for await`:

```ts
export function connect(): MemoriaClient;

export interface MemoriaClient {
  tasks: TasksClient;
  contacts: ContactsClient;
  sync: SyncClient;
  settings: SettingsClient;
  close(): void;
}
```

## Failure modes

- **No repository is set up yet**: reads return empty lists, and changes fail with `FAILED_PRECONDITION`.
- **A program and Task View change the same task**: `UpdateTask` fails with `ABORTED` when the task changed since the program read it, and Task View's save merges, as [above](#task-calls-never-touch-your-draft-or-open-edit).
- **You edit a description while a program adds an image to it**: when your save replaces the description, the image's link goes, and the image stays in the clone unlinked. You accepted this, as for any edit of the same field.
- **A call fails inside the app**: `INTERNAL`, with an Error entry in the [log](local-api.md#every-call-is-logged-where-it-ends).
- **The app saved a change, and then a notice or the local save report failed inside the app** (the platform's [`NoticeFailedException<T>`](modules.md#steps-that-must-all-run-throw-their-failures-together)), even only one failure, for any task, settings, or contact call that changes something: the user decided that the call succeeds with the reply it gives after that save, built from the exception's saved item, such as `CreateTask`'s new task with its ID, and that the app logs one Error entry with the failures. No new status is added. So a program that retries only after an error never makes a second task. Proposed, a decision to confirm: a call whose change saves no item, such as `DeleteTask` or a settings call, replies as after a save, and the call's own log entry stays at Information, beside that Error entry.

## Ruled out

- **`ITasks.Add`, `SaveImage`, and `SaveEdit` for task calls**: each touches your draft or open edit.
- **`UpdateTask` without a check for a newer version**: a program would undo a change it never saw.
- **`AddImage` that returns the Markdown for the program to place with `UpdateTask`**: an image whose update failed would stay in the clone and sync, unlinked.
- **A contact message with a field for each contact field**: the field list would live in Contacts and in the contract.
- **Streams that send the whole list after each change**: a slow reader would queue copies of it.
- **Paging lists**: one person's tasks and contacts fit in one reply.
- **A `v2` package where `tag` becomes a list**: every program would move to new generated types for one field, and the app would serve two packages.
- **Changing `tag`'s type to `repeated string`**: a field's type never changes in version 1, and older programs would read the list wrongly.
- **`UpdateTask`'s older `tag` replacing only the first tag**: the same request would do something different from before, and an empty `tag` would have no clear meaning, the first tag or all.
- **When a request sets both `tag` and `tags`, `tags` wins**: a program that sets both would learn nothing of its mistake.
- **In `ListTasks` only, `tag` and `tags` together both narrow the list**: `CreateTask` and `UpdateTask` would still need a rule, so there'd be two.
- **A request with two tags that differ only in case fails with `INVALID_ARGUMENT`**: the user chose to normalize every tag and drop repeats.
