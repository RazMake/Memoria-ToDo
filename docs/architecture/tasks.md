# Tasks: task files, their merge rules, and images

Status: Decided

Tasks is the shared module that owns every task: its file, ID, and rank, how two devices' versions of it merge, automatic deletion, images, the draft each device keeps, and the settings every device shares. Both apps change tasks only through Tasks.

## Behavior

[Purpose and scope](purpose.md#each-task-is-a-json-file-with-a-few-properties) decides what a task holds, and [Sync through git](sync.md) decides when changes travel.

### The repository holds task files, image folders, and one settings file

The table lists what Tasks keeps in the task repository:

| Path | What it holds |
| --- | --- |
| `tasks/<id>.json` | One task, in a file named by the task's ID |
| `images/<id>/` | That task's images, one file each |
| `settings.json` | The deletion delay and the desktop app's two shortcut texts, whose validation belongs to [Configure](desktop/configure.md#each-setting-has-one-owner) |

Deleting a task removes its file and its image folder.

### Tasks works on the clone the app gives it

Each app gives Tasks the folder of this device's clone when it starts, and again whenever the user picks another; on the desktop, that's [Configure's repository folder](desktop/configure.md#the-sync-settings-section-sets-the-devices-clone). Tasks then reads that folder's tasks and announces the change. With no folder, the list is empty, and adding a task fails before any file is written, so the screen that asked can point the user to Configure and keep the text.

Proposed, each a decision to confirm:

- Tasks' calls are synchronous and may come from any thread; Tasks runs them one at a time. Its change notice is raised on the thread whose call made the change.
- Tasks reads the folder's task files when it's given the folder, so a few hundred small files are read before the list is announced.

### Each task file holds nine fields

Each task file is a JSON object with the nine fields in the table. Every time is in Coordinated Universal Time (UTC), in ISO 8601, such as `2026-11-12T08:30:15.123Z`.

| Field | What it holds |
| --- | --- |
| `id` | The task's ID, the same as its file name |
| `title` | One line of Markdown |
| `description` | Markdown, which can link images and other tasks |
| `rank` | The [sort key](#rank-is-a-text-key-that-sorts-between-its-neighbors) |
| `tags` | The task's tags |
| `created` | When the task was added |
| `updated` | When the task last changed, on any device |
| `resolved` | When the task was resolved, or `null` while it's open |
| `version` | The file format's version, starting at 1 |

- Times are written to the millisecond, so two edits rarely carry the same time.
- A newer app reads every older `version`. An app that finds a newer `version` skips the file and leaves it unchanged.

Proposed, each a decision to confirm:

- Tasks writes each file as UTF-8 JSON with two-space indents, its fields in the table's order, and `\n` line breaks, so one change shows as a few changed lines in git.
- A title is one line: Tasks trims spaces at both ends, and rejects an empty title or one with a line break. A description keeps the user's text, with each line break stored as `\n`, so both apps write the same file.
- A new task's `created` and `updated` are the same time, and its `resolved` is `null`.

### A time-ordered ID names each task, its file, and links to it

A task's ID is a universally unique identifier (UUID), version 7, which starts with the time it was made ([RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-7)). So task files list in the order they were added, and two devices never make the same ID, even offline. .NET makes it with [`Guid.CreateVersion7()`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7), so it needs no new dependency.

- The ID is written in lowercase with hyphens: 36 characters.
- A link to another task is `[link text](task:<id>)`. Tasks owns the `task:<id>` address; [Editing's task reference](editing.md#snippets-insert-common-blocks) writes the link around it and decides the link text.

Proposed: [Rendering's browser check](rendering.md#only-web-links-open-in-the-browser) rejects `task:` addresses, and the app opens the task instead. This is a decision to confirm.

### Rank is a text key that sorts between its neighbors

Each task's rank is a text key that sorts between the keys of the tasks before and after it, a fractional index ([Figma](https://www.figma.com/blog/realtime-editing-of-ordered-sequences/)). Moving a task changes only its own file, so two offline devices that move different tasks never conflict. When two keys are equal, the task IDs set the order. A new task gets a key before all others, so it goes to the top, as the [Task editor](desktop/task-editor.md#a-new-task-goes-to-the-top) decides.

Keys use the 62 digits and letters, compare character by character, and are never renumbered. Adding at the top keeps keys short; only repeated moves into one gap lengthen a key, by about one character per six moves.

Proposed: a key starts with a letter that says how long its whole-number part is, as the [fractional-indexing](https://github.com/rocicorp/fractional-indexing) library's keys do. The first task gets `a0`, and each new task at the top lowers the whole number by one, so the first 63 tasks get 2-character keys and the next 3,844 get 3. Characters compare by their code, so digits sort before capitals, and capitals before small letters. This is a decision to confirm.

### Two versions of a task merge field by field

When two devices changed the same task, Tasks compares each version with the one both started from, git's [merge base](https://git-scm.com/docs/git-merge-base), one field at a time. The table lists each case:

| Case | Result |
| --- | --- |
| A field changed on one device only | That device's change |
| Tags changed on both | Both devices' adds and removes apply |
| Title, Description, or Rank changed on both | The later edit wins, and the other is dropped |
| One device deleted the task, and the other edited it | The edit wins: the task comes back, with the edit |
| One device resolved the task, and the other edited it | The task stays resolved and keeps the edit |
| Both deleted the task | It stays deleted |

An edit can bring back a task that another device deleted. If that task is still resolved past the deletion delay, the next sync deletes it again, unless the user reopens it first.

Saving an edit changes only what the edit changed, so it never undoes a change that arrived while the edit was open. Tasks compares the edit with the version the editor started from: a Title or Description the edit changed replaces the current one, and the tags the edit added or removed are added to or removed from the current tags. Every field the edit didn't touch keeps its current value.

Proposed, each a decision to confirm:

- The later edit is the one with the later `updated` time, from each device's own clock. A device with a wrong clock can win or lose wrongly; this is a known limit.
- When both `updated` times are equal, the version already on the remote wins. The merged `updated` is the later of the two.
- When both devices changed the resolved time, the later edit wins.
- The merged `created` time is the earlier of the two versions' times, rather than rewriting the task's age.
- A task that comes back gets its images back too.

### Resolved tasks are deleted after a delay every device shares

The deletion delay is one setting in `settings.json`, the same on every device, as [purpose and scope](purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. A resolved task is deleted once its resolution is older than the delay, even if it was edited since.

Proposed, each a decision to confirm:

- During each sync, any device deletes the tasks resolved longer ago than the delay. Two devices deleting the same task merge cleanly, and an edit still wins, as the table above says.

The approved range, stored format, and settings merge rules are defined in the [shared settings format](#one-versioned-file-holds-the-three-shared-settings).

### One versioned file holds the three shared settings

The approved `settings.json` format is one object with these fields, in this order:

| Field | Value |
| --- | --- |
| `version` | `1` |
| `deletionDelayDays` | An object with `value`, an integer from 1 to 365, and `updated`, a UTC timestamp |
| `addNewTaskShortcut` | An object with `value`, text or `null`, and `updated`, a UTC timestamp |
| `manageTasksShortcut` | An object with `value`, text or `null`, and `updated`, a UTC timestamp |

Each setting writes `value` before `updated`. The file uses the [task file's JSON style](#each-task-file-holds-nine-fields)
and UTC times to the millisecond. Tasks interprets the deletion delay, but keeps shortcut text without interpreting,
trimming, or rejecting it. Windows [Configure](desktop/configure.md#each-setting-has-one-owner) validates that text;
Android preserves it without using it. A null shortcut means Configure's existing default, not a second default in Tasks.

Each setting merges by its own `updated` time; the later time wins, and equal times take the remote value.
An absent file contributes no settings and never deletes settings from a present file. Three absent files merge to no file.
A present file must hold all three settings with valid types and times. An unsupported version or unreadable file
is reported, left untouched, and cannot be replaced by a setter or initialization.

With no file, reads supply the existing deletion default and null shortcuts without writing. `EnsureSettings` creates
the file during synchronization. Setters also create a missing file, changing only the selected setting's time.
A setter with an unchanged value does nothing. With no selected clone, reads supply defaults and setters fail before writing.

Decision to confirm: untouched defaults carry `1970-01-01T00:00:00.000Z`, so initialization cannot overwrite a
setting another device explicitly changed. A setting changed by the user gets the current time from TimeProvider.

An unreadable settings file does not prevent readable tasks from loading. It makes settings reads and writes fail
with `InvalidDataException`, and prevents automatic deletion, rather than deleting tasks using a guessed delay.

### Sync shares Tasks' local update lock, never a network wait

Sync obtains [its capability](../../src/Shared/Tasks/Synchronization/ITaskSynchronization.cs) from the same store as
the task operations. `UpdateRepository` checks that the expected clone is still selected before calling its synchronous
callback under Tasks' existing lock. A changed selection fails before the callback runs. Commit and merge application
use this operation; fetch, push, and every other network wait stay outside it. Sync rechecks and commits intervening
local edits before applying a fetched merge.

The operation reloads tasks, settings, and skipped-file diagnostics while still holding the lock, including after a
failed callback. It then releases the lock before raising notices. A callback failure propagates; a reload failure too
is kept with it in `AggregateException`. This is local coordination, not a transaction that undoes a failed Git update.

`LocalSaved` reports durable local repository changes, including settings and automatic deletion. It excludes repository
selection, imported updates, sync's settings initialization, drafts, and images still waiting outside the clone. The existing task `Changed` notice
continues to refresh screens; settings have their own notice. A reload never starts another sync by pretending to be
a local save. Skipped-file snapshots name corrupt, unreadable, and newer-version files without deleting them.

Decisions to confirm: a reload always raises the settings-change notice, even when values are unchanged;
`EnsureSettings` raises no `LocalSaved` notice because initialization is already part of a sync pass.

### Images are at most 1 MB and live beside their task

Images are PNG for screenshots and images with transparency, and JPEG for photos. Each platform's own image tools shrink them, so the apps need no imaging library: Windows Presentation Foundation (WPF) imaging ([BitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapencoder)) and Android's [Bitmap](https://developer.android.com/reference/android/graphics/Bitmap.CompressFormat). An image over 1 MB is shrunk in these steps:

1. Its longest side is scaled down to at most 1920 pixels.
2. A PNG still over 1 MB is saved as JPEG.
3. A JPEG still over 1 MB is saved at a lower quality, then scaled down further, until it fits.

Each of these rules also holds:

- A JPEG file stays JPEG; an image from the clipboard or in any other format starts as PNG.
- Each image is named by its own UUID version 7, so two offline devices never pick the same name. A description links to it from the clone's root, such as `![alt](images/<task-id>/<image-id>.png)`, and [Rendering loads it from the clone](rendering.md#the-web-view-runs-no-scripts-and-loads-only-images-from-the-clone).
- An image is deleted only with its task, even when no link to it is left, because a merge can drop the only link.
- An image added to a draft or an open edit waits outside the clone, and moves into the task's folder on save. Discarding deletes it. A draft gets its task ID when it starts, so its image links are final.
- Tasks tells an open edit's image from a draft's by its task ID: an image for a task that already exists belongs to that task's open edit. It waits apart from the draft's images, so keeping, discarding, or adding the draft never deletes it. Both apps' editors keep every image through the same call.

Each app's image tools only read an image's format and size, scale it, and save it as PNG, or as JPEG at a given quality, through an interface Tasks publishes. Tasks runs the steps above, so both apps shrink images the same way.

Proposed, each a decision to confirm:

- 1 MB is 1,048,576 bytes. A PNG or JPEG file of at most 1 MB is saved unchanged. An image in any other format is first saved as PNG at its own size, and goes through the steps when that's over 1 MB.
- The third step saves at quality 85 first, then lowers the quality by 10 at a time, down to 55, then scales the longest side down by a quarter at a time. An image still over 1 MB at 256 pixels isn't saved.
- PNG images end in `.png`, and JPEG images in `.jpg`.
- The app passes in each image's alt text, and Tasks escapes any bracket or backslash in it, so the alt text shows as written.

Git history keeps every task and image ever saved, so the repository only grows. This is an [accepted risk](overview.md#risks-the-later-rows-must-resolve); the 1 MB cap limits each image.

### A draft waits on this device until it's saved

Each device keeps at most one draft of a new task, and Tasks owns all of it, so both apps keep drafts the same way. The draft lives outside the clone, in a folder the app gives Tasks; the desktop app's is [`%LocalAppData%\Memoria-ToDo\draft\`](desktop/task-editor.md#closing-without-saving-keeps-a-draft-on-this-device). It's one JSON file with the fields `version`, `id`, `title`, `description`, and `tags`, where `id` is the task ID the draft got when it started. The draft's images sit beside it. Adding the task writes its file, moves the draft's images into the clone, and then deletes the draft.

Proposed, each a decision to confirm:

- The draft file is `draft.json`, and its images sit in `images/<task-id>/` beside it, as they will in the clone.
- A draft with no title, description, or tags, apart from spaces, is no draft: keeping it deletes the kept draft and its images.
- Keeping a draft replaces the kept one, and deletes images kept for another task ID.
- Adding the task deletes the draft last, so a failed save keeps it.
- A kept draft that can't be read stays as it is until the next keep or discard. Loading it fails with `InvalidDataException`, whether the file was corrupt or couldn't be read from disk, with the cause kept as the inner exception.
- A kept draft whose task ID is already a task was saved just before the app stopped, so Tasks deletes it instead of loading it.

### Tags and search ignore case

A tag is one trimmed, non-empty line. Tags that differ only in case are the same tag, and a new tag takes the spelling of a matching tag in use. A tag suggestion is a tag in use that starts with the text typed so far, ignoring case, so both apps suggest the same tags.

Proposed, each a decision to confirm:

- Tags in use count open and resolved tasks, and list the most-used first, with ties in alphabetical order. Suggestions follow the same order, leave out the tags the task already has, and are empty while nothing is typed.
- A task keeps its tags in the order they were added, and adding a tag it already has changes nothing.
- When tasks spell one tag differently, such as after two offline devices added it, the tags in use and the suggestions spell it as most of those tasks do, and on a tie as the first in alphabetical order.
- Search matches a task when every word typed appears in its title, description, or tags. Empty search text matches every task, and matches come in rank order.
- The tag filter shows the tasks that have the tag.

## Public interface

Each app's modules and Sync use these parts:

- **The task list**: every task, open and resolved, in rank order, with a notice when any task changes here or through sync.
- **Task operations**: add a task at the top, save an edit, move, resolve, reopen, and delete. Each writes the task's file at once.
- **Tags and search**: the tags in use with how many tasks use each, and the tasks matching a search and a tag.
- **Task links**: a task's link, and the task ID in a link's address, if it's a task link.
- **Drafts**: start, load, keep, and discard this device's draft of a new task, with its images.
- **Images**: save an image for a draft or an open edit and get its Markdown, and discard an open edit's images.
- **[Shared settings](../../src/Shared/Tasks/ISharedTaskSettings.cs)**: the typed settings capability used by Configure.
- **[Synchronization](../../src/Shared/Tasks/Synchronization/ITaskSynchronization.cs)**: Sync's separate capability, including its [pure file rules](../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs). The source contracts define initialization, automatic deletion, coordinated updates, reloads, and diagnostics.

The same three versions always merge to the same result, on every device. Tasks reads and writes files through the shared platform's [file system interface](modules.md#the-file-system-interface-reads-and-writes-whole-files), takes the time from .NET's TimeProvider, and publishes an interface for image shrinking. Each app implements the interfaces and passes the system's time, as [How the apps fit together](overview.md#two-apps-share-one-c-library) says.

Step 3 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) builds the part below, in `Memoria.ToDo.Shared.Tasks`; its calls are synchronous. Step 4 renames `SaveDraftImage` to `SaveImage`, and adds `DiscardEditImages`, `SaveEdit`, `Move`, `Resolve`, `Reopen`, `Delete`, the tag filter's `Search`, and `TaskNotFoundException`, all shown below. Step 5 adds the [separate settings and synchronization capabilities](#step-5-publishes-small-capabilities-without-growing-task-operations). `All` and `Search` hold resolved tasks too, in rank order; each screen picks open or resolved tasks by `Resolved`.

```csharp
namespace Memoria.ToDo.Shared.Tasks;

public static class TaskStore
{
    /// deviceFolder: this device's folder for the draft, outside the clone.
    public static ITasks Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder);
}

/// Safe to call from any thread; runs one call at a time.
public interface ITasks
{
    /// Reads the clone's tasks, then raises Changed. Null means no folder is set: the list is empty.
    void UseRepositoryFolder(string? folder);

    /// Every task, open and resolved, in rank order; tasks with the same rank in ID order.
    IReadOnlyList<TaskItem> All { get; }

    /// Raised on the calling thread after any change to All.
    event EventHandler? Changed;

    /// Adds the draft's task at the top, writes its file, moves its images into the clone,
    /// then deletes the kept draft. Throws NoRepositoryFolderException when no folder is set,
    /// and ArgumentException for an empty or multi-line title or an invalid tag.
    TaskItem Add(TaskDraft draft);

    /// The tags in use and how many tasks have each, most-used first, ties in alphabetical order.
    IReadOnlyList<TagUse> TagsInUse();

    /// tags with the typed tag added, spelled as a matching tag in use; unchanged when already there.
    /// Throws ArgumentException for an empty or multi-line tag.
    IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed);

    /// Tags in use that start with typed, ignoring case, except those in tags, most-used first.
    IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags);

    /// Tasks whose title, description, or tags hold every typed word, ignoring case, in rank order.
    IReadOnlyList<TaskItem> Search(string text);

    /// The address a link to the task uses: task:<id>.
    string LinkAddress(string taskId);

    /// The task ID in address, or null when it isn't a task link.
    string? TaskIdIn(string address);

    /// A new, empty draft with a new task ID. Keeps nothing.
    TaskDraft StartDraft();

    /// The kept draft, or null when there's none. Throws InvalidDataException when it can't be read.
    TaskDraft? LoadDraft();

    /// Replaces the kept draft with this one.
    void KeepDraft(TaskDraft draft);

    /// Deletes the kept draft and its images.
    void DiscardDraft();

    /// Shrinks the image to at most 1 MB and returns its Markdown, such as
    /// ![alt](images/<task-id>/<image-id>.png). Keeps it with the open edit when taskId is a task
    /// that already exists, and with the draft otherwise. Throws InvalidDataException when it
    /// isn't an image, and ImageTooLargeException when it can't be shrunk to 1 MB.
    string SaveImage(string taskId, byte[] image, string altText);

    /// Deletes the images kept for the task's open edit. Never touches the draft.
    void DiscardEditImages(string taskId);

    /// Saves an edit of started, the task as the editor got it, onto the task as it is now.
    /// A Title or Description that differs from started's replaces the current one; the tags edited
    /// added or removed, ignoring case, are added to or removed from the current tags. Every other
    /// field keeps its current value. Moves the edit's images into the clone, then writes the file
    /// and sets Updated, unless nothing changed. Returns the task as saved.
    /// Throws, before any change: NoRepositoryFolderException when no folder is set;
    /// TaskNotFoundException when the task isn't in All; ArgumentException when edited.TaskId isn't
    /// started.Id, or for an empty or multi-line title or an invalid tag.
    TaskItem SaveEdit(TaskItem started, TaskDraft edited);

    /// Gives the task a rank that shows it after previousTaskId and before nextTaskId; a null ID
    /// leaves that side open, so Move(taskId, null, null) changes nothing. Changes only the task's own
    /// file and sets Updated. Changes nothing when the task already shows there. Returns the task as saved.
    /// Throws TaskNotFoundException when any ID isn't in All, and ArgumentException when a neighbor
    /// is the task itself, previousTaskId doesn't show before nextTaskId, or the neighbors share a rank,
    /// so no rank of the task's own shows it between them.
    TaskItem Move(string taskId, string? previousTaskId, string? nextTaskId);

    /// Sets Resolved and Updated to now. A resolved task stays as it is. Returns the task as saved.
    /// Throws TaskNotFoundException when the task isn't in All.
    TaskItem Resolve(string taskId);

    /// Clears Resolved and sets Updated to now. An open task stays as it is. Returns the task as saved.
    /// Throws TaskNotFoundException when the task isn't in All.
    TaskItem Reopen(string taskId);

    /// Deletes the task's file, then its images in the clone, images/<task-id>/, then any images
    /// kept for its open edit. Once the file is deleted, the task is gone from All and Changed is
    /// raised, even when a later step throws. Throws TaskNotFoundException when the task isn't in All.
    void Delete(string taskId);

    /// Search(text), narrowed to the tasks that have tag, ignoring case.
    /// Throws ArgumentException for an empty or multi-line tag; Search(text) filters by no tag.
    IReadOnlyList<TaskItem> Search(string text, string tag);
}

public sealed record TaskItem(string Id, string Title, string Description, string Rank,
    IReadOnlyList<string> Tags, DateTimeOffset Created, DateTimeOffset Updated, DateTimeOffset? Resolved);

public sealed record TaskDraft(string TaskId, string Title, string Description, IReadOnlyList<string> Tags);

public sealed record TagUse(string Tag, int Count);

public sealed class NoRepositoryFolderException : InvalidOperationException { }

public sealed class ImageTooLargeException : InvalidOperationException { }

/// The task isn't in All, for example because it was deleted.
public sealed class TaskNotFoundException : InvalidOperationException { }

/// Each app's image tools. Tasks runs the shrinking steps through it.
public interface IImageEncoder
{
    /// The image's format and size. Throws InvalidDataException when it isn't an image.
    ImageInfo Read(byte[] image);

    /// The image as PNG, scaled so its longest side is at most longestSide pixels.
    byte[] SavePng(byte[] image, int longestSide);

    /// The image as JPEG at quality 1–100, scaled so its longest side is at most longestSide pixels.
    byte[] SaveJpeg(byte[] image, int longestSide, int quality);
}

public enum ImageFormat { Png, Jpeg, Other }

public sealed record ImageInfo(ImageFormat Format, int Width, int Height);
```

### Step 5 publishes small capabilities without growing task operations

The source contracts below define the approved implementation boundary. They are the reference for callers;
this section describes the design, not a claim that final validation or delivery has finished.

| Contract | What its caller gets |
| --- | --- |
| [`ITaskStore`](../../src/Shared/Tasks/ITaskStore.cs) | Existing `ITasks` operations, plus `Settings` and `Synchronization` |
| [`ISharedTaskSettings`](../../src/Shared/Tasks/ISharedTaskSettings.cs) | Three typed values, one setter each, and a settings-change notice |
| [`ITaskSynchronization`](../../src/Shared/Tasks/Synchronization/ITaskSynchronization.cs) | Selected folder, skipped files, local-save notice, settings initialization, automatic deletion, and exclusive local updates |
| [`IRepositoryFileRules`](../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs) | Canonical managed paths and pure three-version task/settings byte merges |
| [`ISkippedRepositoryFile`](../../src/Shared/Tasks/Synchronization/ISkippedRepositoryFile.cs) | Immutable relative path, reason, and whether updating the app is needed |

The [factory contract](../../src/Shared/Tasks/TaskStore.cs) exposes these capabilities through `ITaskStore`.
Existing callers can still use the result as `ITasks`; Configure receives only `Settings`, and
Sync receives only `Synchronization`. No caller casts a concrete implementation or creates another store.

`IRepositoryFileRules` is the only public home of the managed directory/file names and their classification.
Its implementation reuses `RepositoryLayout` and the existing codecs. A Git-relative path uses `/`, is never rooted,
and contains no `.` or `..` segment. Task and settings merges do no I/O and reject unreadable inputs rather than
returning a success-shaped fallback. Sync catches that failure per file, reports it, and leaves its bytes intact,
except for the user-approved [bounded Sync discard policy](sync.md#each-sync-commits-fetches-merges-and-pushes-one-branch).

Approved: the task-merge contract receives the expected task ID from the classified repository path. Tasks checks
every present input against that ID, including the basis and a surviving side of a deletion, before merging.
The [source contract](../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs) owns the signature and failure
behavior. Sync supplies the ID through the same file-rules capability; it never duplicates Tasks' payload parser.

Low-risk clarification to confirm: an invalid expected-ID argument is a caller error, rejected before payload
inspection even when all inputs are absent. The source contract defines its exception; malformed file content
remains a separate data error. This uses Tasks' existing canonical-ID validation, not another ID format.

Proposed: `SaveImage`'s two exceptions, so the editor can say why an image wasn't saved. This is a decision to confirm.

Also proposed for step 4, each a decision to confirm:

- `SaveEdit` compares a Title after trimming, and a Description after storing its line breaks as `\n`, so only a real change counts. It adds tags in the edit's order, spelled as `WithTag` spells them.
- `SaveEdit` of a task that's no longer in `All` throws, so a save never undoes a deletion that arrived while the edit was open. The editor keeps the text, so the user can copy it.
- `SaveEdit` moves the edit's images first, so a failed write leaves them in the task's folder, and a retry works. An image in the task's folder with no link is kept, as the rule above says.
- When `Move`'s neighbors share a rank, which only two offline devices adding tasks can cause, no key sorts between them, and a move changes only its own file. So `Move` refuses, unless the task already shows there, and the Task list shows the move failed. A later design can re-rank a neighbor instead.
- `Move` with no neighbors changes nothing, even when other tasks exist, such as hidden by a filter that shows only this task.
- `Search(text, tag)` refuses an empty tag instead of treating it as no filter, so a caller's mistake shows; a screen with no tag filter calls `Search(text)`.
- `Delete` stops at the first step that fails and throws its error, with no new exception type. Deleting the file is the step that deletes the task: if it fails, the task and its images stay as they were; once it succeeds, the task is deleted even when removing its images then fails, and those images stay in the clone. A caller tells the two apart by whether the task is still in `All`. Like every call, `Delete` does all its file work while no other call's file work runs, so its last step can't delete an image saved after the file was deleted, such as for a task with the same ID in another repository. Then it raises `Changed`, including when a step after the file's deletion failed. As with every call, `Changed` is raised once other calls can run again, so another call may run before the handlers do; Tasks never holds other calls back while handlers run.
- `Resolve` and `Reopen` raise `Changed` only when they change the task, and `Move` and `SaveEdit` likewise.

## Failure modes

- **No clone folder is set**: adding a task fails before any file is written, and the screen that asked keeps the text and points to where the folder is set.
- **A task file can't be read**, such as broken JSON from the remote: Tasks skips the task and never deletes its file, and the sync status names it.
- **A task file has a newer `version`**: Tasks skips it and leaves it unchanged, and the sync status asks the user to update the app.
- **A task file can't be written**, such as on a full disk: the screen that asked shows the error and keeps the user's text, and the draft stays.
- **An image can't be read, or can't be shrunk to 1 MB**: nothing is saved, and the editor says why.
- **The kept draft can't be read**: loading it fails with `InvalidDataException` (a read failure keeps its cause inside), and the file stays as it is until the next keep or discard. The Add window then opens empty and says so.
- **Adding a task fails after its file is written**, while moving images or deleting the draft: Tasks undoes what it did, moving the images back and deleting the new task file, and rethrows the original error, so the draft stays whole, nothing is left in the repository, and a retry works. The draft's images folder is deleted before its file, so a failed draft delete is undone the same way. If the undo itself fails, Tasks throws an `AggregateException` with both errors. Adding never overwrites: if the task file's path is already occupied, even by a file that was skipped on load, it throws `ArgumentException`.
- **The top task's rank isn't a valid key**: adding fails with `InvalidDataException` rather than guess an order.

Proposed, each a decision to confirm: `Add` refuses a draft whose ID is already a task or whose task file path is occupied (`ArgumentException`); the `AggregateException` when an undo also fails; task and draft files with an ID that isn't a lowercase UUID are unreadable; a description's line breaks are stored as `\n`; times are stored to the millisecond; draft images are deleted on discard for the kept draft and for every ID that got an image since the app started, including a draft never kept; images left by an earlier run for a draft that was never kept, or whose draft file is unreadable, aren't cleaned up, because the file system can't list folders. Known limitation, not fixed: `Add` checks that the task file path is free and then writes, so another process creating that file in between could be overwritten; this is a single-user local app, and closing the gap needs a create-only `IFileSystem` operation, a public interface change.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Repository layout:
  - **A folder per task, `tasks/<id>/task.json`**: the file would no longer be named by its ID, unlike the decided format.
  - **One flat `images/` folder, with files named `<task-id>-<n>`**: deleting a task would mean matching file names.
- Task ID:
  - **A random UUID version 4**: files would list in random order.
  - **A short 10-character random ID**: its own generator, with a tiny chance of collision.
- Rank:
  - **Whole numbers with gaps**: renumbering touches many files and invites conflicts.
  - **One order file listing the IDs**: every move and add changes it, so offline devices conflict.
- Merging:
  - **The later edit wins, and the other Description is kept in a warning block**: not chosen.
  - **A line-by-line merge with conflict markers**: it needs a diff library, and the markers end up in the task.
  - **The deletion wins over an edit**: the edit would survive only in git history.
  - **Saving an edit writes every field the editor holds**: it would undo a change that arrived while the edit was open.
- Automatic deletion:
  - **Delete only when both the resolution and the last edit are older than the delay**: offered, and not chosen.
- Image format:
  - **Always JPEG**: screenshot text blurs, and transparency is lost.
  - **WebP everywhere**: WPF has no WebP encoder ([BitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapencoder)), so Windows would need a new imaging library such as SkiaSharp.
- Where tasks live before the first clone:
  - **A local folder until Sync is set up**: the tasks would have to move into the clone later.
  - **Add turned off until a folder is set**: Add would depend on Configure's state, through Shell.
- Who keeps the draft:
  - **The editor keeps the text, and Tasks keeps the images**: the draft would be split in two places, and Android would repeat the editor's format.
  - **A device store in the platform**: no second module needs one.
- Tag suggestions:
  - **A matching rule in the shared platform**: only Tasks matches tags and tasks, so the rule stays in Tasks.
- Images for an open edit:
  - **The editor says whether it holds a draft or a task, and calls a separate image part for edits**: an extra setting on the editor, and two image parts that shrink images the same way.
  - **Images added during an edit go straight into the clone**: a sync during the edit could push images that a discard then deletes, and git history would keep them.
