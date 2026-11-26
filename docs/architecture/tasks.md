# Tasks: task files, their merge rules, and images

Status: Decided

Tasks is the shared module that owns every task: its file, ID, and rank, how two devices' versions of it merge, automatic deletion, images, the draft each device keeps, and the settings, tag colors, and tag order every device shares. Both apps change tasks only through Tasks.

## Behavior

[Purpose and scope](purpose.md#each-task-is-a-json-file-with-a-few-properties) decides what a task holds, and [Sync through git](sync.md) decides when changes travel.

### The repository holds task files, image folders, settings, and tag colors

The table lists what Tasks keeps in the task repository:

| Path | What it holds |
| --- | --- |
| `tasks/<id>.json` | One task, in a file named by the task's ID |
| `images/<id>/` | That task's images, one file each |
| `settings.json` | The deletion delay and the desktop app's shortcut texts, whose validation belongs to [Configure](desktop/configure.md#each-setting-has-one-owner) |
| `tags.json` | The color the user picked for each tag, and its place in the tags' order, as [below](#each-tags-color-is-kept-in-one-synced-file) |

Deleting a task removes its file and its image folder. The clone also holds the user's contacts under `autocomplete/`, which [Contacts](contacts.md#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts) owns; Tasks never reads them.

### Tasks works on the clone the app gives it

Each app selects this device's clone when it starts, and again whenever the user picks another, through the shared platform's [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), which Tasks joins as one of its parts; on the desktop, that's [Configure's repository folder](desktop/configure.md#the-sync-settings-section-sets-the-devices-clone). Tasks then reads that folder's tasks and announces the change. With no folder, the list is empty, and adding a task fails before any file is written, so the screen that asked can point the user to Configure and keep the text.

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
| `tags` | The task's tags, in the user's order, where the first one groups the task, as [A task's tags are an ordered list](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) says |
| `created` | When the task was added |
| `updated` | When the task last changed, on any device |
| `resolved` | When the task was resolved, or `null` while it's open |
| `version` | The file format's version, starting at 1 |

- Times are written to the millisecond, and each change on a device [moves `updated` strictly forward](#direct-task-changes-are-a-capability-of-their-own).
- A newer app reads every older `version`. An app that finds a newer `version` skips the file and leaves it unchanged, by the platform's [version and skip rules](modules.md#stores-share-one-json-style-and-one-set-of-file-rules).

Proposed, each a decision to confirm:

- Tasks writes each file in the platform's [JSON style](modules.md#stores-share-one-json-style-and-one-set-of-file-rules), its fields in the table's order, so one change shows as a few changed lines in git.
- A title is one line: Tasks trims spaces at both ends, and rejects an empty title or one with a line break. A description keeps the user's text, with each line break stored as `\n`, so both apps write the same file.
- A new task's `created` and `updated` are the same time, and its `resolved` is `null`.

### A time-ordered ID names each task, its file, and links to it

A task's ID is a universally unique identifier (UUID), version 7, which starts with the time it was made ([RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-7)). So task files list in the order they were added, and two devices never make the same ID, even offline. .NET makes it with [`Guid.CreateVersion7()`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7), so it needs no new dependency.

- The ID is written in lowercase with hyphens: 36 characters.
- A link to another task is `[link text](task:<id>)`. Tasks owns the `task:<id>` address; [Editing's task reference](editing.md#snippets-insert-common-blocks) writes the link around it and decides the link text.

Proposed: [Rendering's browser check](rendering.md#only-web-links-open-in-the-browser) rejects `task:` addresses, and the app opens the task instead. This is a decision to confirm.

### Rank is a text key that sorts between its neighbors

Each task's rank is a text key that sorts between the keys of the tasks before and after it, a fractional index ([Figma](https://www.figma.com/blog/realtime-editing-of-ordered-sequences/)). Moving a task changes only its own file, so two offline devices that move different tasks never conflict. When two keys are equal, the task IDs set the order. A new task gets a key before all others, so it goes to the top, as the [Task editor](desktop/task-editor.md#a-new-task-goes-to-the-top) decides.

Adding or moving a task never gives it a key another task holds. Every move bounds its new key by the tasks really next to it among every task, open or resolved, including those in other groups or hidden by a filter, even when a task of another group sits between the two neighbors a screen gives, as [`Move`](#public-interface) says. So equal keys come only from two devices that add or move tasks while offline.

Keys use the 62 digits and letters, compare character by character, and are never renumbered. Adding at the top keeps keys short; only repeated moves into one gap lengthen a key, by about one character per six moves.

Proposed: a key starts with a letter that says how long its whole-number part is, as the [fractional-indexing](https://github.com/rocicorp/fractional-indexing) library's keys do. The first task gets `a0`, and each new task at the top lowers the whole number by one, so the first 63 tasks get 2-character keys and the next 3,844 get 3. Characters compare by their code, so digits sort before capitals, and capitals before small letters. This is a decision to confirm.

### Two versions of a task merge field by field

When two devices changed the same task, Tasks compares each version with the one both started from, git's [merge base](https://git-scm.com/docs/git-merge-base), one field at a time. The table lists each case:

| Case | Result |
| --- | --- |
| A field changed on one device only | That device's change |
| Title, Description, Tags, or Rank changed on both | The later edit wins, and the other is dropped |
| One device deleted the task, and the other edited it | The edit wins: the task comes back, with the edit |
| One device resolved the task, and the other edited it | The task stays resolved and keeps the edit |
| Both deleted the task | It stays deleted |

An edit can bring back a task that another device deleted. If that task is still resolved past the deletion delay, the next sync deletes it again, unless the user reopens it first.

Saving an edit changes only what the edit changed, so it never undoes a change that arrived while the edit was open. Tasks compares the edit with the version the editor started from: a Title, Description, or tag list the edit changed replaces the current one, as [A task's tags are an ordered list](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) says for the tags. Every field the edit didn't touch keeps its current value.

The user decided that a task's tags merge as a Title does: a change on one device wins, and when both changed them, the later edit wins. The whole list is one field, and its order is part of its value, so a reorder is a change. A reorder on one device and an added tag on another then conflict, and the later edit wins. Lists compare after [normalizing](#tags-are-lowercase-with-hyphens-and-search-ignores-case), so `Work` on one side and `work` on the other are no change.

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
A present file must hold the first three settings with valid types and times; `closeWindowShortcut` and `saveShortcut` may be missing, as below. An unsupported version or unreadable file
is reported, left untouched, and cannot be replaced by a setter or initialization.

With no file, reads supply the existing deletion default and null shortcuts without writing. `EnsureSettings` creates
the file during synchronization. Setters also create a missing file, changing only the selected setting's time.
A setter with an unchanged value does nothing. With no selected clone, reads supply defaults and setters fail before writing.

Decision to confirm: untouched defaults carry `1970-01-01T00:00:00.000Z`, so initialization cannot overwrite a
setting another device explicitly changed. A setting changed by the user gets the current time from TimeProvider.

An unreadable settings file does not prevent readable tasks from loading. It makes settings reads and writes fail
with `InvalidDataException`, and prevents automatic deletion, rather than deleting tasks using a guessed delay.

A fourth setting,
`closeWindowShortcut`, after `manageTasksShortcut`, is kept and merged like the other shortcut texts. It may be missing
from a present file, which reads as null with the untouched-default time, so files written before it stay readable.
Every write includes it. Its public parts sit beside the other two shortcuts':

```csharp
/// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
string? CloseWindowShortcut { get; }

/// <summary>
/// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
/// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
/// </summary>
void SetCloseWindowShortcut(string? shortcut);
```

A fifth setting, `saveShortcut`, after `closeWindowShortcut`, holds the [Save shortcut](desktop/configure.md#the-save-shortcut-saves-an-edit-in-any-window), as the user decided. It's kept, merged, read when missing, and written exactly as `closeWindowShortcut` is, and its public parts follow the same pattern:

```csharp
/// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
string? SaveShortcut { get; }

/// <summary>
/// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
/// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
/// </summary>
void SetSaveShortcut(string? shortcut);
```

The user decided that a sixth setting, `configureShortcut`, after `saveShortcut`, holds the desktop's global shortcut that opens Configure, with the version still 1. It's kept, merged, read when missing, and written exactly as `saveShortcut` is, and its public parts follow the same pattern:

```csharp
/// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
string? ConfigureShortcut { get; }

/// <summary>
/// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
/// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
/// </summary>
void SetConfigureShortcut(string? shortcut);
```

The user decided Contacts has a fourth global shortcut, which Configure changes like the others. So a seventh setting, `contactsShortcut`, after `configureShortcut`, holds the desktop's global shortcut that opens [Contacts](desktop/contact-list.md#a-global-shortcut-and-the-tray-menu-open-contacts), with the version still 1. Proposed, a decision to confirm: it's kept, merged, read when missing, and written exactly as `configureShortcut` is, and its public parts follow the same pattern, after `SetConfigureShortcut`. The desktop's step 15 built it in slice (e), with the Contacts shortcut, in [`ISharedTaskSettings.cs`](../../src/Shared/Tasks/ISharedTaskSettings.cs):

```csharp
/// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
string? ContactsShortcut { get; }

/// <summary>
/// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
/// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
/// </summary>
void SetContactsShortcut(string? shortcut);
```

### Sync shares the repository's lock, never a network wait

Sync updates the clone through the shared platform's [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), whose lock Tasks also takes for every read and write, and Contacts too. The repository checks that the expected clone is still selected before calling Sync's synchronous
callback under the lock. A changed selection fails before the callback runs. Commit and merge application
use this operation; fetch, push, and every other network wait stay outside it. Sync rechecks and commits intervening
local edits before applying a fetched merge.

The repository reloads every part while still holding the lock, including after a failed callback; Tasks' part reloads tasks, settings, tag colors, and skipped-file diagnostics. It then releases the lock before raising notices. A callback failure propagates; a reload failure too
is kept with it in `AggregateException`. This is local coordination, not a transaction that undoes a failed Git update.

Tasks reports a local save to the repository after durable local repository changes, including settings, tag colors, and automatic deletion. It reports none for repository
selection, imported updates, sync's settings initialization, drafts, and images still waiting outside the clone. The existing task `Changed` notice
continues to refresh screens; settings and tag colors each have their own notice. Every notice and the report run even when one fails, as [Failure modes](#failure-modes) says. A reload never starts another sync by pretending to be
a local save. Skipped-file snapshots name corrupt, unreadable, and newer-version files without deleting them, by the platform's [skip rule](modules.md#stores-share-one-json-style-and-one-set-of-file-rules).

Decisions to confirm: a reload always raises the settings-change notice, even when values are unchanged;
`EnsureSettings` raises no `LocalSaved` notice because initialization is already part of a sync pass. Proposed: a reload
always raises the tag-color notice too, the same way. This is a decision to confirm.

### Images are at most 1 MB and live beside their task

Images are PNG for screenshots and images with transparency, and JPEG for photos. Each platform's own image tools shrink them, so the apps need no imaging library: Windows Presentation Foundation (WPF) imaging ([BitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapencoder)) and Android's [Bitmap](https://developer.android.com/reference/android/graphics/Bitmap.CompressFormat). An image over 1 MB is shrunk in these steps:

1. Its longest side is scaled down to at most 1920 pixels.
2. A PNG still over 1 MB is saved as JPEG.
3. A JPEG still over 1 MB is saved at a lower quality, then scaled down further, until it fits.

Each of these rules also holds:

- A JPEG file stays JPEG; an image from the clipboard or in any other format starts as PNG.
- Each image is named by its own UUID version 7, so two offline devices never pick the same name. A description links to it from the clone's root, such as `![alt](images/<task-id>/<image-id>.png)`, and [Rendering loads it from the clone's address](rendering.md#the-web-view-runs-no-scripts-and-loads-images-only-from-the-clones-address).
- An image is deleted only with its task, even when no link to it is left, because a merge can drop the only link.
- An image added to a draft or an open edit waits outside the clone, and moves into the task's folder on save. Discarding deletes it. A draft gets its task ID when it starts, so its image links are final.
- Tasks tells an open edit's image from a draft's by its task ID: an image for a task that already exists belongs to that task's open edit. It waits apart from the draft's images, so keeping, discarding, or adding the draft never deletes it. Both apps' editors keep every image through the same call.
- An open edit's images wait in one folder on this device, laid out as the clone, so the link an image gets names it there too, before Save, and in the clone after. Tasks publishes where that folder is, and nothing else about it, so the desktop's web view can [show an image in an edit's Preview before Save](desktop/formatted-view.md#the-clones-address-also-serves-an-open-edits-waiting-images). The user decided that the web view serves those images. Proposed: the folder is a read-only property of the store, not of every task operation. This is a decision to confirm.

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

### Tags are lowercase with hyphens, and search ignores case

The user decided that every tag is **normalized**, so two tags can never differ only in case or spacing. Normalizing a tag:

1. trims it,
2. lowercases it by the invariant culture's rules, so a tag reads the same on every PC and phone,
3. replaces each run of whitespace, line breaks included, with one hyphen.

So `My  Work` becomes `my-work`, and `Work`, `work`, and ` WORK ` are all `work`. Tasks normalizes every tag it gets, whoever sends it: the user, the Local API, or the MCP server. It also normalizes every tag it reads from a task file, a draft, or `tags.json`. Tasks publishes the rule as [`TagName.Normalize`](#public-interface), so the desktop's tag box applies the same rule as the user types. With every tag normalized, tags compare as plain text, character by character. Decided by the session, a decision to confirm: every comparison that ignored case before now compares normalized tags ordinally.

Decided by the session, each a decision to confirm:

- **A tag that's empty after normalizing is rejected**, with `ArgumentException`, before any file is written. The Local API and the MCP server turn that into their own errors.
- **Normalizing on read never rewrites a file.** A task file that holds `Work` keeps it on disk until the task's next save, which writes `work`. So opening the app after this change doesn't rewrite every task and push it through sync.
- **Whitespace inside a tag becomes a hyphen, line breaks included**, so a multi-line tag is no longer an error: `to\ndo` becomes `to-do`.

A tag suggestion is a tag in use that contains the typed text anywhere, so `ork` suggests `work`, and both apps suggest the same tags. The user decided it matches anywhere, not only at the start, for the desktop's tag [Search](desktop/task-editor.md#a-click-on-a-pill-opens-the-tag-menu). Decided by the session, a decision to confirm: the typed text is normalized before matching, so `My w` suggests `my-work`.

Proposed, each a decision to confirm:

- Tags in use count open and resolved tasks, and list the most-used first, with ties in alphabetical order. Suggestions follow the same order, leave out the tags the task already has, and are empty while nothing is typed.
- A task's tags keep the order the user gave them. A tag added with `WithTag` goes at the end of the list, so adding a tag never changes the task's [group tag](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) unless the task had none.
- Search matches a task when every word typed appears in its title, description, or any of its tags, first or not, ignoring case. Empty search text matches every task, and matches come in rank order.
- Tags in use count every tag of every task, first or not, so the desktop's tag menu offers a tag the user only ever puts second.

The user decided that the tag filter, `Search(text, tags)`, shows the tasks that have every tag it names, each at any place in the task's list. Its tags are normalized, and a repeated one counts once.

### A task's tags are an ordered list, and the first one groups it

A task has any number of tags, zero included, in an order the user sets. The first tag is the task's **group tag**: the desktop's [Task View](desktop/task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) shows the task only in that tag's group. Every tag, first or not, counts when the user searches, and when the Local API or the MCP server selects tasks by tag. The desktop shows the tags as [pills the user drags into order](desktop/task-editor.md#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them).

This replaces the earlier rule that a task has at most one tag. The user decided these rules:

- **Tasks keeps the rules**, so both apps, the Local API, the MCP server, and every merge hold them, not only one screen.
- **The file's `tags` field stays a list, and the file `version` stays 1.** The list already holds the order, so no stored task changes, and an app not yet updated still reads every task, rather than [skipping a newer version](#failure-modes).
- **The group tag is the list's first entry**, or none when the list is empty. `TaskItem.GroupTag` gives it, so every caller reads it the same way. A task with no tags shows in Task View's "No tag" group.
- **A task's tags are [normalized](#tags-are-lowercase-with-hyphens-and-search-ignores-case), and a repeated tag is dropped, keeping its first place.** This holds for a list read from a file and for one a caller gives, with no error: `Work, home, work` becomes `work, home`.
- **A task has at most 15 tags.** The count is taken after normalizing and dropping repeats. The user decided the limit has one home, the public constant `TagName.MaxPerTask`, which the desktop's tag control and the test fakes read rather than keep a copy.
- **Saving an edit replaces the tags only when the edit changed them.** Tasks compares the edit's list with the list the editor started with, in order, so a reorder is a change. When they differ, the edit's whole list replaces the current one. Otherwise the current tags stay, so tags set meanwhile, such as on another device while the edit was open, stay. Tasks logs at Debug whether it kept or replaced the tags.
- **Two versions' tags merge as one field, and the later edit wins**, as [the merge](#two-versions-of-a-task-merge-field-by-field) says. A reorder on one device and an added tag on another keep only the later of the two.

Decided by the session, each a decision to confirm:

- **More than 15 tags is rejected** with `ArgumentException`, before any file is written, by `Add`, `SaveEdit`, `WithTag`, and the [direct changes](#direct-task-changes-are-a-capability-of-their-own). A task file that already holds more, such as one written by hand, still loads every tag, and a save that doesn't change its tags, or a merge, keeps them all; only a new tag list is limited. The desktop's add pill is [turned off at 15](desktop/task-editor.md#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them).
- **Tasks publishes the normalizing rule as `TagName.Normalize`**, a static method, since the desktop's tag box rewrites typed text by the same rule and must not keep a copy of it.

Proposed, each a decision to confirm:

- **A kept draft keeps every tag, in order**, so Add saves the list the user built.
- **Each tag keeps its own [color](#each-tags-color-is-kept-in-one-synced-file)**, wherever it sits in a task's list.
- **The [tag order](#each-tags-place-orders-task-views-groups-and-androids-tag-chips) orders the groups, not the tags inside a task.** A task's list keeps the user's order, whatever the tags' places are.

The desktop's tag control saves through `SaveEdit`. Outside an edit, it saves at once; during a description or title edit, the tags [join that edit's one save](desktop/task-list.md#tags-changed-during-an-edit-save-with-the-edit), so the text and the tags save or fail together. The user decided that **`SaveEdit` moves the images waiting for the task's open edit into the clone on any save that changes something**.

An app not yet updated reads a task with several tags as it always has: its Task View shows the task in every tag's group, and its one-tag editor shows the first tag. Choosing a tag there replaces the whole list with that one tag. The user accepted this cost, since one person updates all their own devices, and a wrong grouping on an old device is easier to live with than missing tasks.

### Each tag's color is kept in one synced file

The user decided that each tag's color is kept in `tags.json`, a file in the clone that syncs like `settings.json`, so a tag looks the same on every device. The same file keeps each tag's [place](#each-tags-place-orders-task-views-groups-and-androids-tag-chips), so everything the user sets for a tag lives in one entry. Tasks keeps a color's name as text without interpreting it, as it keeps shortcut text, since Tasks uses no other module. [Rendering](rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) names the colors, and picks the color of a tag with none.

The file is one object with these fields, in this order:

| Field | Value |
| --- | --- |
| `version` | `2` |
| `tags` | A list of entries, each an object with `tag`, the [normalized](#tags-are-lowercase-with-hyphens-and-search-ignores-case) tag; `color`, an object with `value`, the color's name, and `updated`, a UTC timestamp, or `null` when no color was picked; and `place`, an object with `value`, a [rank key](#rank-is-a-text-key-that-sorts-between-its-neighbors), and `updated`, or `null` when the tag was never moved |

For example, a tag the user colored and then moved:

```json
{
  "version": 2,
  "tags": [
    {
      "tag": "work",
      "color": { "value": "teal", "updated": "2026-11-12T08:30:15.123Z" },
      "place": { "value": "a0", "updated": "2026-11-16T17:02:44.010Z" }
    }
  ]
}
```

Version 1 has the same `tags` list, but each entry holds `tag`, `color` as the bare name, and `updated`, the color's time. Tasks reads it as entries with that color and no place.

The user decided that the place lives in `tags.json`, as version 2, not in a file of its own, and accepted the cost. Tasks writes version 2 every time it writes the file, whether for a color, a move, or a merge. An app older than this design [skips a newer version](#failure-modes), so once version 2 reaches it, every tag shows its default color there, and picking a color fails, until the user updates that app. Its sync names the file, asks the user to update, and leaves the file as it is, so no color or place is lost.

Two versions of the file merge one entry at a time, matching normalized tags. An entry's color and place merge apart:

- **An entry on one side only is kept.** A missing entry or an absent file never deletes one.
- **An entry on both sides takes the color with the later `updated` time, and, apart from it, the place with the later `updated` time.** Equal times take the remote's. So a color picked on one device and a move made on another both survive.
- **Three absent files merge to no file.** A present file that can't be read fails the merge, as a settings file does.

Sync carries `tags.json` as it carries `settings.json`: Tasks' [file rules](#tag-colors-are-a-capability-of-their-own) classify the file and merge its versions, and Sync commits, merges, and pushes it. Places need no new file kind and no change to Sync.

Proposed, each a decision to confirm:

- The file uses the [task file's JSON style](#each-task-file-holds-nine-fields), with its entries sorted by tag, ordinally, so a recolor or a move shows as a few changed lines.
- An entry is written only when the user picks a color or moves a tag, and is never deleted, so a tag that goes out of use and comes back keeps its color and its place. A clone has no `tags.json` until the first pick or move; settings initialization doesn't create one.
- Picking a color writes the normalized tag, the color, and the current time, and keeps the entry's place. Picking the color a tag already has writes nothing.
- A present file is unreadable when an entry's tag is empty after normalizing, its color isn't one non-empty line, its place isn't a valid rank key, a time isn't valid, or it has neither a color nor a place. A newer app reads every older `version`, and an app that finds a newer one skips the file, as for task files.

The user decided that a color is looked up by the normalized tag. Decided by the session, each a decision to confirm:

- **Reading `tags.json` normalizes each entry's tag, and when two entries then name the same tag, the first in the file wins** and the other is dropped. A file written before tags were normalized, holding `Work` and `work`, still loads, and keeps one color and one place for `work`.
- **Reading never rewrites the file**, as for task files. The next color pick or move writes every entry normalized, without the dropped ones.
- **A tag whose spelling changes by normalizing keeps its kept color**, since its entry is normalized too. A tag with no kept color that held a space gets a different default color, since `my work` becomes `my-work`. Case alone changes nothing, since [Rendering](rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) picks the default from the tag in uppercase.
- While `tags.json` can't be read, every tag shows its default color and has no place, and picking a color or moving a tag fails without writing, as [failure modes](#failure-modes) say. Showing colors and order never stops the task list from loading.

### Each tag's place orders Task View's groups and Android's tag chips

The user decided that each tag has its own place: a [rank key](#rank-is-a-text-key-that-sorts-between-its-neighbors) with the time it was set, kept in the tag's [`tags.json` entry](#each-tags-color-is-kept-in-one-synced-file). The desktop's Task View shows its [tag groups](desktop/task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) in this order, and the Android app's Task View its [tag chips](android/task-list.md#tags-and-search-narrow-the-list). Tasks owns the sort rule, so both apps show the same order with no copy of it.

Tasks sorts tags this way, as the user decided:

1. **Tags with no place come first**, at the top, as a [new task goes to the top](#rank-is-a-text-key-that-sorts-between-its-neighbors), in alphabetical order. With nothing placed, the order is alphabetical, and a new tag shows first, where the user sees it.
2. **Then the placed tags, by place.** Places compare as task ranks do; equal places sort by tag.

Tags sort as [normalized](#tags-are-lowercase-with-hyphens-and-search-ignores-case) text, ordinally. Decided by the session, a decision to confirm: sorting normalizes the tags it's given and drops a repeat, rather than rejecting the same tag twice, as every other call that takes tags does.

A task with no tag has no tag to sort. Where an app shows such tasks, such as the desktop's "No tag" group, is that app's choice.

A move gives the moved tag a new place between its two neighbors' places, with the current time. Only that tag's entry changes, so two devices that move different tags never conflict, and when both move the same tag, the later move wins. A move to where the tag already shows writes nothing.

When a neighbor has no place yet, or both neighbors share one, the move first gives a new place to every tag in use, every tag the file already places, the moved tag, and its neighbors, in the order sorting shows now. Then it places the moved tag. So the tag lands exactly where it was dropped, and unused placed tags keep their order among the rest. The user decided this, and accepted two costs. Unused placed tags get a new time, which can win over another device's move of them. And two devices making their first move offline may interleave their orders; each entry still merges cleanly.

## Public interface

Each app's modules and Sync use these parts:

- **The task list**: every task, open and resolved, in rank order, with a notice when any task changes here or through sync.
- **Task operations**: add the draft's task at the top; save an edit, move, resolve, reopen, and delete. Each writes the task's file at once.
- **[Direct task changes](#direct-task-changes-are-a-capability-of-their-own)**: add a task from a title, description, and tags, update it, and add an image to it, for a caller with no editor, without touching the draft or an open edit.
- **Tags and search**: the tags in use with how many tasks use each, a task's group tag, and the tasks matching a search and a set of tags.
- **Task links**: a task's link, and the task ID in a link's address, if it's a task link.
- **Drafts**: start, load, keep, and discard this device's draft of a new task, with its images.
- **Images**: save an image for a draft or an open edit and get its Markdown, discard an open edit's images, and the folder where an open edit's images wait.
- **[Shared settings](../../src/Shared/Tasks/ISharedTaskSettings.cs)**: the typed settings capability used by Configure.
- **[Tag colors](#tag-colors-are-a-capability-of-their-own)**: the color name kept for a tag, and keeping a new one.
- **[Tag order](#tag-order-is-a-capability-of-its-own)**: tags sorted in the user's order, and moving a tag to a new place.
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
    /// Every task, open and resolved, in rank order; tasks with the same rank in ID order.
    IReadOnlyList<TaskItem> All { get; }

    /// Raised on the calling thread after any change to All.
    event EventHandler? Changed;

    /// Adds the draft's task at the top, writes its file, moves its images into the clone,
    /// then deletes the kept draft. Throws NoRepositoryFolderException when no folder is set,
    /// and ArgumentException for an empty or multi-line title, a tag that's empty once normalized, or more
    /// than 15 tags. Keeps the draft's tags in order, normalized, each repeat dropped.
    TaskItem Add(TaskDraft draft);

    /// The tags in use and how many tasks have each, most-used first, ties in alphabetical order.
    IReadOnlyList<TagUse> TagsInUse();

    /// tags with the typed tag added at the end, normalized; unchanged when already there.
    /// Throws ArgumentException for a tag that's empty once normalized, or when tags already holds 15.
    IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed);

    /// Tags in use that contain typed, normalized, anywhere, except those in tags, most-used first.
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
    /// A Title or Description that differs from started's replaces the current one; a tag list that
    /// differs from started's, in order, once normalized, replaces the current list whole. Every other
    /// field keeps its current value. Moves the edit's images into the clone, then writes the file
    /// and sets Updated, unless nothing changed. Returns the task as saved.
    /// Throws, before any change: NoRepositoryFolderException when no folder is set;
    /// TaskNotFoundException when the task isn't in All; ArgumentException when edited.TaskId isn't
    /// started.Id, or for an empty or multi-line title, a tag that's empty once normalized, or more than 15 tags.
    /// When a move or the write fails, moves every image it moved back to the edit and rethrows the
    /// failure, so nothing changed; throws AggregateException, the failure first, when moving one back
    /// fails too.
    TaskItem SaveEdit(TaskItem started, TaskDraft edited);

    /// Gives the task a rank that shows it after previousTaskId and before nextTaskId; a null ID
    /// leaves that side open, so Move(taskId, null, null) changes nothing. The new rank is never one another
    /// task holds: with a previous task, it sorts after that task's rank and before the first rank after it in
    /// All that differs from it, which is never past the next task; with only a next task, before that task's
    /// rank and after the last rank before it in All that differs from it. Both skip the moved task.
    /// Changes only the task's own file and sets Updated. Changes nothing when the task already shows
    /// there: between the two given tasks, or, with a side open, next to the given task, with only tasks
    /// sharing its rank between them. Returns the task as saved.
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

    /// Search(text), narrowed to the tasks that have every one of tags, normalized, each anywhere in the
    /// task's list; a repeated tag counts once. Throws ArgumentException when tags is empty or holds a tag
    /// that's empty once normalized; Search(text) filters by no tag.
    IReadOnlyList<TaskItem> Search(string text, IReadOnlyCollection<string> tags);
}

public sealed record TaskItem(string Id, string Title, string Description, string Rank,
    IReadOnlyList<string> Tags, DateTimeOffset Created, DateTimeOffset Updated, DateTimeOffset? Resolved)
{
    /// The tag that groups the task: Tags' first entry, or null when it has none.
    public string? GroupTag { get; }
}

/// The one rule that turns text into a tag.
public static class TagName
{
    /// The most tags a new tag list may hold, counted after normalizing and dropping repeats.
    public const int MaxPerTask = 15;

    /// text trimmed, lowercase by the invariant culture's rules, with each run of whitespace,
    /// line breaks included, replaced by one hyphen. Empty when text holds only whitespace.
    public static string Normalize(string text);
}

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
| [`ISharedTaskSettings`](../../src/Shared/Tasks/ISharedTaskSettings.cs) | Typed values, one setter each, and a settings-change notice |
| [`ITaskSynchronization`](../../src/Shared/Tasks/Synchronization/ITaskSynchronization.cs) | The file rules, settings initialization, and automatic deletion; the selected folder, skipped files, local-save notice, and exclusive updates come from the [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload) |
| [`IRepositoryFileRules`](../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs) | Canonical managed paths and pure three-version task/settings byte merges |
| [`ISkippedRepositoryFile`](../../src/Shared/Platform/ISkippedRepositoryFile.cs), in the shared platform | Immutable relative path, reason, and whether updating the app is needed |

The [factory contract](../../src/Shared/Tasks/TaskStore.cs) exposes these capabilities through `ITaskStore`.
Existing callers can still use the result as `ITasks`; Configure receives only `Settings`, and
Sync receives only `Synchronization`. No caller casts a concrete implementation or creates another store.

With the [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), the user decided these changes:

- `TaskStore.Create` takes the repository's store side, `IRepositoryAccess repository`, as its new last argument, and adds the store as one of its parts.
- `ITasks.UseRepositoryFolder` goes: the composition root selects through the repository instead.
- `ITaskSynchronization` keeps `Files`, `EnsureSettings`, and `DeleteExpiredTasks`. `RepositoryFolder`, `SkippedFiles`, `LocalSaved`, and `UpdateRepository` move to the repository's Sync side, `ISyncedRepository`, with the same rules.
- `ISkippedRepositoryFile` and `NoRepositoryFolderException` move to `Memoria.ToDo.Shared.Platform` with the same members.
- No task behavior changes; tests change only how they select a folder.

```csharp
public static ITaskStore Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder,
    IRepositoryAccess repository);
```

With [Contacts](contacts.md), the user decided that Tasks' JSON style, its version check, its rule for which read failures skip a file, and its relative-path check move into the platform's [shared file rules](modules.md#stores-share-one-json-style-and-one-set-of-file-rules). Tasks' task, settings, tags, and draft files use them, with no change in behavior and no change to Tasks' public interface.

Row 11 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) adds the folder where an open edit's [images wait](#images-are-at-most-1-mb-and-live-beside-their-task), for the desktop's composition root, which gives it to the web view: [`ITaskStore.EditImagesFolder`](../../src/Shared/Tasks/ITaskStore.cs). It's laid out like the clone, so the link an image gets names the image under it. Proposed: its shape. This is a decision to confirm.
That folder is under the device folder, so `TaskStore.Create` throws `ArgumentException` when the device folder isn't fully qualified, before any side effect: a relative path would follow the working directory. This is a decision to confirm.`IRepositoryFileRules` is the only public home of the managed directory/file names and their classification.
Its implementation reuses `RepositoryLayout` and the existing codecs. A Git-relative path follows the platform's
[relative-path rule](modules.md#stores-share-one-json-style-and-one-set-of-file-rules). Task, settings, and tag-color merges do no I/O and reject unreadable inputs rather than
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

Row 14 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) brought one tag per task, and row 21 replaces it with [an ordered list of tags](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it). The user decided these declaration changes:

- **`TaskItem.GroupTag`** is new, so Task View's grouping, the Local API's `tag` field, and any later caller read the group tag one way.
- **`Search(string text, string tag)` becomes `Search(string text, IReadOnlyCollection<string> tags)`**, so the Local API and the MCP server can ask for several tags at once. Its one-tag callers pass a one-tag list.
- **`Add`, `SaveEdit`, and `WithTag` throw for more than 15 tags**, and no longer for two or more tags or a multi-line tag. `LoadDraft` returns every tag of a kept draft, normalized.

Decided by the session, a decision to confirm: **`TagName.Normalize`** is new, a static method, so the desktop's tag box rewrites typed text by Tasks' rule rather than a copy of it. It's a public interface change, so the user confirms it.

Also proposed for step 4, each a decision to confirm:

- `SaveEdit` compares a Title after trimming, and a Description after storing its line breaks as `\n`, so only a real change counts. It replaces the tags as [A task's tags are an ordered list](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) says, normalizing each one.
- `SaveEdit` of a task that's no longer in `All` throws, so a save never undoes a deletion that arrived while the edit was open. The editor keeps the text, so the user can copy it.
- The user decided that a failed `SaveEdit` undoes its image moves, as `Add` undoes its steps. It still moves the edit's images before it writes the file. When a move or the write fails, it moves every image it moved back to the edit's folder, newest first, and rethrows the failure, so the clone is as it was: a retry moves them all again, and discarding the edit deletes them all. When moving one back fails too, it throws as [Failure modes](#failure-modes) says. `Add` and `SaveEdit` undo through one internal part of Tasks, built on the platform's [rule for steps that must all run](modules.md#steps-that-must-all-run-throw-their-failures-together), so the rule has one home.
- When `Move`'s two given neighbors share a rank, which only [two devices changing tasks offline](#rank-is-a-text-key-that-sorts-between-its-neighbors) can cause, no key sorts between them, and a move changes only its own file. So `Move` refuses, unless the task already shows there, and the Task list shows the move failed. A later design can re-rank a neighbor instead.
- The user decided that every `Move` bounds the new rank by the task really next to it in `All`, so a drop at the end of a tag group can't take the rank of the first task in the next group, and a move between two tasks of one group can't take the rank of another group's task between them. With a previous neighbor, the bound is the first rank after it that differs from its own, past the moved task and the tasks sharing the previous neighbor's rank, so it's never past the given next neighbor. With only a next neighbor, it's the last rank before it that differs from its own, past the moved task. Skipping the tasks that share a neighbor's rank still honors the request, where refusing would fail a drop because of tasks the user may not see.
- Proposed, a decision to confirm: with both neighbors given, the task already "shows there" when it sits anywhere between them in `All`, as before, so a drop that doesn't change its group's order writes nothing, even with another group's task between it and a neighbor. With a side open, it shows there only when it's next to the given neighbor, with only tasks sharing that neighbor's rank between them, as the open-side rule already does.
- `Move` with no neighbors changes nothing, even when other tasks exist, such as hidden by a filter that shows only this task.
- `Search(text, tags)` refuses an empty list or an empty tag instead of treating it as no filter, so a caller's mistake shows; a screen with no tag filter calls `Search(text)`.
- `Delete` stops at the first step that fails and throws its error, with no new exception type. Deleting the file is the step that deletes the task: if it fails, the task and its images stay as they were; once it succeeds, the task is deleted even when removing its images then fails, and those images stay in the clone. A caller tells the two apart by whether the task is still in `All`. Like every call, `Delete` does all its file work while no other call's file work runs, so its last step can't delete an image saved after the file was deleted, such as for a task with the same ID in another repository. Then it raises `Changed`, including when a step after the file's deletion failed. As with every call, `Changed` is raised once other calls can run again, so another call may run before the handlers do; Tasks never holds other calls back while handlers run.
- `Resolve` and `Reopen` raise `Changed` only when they change the task, and `Move` and `SaveEdit` likewise.

### Tag colors are a capability of their own

Row 8 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) adds [tag colors](#each-tags-color-is-kept-in-one-synced-file) the way step 5 added settings: a property of the store, a small interface, and two file rules Sync uses. The composition root passes `TagColors` to the window modules that show tags. The declarations below add to the existing contracts:

```csharp
namespace Memoria.ToDo.Shared.Tasks;

public interface ITaskStore : ITasks
{
    // Settings and Synchronization stay as they are.

    /// <summary>Each tag's color in this store's selected repository.</summary>
    ITagColors TagColors { get; }
}

/// <summary>
/// Each tag's color, kept in tags.json and shared by every device. Safe to call from any thread;
/// uses the task store's lock. Tags match once normalized, by TagName.Normalize. A color's name is kept without interpreting it.
/// </summary>
public interface ITagColors
{
    /// <summary>
    /// The color name kept for tag, or null when there's none: no entry, no selected repository,
    /// or a tags.json that can't be read or has a newer version, which SkippedFiles names.
    /// </summary>
    string? ColorOf(string tag);

    /// <summary>
    /// Keeps colorName for tag, with the current UTC time, creating tags.json when it's missing, then raises
    /// LocalSaved and Changed. The same color as kept writes and raises nothing. Throws, before writing:
    /// ArgumentException for a tag that's empty once normalized, or a colorName that isn't one non-empty line;
    /// NoRepositoryFolderException when no repository is selected; and InvalidDataException when
    /// tags.json can't be read or has a newer version.
    /// </summary>
    void SetColor(string tag, string colorName);

    /// <summary>
    /// Raised on the calling thread, outside the store's lock, after a color is kept here,
    /// on repository selection, and on every reload.
    /// </summary>
    event EventHandler? Changed;
}

public interface IRepositoryFileRules
{
    // The existing members stay as they are.

    /// <summary>The tag colors file's name, relative to the repository root.</summary>
    string TagColorsFileName { get; }

    /// <summary>
    /// Merges each tag's entry by its own Updated time, remote winning ties, matching normalized tags; within one
    /// file, the first entry for a normalized tag wins.
    /// Null means an absent file; an absent side or a missing entry never removes an entry. Three absent
    /// files return null. Throws InvalidDataException for any present file that cannot be read; never alters input arrays.
    /// </summary>
    byte[]? MergeTagColors(byte[]? basis, byte[]? local, byte[]? remote);
}

public enum RepositoryFileKind
{
    Unmanaged,
    Task,
    Image,
    Settings,
    TagColors,
}
```

The other contracts keep their members and cover the new file too: `SkippedFiles` names a `tags.json` that can't be read or has a newer version, and the repository's update reloads tag colors with tasks and settings.

Proposed: `ColorOf` answers null while `tags.json` can't be read, rather than failing, so every badge shows its default color and the sync status names the file. A failure there would stop every screen that shows a tag, and a tag's default color is always a valid color. This is a decision to confirm.

Proposed: `SetColor` checks its arguments before anything else, so a tag that's empty once normalized, or an invalid color name, throws `ArgumentException` even when no repository is selected or `tags.json` can't be read. A caller's mistake then shows the same way whatever the repository's state. This is a decision to confirm.

### Tag order is a capability of its own

Row 13 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) adds [each tag's place](#each-tags-place-orders-task-views-groups-and-androids-tag-chips) as a second capability over `tags.json`, beside tag colors, so each caller gets only the part it uses: the desktop's Task list gets both, and its Task editor only colors. The composition root passes `TagOrder` to each app's Task list. The declarations below add to the existing contracts:

```csharp
namespace Memoria.ToDo.Shared.Tasks;

public interface ITaskStore : ITasks
{
    // The existing members stay as they are.

    /// <summary>Each tag's place in this store's selected repository.</summary>
    ITagOrder TagOrder { get; }
}

/// <summary>
/// Each tag's place in the order both apps show tags in, kept in tags.json beside the tag's color and shared by every
/// device. Safe to call from any thread; uses the task store's lock. Tags match once normalized, by TagName.Normalize.
/// </summary>
public interface ITagOrder
{
    /// <summary>
    /// tags, normalized with each repeat dropped, in the user's order: the tags with no place first, in ordinal order,
    /// then the placed tags by place, equal places in ordinal order. Every tag has no
    /// place when no repository is selected, or tags.json can't be read or has a newer version, which SkippedFiles names.
    /// Throws ArgumentException for a tag that's empty once normalized.
    /// </summary>
    IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags);

    /// <summary>
    /// Gives tag a place after previousTag and before nextTag, as Sort shows them; null leaves that side open, so
    /// Move(tag, null, null) changes nothing. When a neighbor has no place, or both share one, first gives the tags in
    /// use, tag, and its neighbors places in the order Sort shows them. Writes tags.json with the current UTC time and
    /// the normalized tag, then raises LocalSaved and Changed. Writes and raises nothing when tag already shows
    /// there. Throws, before writing: ArgumentException for a tag that's empty once normalized, a neighbor that is tag itself, or a
    /// previousTag that doesn't show before nextTag; NoRepositoryFolderException when no repository is selected; and
    /// InvalidDataException when tags.json can't be read or has a newer version.
    /// </summary>
    void Move(string tag, string? previousTag, string? nextTag);

    /// <summary>
    /// Raised on the calling thread, outside the store's lock, after a move here, on repository selection,
    /// and on every reload.
    /// </summary>
    event EventHandler? Changed;
}
```

The file rules keep their `TagColorsFileName` and `MergeTagColors` names; their contracts now cover version 2 of `tags.json`, which holds each tag's color and place. The [file rules source](../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs) holds the merge rules.

`RepositoryFileKind.TagColors` still classifies `tags.json`, so Sync's [table of merges by file kind](../../src/Shared/Sync/Git/FileMerges.cs) doesn't change. A reload raises both `TagColors.Changed` and `TagOrder.Changed`; a color pick raises only the first, and a move only the second.

Proposed: the file rules keep their `TagColors` names, though `tags.json` now holds places too. Renaming them would change Sync's code and two published names for no change in behavior. This is a decision to confirm.

### Direct task changes are a capability of their own

The user decided, for the [local API](local-api-contract.md#task-calls-never-touch-your-draft-or-open-edit): a fourth capability adds and changes tasks for a caller with no editor. `Add` deletes the kept draft, `SaveImage` keeps images with the open edit or the draft, and `SaveEdit` moves the open edit's images, so a program using them would change what you're typing. Direct changes never touch the draft or an open edit. A task's tags here are the same [ordered list](#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) as in the editor.

It has two callers, the local API and the desktop's [MCP server](desktop/mcp-server.md#adding-and-editing-never-touch-the-users-open-work). The user decided that the MCP server uses all three changes: `Add` for its new tasks, `Update` for `edit_task`, and `AddImage` for its images. So changing a task without an editor has one home, and `ITasks` has no members of its own for that. `edit_task` passes the `Updated` of the task it read just before, so the assistant never sends a time.

An open edit still [merges](#two-versions-of-a-task-merge-field-by-field) with a direct change, as it does with a sync: its save replaces only the fields the edit changed. So when an edit saves a changed description, an image's link that `AddImage` appended while the edit was open is dropped; the image itself stays in the task's folder until the task is deleted, as every image does. A link appended after the save, or during an edit that doesn't change the description, stays.

The declarations below add to the existing contracts:

```csharp
namespace Memoria.ToDo.Shared.Tasks;

public interface ITaskStore : ITasks
{
    // The existing members stay as they are.

    /// <summary>Adds and changes tasks without touching the kept draft or any open edit.</summary>
    IDirectTaskChanges Direct { get; }
}

/// <summary>
/// Task changes for a caller with no editor, such as the local API and the MCP server. Never reads or changes the kept draft or an open
/// edit's waiting images. Safe to call from any thread; uses the task store's lock. Each change writes the task's file,
/// then raises Changed on the calling thread and reports the local save, as ITasks' changes do.
/// </summary>
public interface IDirectTaskChanges
{
    /// <summary>
    /// Adds an open task with a new ID, at the top, with Created and Updated set to now, and tags in the given order,
    /// normalized, each repeat dropped; an empty list means none. Returns it.
    /// Throws, before writing: ArgumentException for an empty or multi-line title, a tag that's empty once normalized,
    /// or more than 15 tags; NoRepositoryFolderException when no folder is set.
    /// </summary>
    TaskItem Add(string title, string description, IReadOnlyList<string> tags);

    /// <summary>
    /// Replaces each field that isn't null; a tags list, normalized with each repeat dropped, replaces the whole list, in
    /// its order, and an empty list removes every tag. Sets Updated, unless nothing changed, which writes nothing; a list
    /// that equals the current one in order, once normalized, is no change. Returns the task as saved. Throws, before
    /// writing: StaleTaskException when the task's Updated isn't baseUpdated; TaskNotFoundException; ArgumentException for
    /// an empty or multi-line title, a tag that's empty once normalized, or more than 15 tags; NoRepositoryFolderException.
    /// </summary>
    TaskItem Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, IReadOnlyList<string>? tags);

    /// <summary>
    /// Shrinks the image to at most 1 MB, writes it into the task's image folder in the clone, and appends its Markdown on
    /// a new line at the end of the description, or as the whole description when it's empty, in one save that sets
    /// Updated. Deletes the image again when the save
    /// fails. Returns the task as saved. Throws, before writing: TaskNotFoundException; InvalidDataException when it
    /// isn't an image; ImageTooLargeException; NoRepositoryFolderException.
    /// </summary>
    TaskItem AddImage(string taskId, byte[] image, string altText);
}

/// <summary>The task changed since the caller read it. Current is the task as it is now.</summary>
public sealed class StaleTaskException : InvalidOperationException
{
    public StaleTaskException(TaskItem current);

    public TaskItem Current { get; }
}
```

Moving, resolving, reopening, and deleting use `ITasks` as they are, since none touches the draft.

The user decided: `Update` compares `Updated` exactly, to the millisecond it's stored to, so a caller that read a task before any change passes. No update without a time to compare is offered, since a program would undo a change it never saw.

**Every change to a task moves `Updated` strictly forward.** An editor save, a move, a resolve, a reopen, a direct update, and an added image each set it to now, cut to the millisecond, or to one millisecond after its last value when now isn't later. So two changes in the same millisecond still leave different times, and an `Update` that read the first fails with `StaleTaskException`.

Decided by the implementation, to confirm: `AddImage` on an empty description makes the image's Markdown the whole description, with no blank first line.

For row 21, the user decided that `Add` and `Update` take a list of tags instead of one tag, so the Local API and the MCP server can set several in order. With a list there's no empty-tag shortcut: an empty list means no tags, and a tag that's empty once normalized is rejected. The Local API keeps its one-tag field and turns it into a list, as its [contract](local-api-contract.md#a-tasks-tags-travel-as-a-list-beside-the-older-one-tag-field) says.

### A caller can require readable settings files

The user decided that Tasks, which owns `settings.json` and `tags.json`, says when either was skipped, with the platform's [`SkippedFileException`](modules.md#a-call-that-needs-a-skipped-file-throws-one-type). The [local API's settings calls](local-api-contract.md#four-services-cover-version-1) need it: tag colors and places fall back to defaults while `tags.json` can't be read, so a reply would hide the problem, and a write to one file must not happen while the other is skipped. The existing reads and writes keep their contracts.

Proposed, a decision to confirm: this declaration.

```csharp
namespace Memoria.ToDo.Shared.Tasks;

public interface ITaskStore : ITasks
{
    // The existing members stay as they are.

    /// <summary>
    /// Throws SkippedFileException when the latest load skipped settings.json or tags.json, naming settings.json
    /// when both were. Does nothing otherwise, including when either file is missing or no repository is selected.
    /// Writes nothing and raises nothing.
    /// </summary>
    void RequireReadableSettings();
}
```

The check and a later call each take the lock on their own, so a sync that skips a file between them makes the later call fail as its own contract says, or a read answer from defaults. Proposed: this is an accepted risk, a decision to confirm.

## Failure modes

- **No clone folder is set**: adding a task fails before any file is written, and the screen that asked keeps the text and points to where the folder is set.
- **A task file can't be read**, such as broken JSON from the remote: Tasks skips the task and never deletes its file, and the sync status names it.
- **A task file has a newer `version`**: Tasks skips it and leaves it unchanged, and the sync status asks the user to update the app.
- **A task file can't be written**, such as on a full disk: the screen that asked shows the error and keeps the user's text, and the draft stays.
- **A direct update names a task that changed since the caller read it**: it throws `StaleTaskException` and writes nothing, as [direct task changes](#direct-task-changes-are-a-capability-of-their-own) say.
- **An image can't be read, or can't be shrunk to 1 MB**: nothing is saved, and the editor, the local API, or the assistant's tool says why.
- **A direct image's task can't be saved**: Tasks deletes the image it wrote, so nothing changes, and the caller gets the save's error, as [direct task changes](#direct-task-changes-are-a-capability-of-their-own) say.
- **The kept draft can't be read**: loading it fails with `InvalidDataException` (a read failure keeps its cause inside), and the file stays as it is until the next keep or discard. The Add window then opens empty and says so.
- **Adding a task fails after its file is written**, while moving images or deleting the draft: Tasks undoes what it did, moving the images back and deleting the new task file, and rethrows the original error, so the draft stays whole, nothing is left in the repository, and a retry works. The draft's images folder is deleted before its file, so a failed draft delete is undone the same way. If the undo itself fails, Tasks throws an `AggregateException` with both errors. Adding never overwrites: if the task file's path is already occupied, even by a file that was skipped on load, it throws `ArgumentException`.
- **The top task's rank isn't a valid key**: adding fails with `InvalidDataException` rather than guess an order.
- **Saving an edit fails while moving its images or writing the task file**: Tasks moves every image it moved back to the edit's folder and rethrows the original error, so the task file and the task's images folder are as they were, no notice is raised, and no local save is reported. A retry works, and discarding the edit deletes every one of its images. If moving one back fails too, that image stays in the task's folder, and Tasks throws an `AggregateException` with the original error first, then each failure of the undo, in the order they happened.
- **`tags.json` can't be read, or has a newer `version`**: Tasks leaves it unchanged, and the sync status names it, or asks the user to update the app. Every tag shows its default color and has no place, so tags show in alphabetical order, and picking a color or moving a tag fails without writing, so the screen that asked says why. Tasks still load.
- **A notice handler or the local save report fails after a change**: the change stays saved. Tasks still raises every notice the change calls for, in this order: `Changed`, then the settings', the tag colors', and the tag order's, and then reports the local save. Each runs even when an earlier one failed, so Sync always learns of the save. Each event counts as one step, as in [Contacts](contacts.md#failure-modes): when one of its handlers throws, that event's later handlers don't run. Tasks then throws by the platform's [rule for steps that must all run](modules.md#steps-that-must-all-run-throw-their-failures-together): one failure as it is, several as an `AggregateException`, in the order they happened.
- **Deleting a task fails after its file is deleted**, while deleting its images, by hand or by automatic deletion: the task is gone, so Tasks raises `Changed` and reports the local save as above, then throws, the deletion's own failure first. Proposed, matching Contacts' [partial renumber](contacts.md#failure-modes): this is a decision to confirm. Every other change either writes its one file or fails before any notice.
- **A notice fails after a reload**: the [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload) runs each part's notices as one step, so Tasks itself runs all four of its notices, each even when an earlier one failed, and reports no local save. The repository keeps what Tasks throws as one failure, so several of Tasks' failures arrive as one `AggregateException` inside the repository's. Proposed: this is a decision to confirm.
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
- How a move picks its rank:
  - **Bounded only by the given neighbors**: the new rank could equal that of a task in another group or hidden by a filter, and then random parts of their IDs would set their order.
  - **Bounding only an open side, and narrowing the promise to open sides**: a move between two tasks of one group could still take the rank of another group's task between them, and the resolved tasks, which show in one list, could show the two in an order the user didn't choose.
  - **Refusing when tasks next to the given neighbor share its rank**: a drop would fail because of tasks the user may not see, though a rank past them honors the request.
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
  - **Suggestions that only start with the typed text**: the user asked that a tag containing it anywhere match.
  - **A second rule beside the suggestions, for the desktop's Search**: two near-identical rules, one with no caller.
- Images for an open edit:
  - **The editor says whether it holds a draft or a task, and calls a separate image part for edits**: an extra setting on the editor, and two image parts that shrink images the same way.
  - **Images added during an edit go straight into the clone**: a sync during the edit could push images that a discard then deletes, and git history would keep them.
- How a caller with no editor adds a task and an image:
  - **`Add` with a draft deletes the kept draft only when it's the draft being added, matched by task ID**: the user chose an operation of its own, so `Add` with a draft keeps its one rule.
  - **An assistant's image waits with the task's open edit through `SaveImage`**: the user's discard of the edit would delete it, and the user's Save would publish it unseen.
  - **`ITasks` members of their own for the MCP server, `Add` without a draft and an `AddImage` that only writes the image and returns its Markdown**: a second home for adding without a draft, beside direct task changes. The user unified them.
- How the desktop's web view finds an open edit's waiting images:
  - **A call that reads a waiting image's bytes for a link**: a bigger interface, and Tasks would serve the web view's requests one by one, for what one folder path gives.
  - **The composition root joins the draft folder and `edits` itself**: the folder's layout would then live in two places that must change together.
- Where a tag's color is kept:
  - **In each task file, beside each tag**: one tag could carry different colors on different tasks, a recolor rewrites every task with the tag, and task files would need version 2, which older apps skip entirely.
  - **On each device only**, outside the clone: colors wouldn't travel, so a tag colored on the PC would show its default color on the phone.
  - **A new field in `settings.json`**: it needs a new version, which makes an older app's settings reads and writes fail; without one, an older app drops the colors when it writes a setting.
- How Sync merges `tags.json`:
  - **One merge method in the file rules that takes any managed path**: it would replace two published methods to add one file.
- Where each tag's place is kept:
  - **A separate synced file, `tag-order.json`**: older apps would keep their colors, but it adds a file format, a merge rule, and a file kind in Sync. The user chose one file for everything set on a tag.
  - **On each device only**: another PC, the phone, or a reinstall would show the default order, and many drags would restore it.
- How the stored order records each tag's place:
  - **One ordered list of tags, with one time**: two devices that move different tags conflict, and the later move undoes the other.
  - **One time per entry, shared by its color and place**: a color picked on one device and a move made on another would conflict, and one would be lost.
- Where a tag with no place shows:
  - **After every placed tag, in alphabetical order**: the user chose the top, as for a new task.
- How the tag order is published:
  - **Sorting and moving on `ITagColors`**: a caller that needs only colors, such as the desktop's Task editor, would get the order too.
- Where a task's group tag is kept:
  - **A field of its own, such as `groupTag`, beside the list**: two fields would have to change together, a merge could pair one device's group tag with another's list, and an app not yet updated would keep writing the list without it.
  - **The tag with the earliest [place](#each-tags-place-orders-task-views-groups-and-androids-tag-chips) in the tag order**: the user asked that the first tag in the task's own list group it, and a group move would then regroup tasks the user never touched.
- Where the tag-list rules live:
  - **Only in the desktop's tag control**: the Local API, the MCP server, and the Android app would each need their own check, so two same tags could still reach a file.
- How the API and the MCP server set several tags:
  - **One text field with the tags separated by commas**: a tag could never hold a comma, and every caller would parse it.
- Which version a task file with several tags has:
  - **Version 2 for every task file**: an app not yet updated [skips every version 2 file](#failure-modes), so a task saved by the new app would disappear from an old device until it's updated, and old and new files would mix for a long time.
  - **Version 2 only for a task with two or more tags**: those tasks would vanish from an old app, and a task's version would depend on its content.
- How many tags a task may have:
  - **No limit**: the user chose at most 15.
  - **At most 10**: the user chose 15.
- What happens to tags that differ only in case:
  - **Rejecting a list with two of them, with `ArgumentException`**: the user chose to normalize every tag and drop repeats, so no caller ever meets that error.
  - **Tags keep the user's spelling, matched ignoring case, and a new tag takes the spelling of a matching tag in use**: two spellings of one tag could still reach files and `tags.json`, so every comparison, merge, and color lookup had to ignore case. The user chose one lowercase spelling.
  - **Removing spaces from a tag instead of turning them into hyphens**: `my work` would become `mywork`. The user chose hyphens.
- What happens to `tags.json` entries whose tags normalize to the same tag:
  - **The file is unreadable**: every tag would lose its color and place until someone fixed the file by hand, for a file this change itself makes common.
  - **The entries merge, each of color and place by its later time**: right more often, but a second merge rule inside one file, for a case that happens once per clone.
- Whether normalizing on read rewrites the file:
  - **Rewriting every task file and `tags.json` on first read**: every device would rewrite and push every task at once, and two devices doing it would conflict on every file.
- What a filter of several tags matches:
  - **Tasks that have any given tag**: "work and urgent" would need the caller to intersect results itself, while "work or home" takes one call per tag.
  - **Only a task's group tag**: the user asked that every tag count when the API and the MCP server select tasks.
- How a caller reads the group tag:
  - **Each caller reads `Tags.FirstOrDefault()`**: the rule would be written in every caller, which the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) forbids at the second use.
  - **A static helper in Tasks**: one home too, but harder to find than a property of the task.
- How the desktop's tag box applies the tag rule:
  - **Its own copy of the rule**: the rule would live in two places that must change together.
  - **A member of `ITasks`**: an instance call, and a fake in every test, for a pure rule with no state.
- How two devices' tag lists merge:
  - **Each tag merges on its own: adds and removes from both devices, then the later device's order**: neither change is lost, but the merge grows a set rule with its own tests, and the order can be one neither device chose.
- Which waiting images saving an edit moves:
  - **Move waiting images only on a Description change**: needed only for tag saves during an edit, which the atomic save removed.

- What a failed save of an edit undoes:
  - **Nothing, as before**: a retry worked, but discarding the edit afterwards left its moved images in the task's folder with no link, where they sync and git keeps them for good.
  - **Write the file first, then move the images, undoing by writing the old file back**: the undo takes a second write on the disk that just failed, and when it fails, the saved description links to images that aren't there.

## Open questions

None.