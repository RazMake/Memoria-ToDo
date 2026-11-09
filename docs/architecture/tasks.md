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
| `settings.json` | The settings every device shares, such as the deletion delay and the desktop app's two global shortcuts, whose owner [Configure](desktop/configure.md#which-module-owns-the-shortcuts-in-settingsjson) leaves open |

Deleting a task removes its file and its image folder.

### Tasks reads the clone's folder, which can change while the app runs

Tasks works on one clone at a time. The app gives Tasks the clone's folder when it starts, and again each time the user picks another; Tasks then reads that folder's tasks. With no folder, the list is empty, and adding a task fails with an error that says no repository folder is chosen, so each app can tell the user where to choose one.

Proposed, each a decision to confirm:

- Tasks reads the folder in the background, so no screen waits for it. Until the read ends, the list is empty.
- The last folder given wins: a read of an earlier folder that ends later is dropped.
- Adding a task while the folder is being read waits for the read, so the new task's rank goes above every task.
- When the folder can't be read, the list stays empty, and adding a task fails with that error until another folder is given.

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

Each file also follows these rules:

- Times are written to the millisecond, so two edits rarely carry the same time.
- A newer app reads every older `version`. An app that finds a newer `version` skips the file and leaves it unchanged.
- Tasks writes each file as UTF-8 JSON with two-space indents, its fields in the table's order, and `\n` line breaks, so one change shows as a few changed lines in git.
- A title is one line: Tasks trims spaces at both ends, and rejects an empty title or one with a line break. A description keeps the user's text, with each line break stored as `\n`.
- A task's tags keep the order they were added in.

### A time-ordered ID names each task, its file, and links to it

A task's ID is a universally unique identifier (UUID), version 7, which starts with the time it was made ([RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-7)). So task files list in the order they were added, and two devices never make the same ID, even offline. .NET makes it with [`Guid.CreateVersion7()`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7), so it needs no new dependency.

- The ID is written in lowercase with hyphens: 36 characters.
- A link to another task is `[link text](task:<id>)`. Its text is the target's title when the link is inserted, as a reader sees it, which [Rendering's styled spans](rendering.md#titles-and-the-editor-get-styled-spans-not-html) give, so the title's Markdown can't break the link.

Proposed: [Rendering's browser check](rendering.md#only-web-links-open-in-the-browser) rejects `task:` addresses, and the app opens the task instead. This is a decision to confirm.

### Rank is a text key that sorts between its neighbors

Each task's rank is a text key that sorts between the keys of the tasks before and after it, a fractional index ([Figma](https://www.figma.com/blog/realtime-editing-of-ordered-sequences/)). Moving a task changes only its own file, so two offline devices that move different tasks never conflict. When two keys are equal, the task IDs set the order. A new task gets a key before all others, so it goes to the top, as the [Task editor](desktop/task-editor.md#a-new-task-goes-to-the-top) decides.

Keys use the 62 digits and letters, compare character by character, and are never renumbered. Adding at the top keeps keys short; only repeated moves into one gap lengthen a key, by about one character per six moves.

A key starts with a letter that says how long its whole-number part is, as the [fractional-indexing](https://github.com/rocicorp/fractional-indexing) library's keys do. The first task gets `a0`, and each new task at the top lowers the whole number by one, so the first 63 tasks get 2-character keys and the next 3,844 get 3.

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

Proposed, each a decision to confirm:

- The later edit is the one with the later `updated` time, from each device's own clock. A device with a wrong clock can win or lose wrongly; this is a known limit.
- When both `updated` times are equal, the version already on the remote wins. The merged `updated` is the later of the two.
- When both devices changed the resolved time, the later edit wins.
- A task that comes back gets its images back too.
- Saving an edit merges the same way, against the version the edit started from, so it never undoes a change that arrived while the edit was open.

### Resolved tasks are deleted after a delay every device shares

The deletion delay is one setting in `settings.json`, the same on every device, as [purpose and scope](purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. A resolved task is deleted once its resolution is older than the delay, even if it was edited since.

Proposed, each a decision to confirm:

- During each sync, any device deletes the tasks resolved longer ago than the delay. Two devices deleting the same task merge cleanly, and an edit still wins, as the table above says.
- The delay is 1 to 365 whole days.
- `settings.json` merges one setting at a time, and the later change wins. Each setting keeps its value and the UTC time it changed, and the file has its own `version`.

### Images are at most 1 MB and live beside their task

Images are PNG for screenshots and images with transparency, and JPEG for photos. Each platform's own image tools shrink them, so the apps need no imaging library: Windows Presentation Foundation (WPF) imaging ([BitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapencoder)) and Android's [Bitmap](https://developer.android.com/reference/android/graphics/Bitmap.CompressFormat). An image over 1 MB is shrunk in these steps:

1. Its longest side is scaled down to at most 1920 pixels.
2. A PNG still over 1 MB is saved as JPEG.
3. Proposed: a JPEG still over 1 MB is saved at a lower quality, then scaled down further, until it fits. This is a decision to confirm.

Each image is named by its own UUID version 7, so two offline devices never pick the same name. A description links to it from the clone's root, such as `![alt](images/<task-id>/<image-id>.png)`, and [Rendering loads it from the clone](rendering.md#the-web-view-runs-no-scripts-and-loads-only-images-from-the-clone).

Each app's image shrinking only reads an image's size and format, scales it, and saves it as PNG, or as JPEG at a given quality. Tasks runs the steps above, so both apps shrink images the same way.

Proposed, each a decision to confirm:

- A JPEG file stays JPEG; an image from the clipboard or in any other format starts as PNG.
- An image is deleted only with its task, even when no link to it is left, because a merge can drop the only link.
- An image added to a [draft](#a-draft-waits-on-this-device-until-its-saved) or an open edit waits in the shared platform's [device store](platform.md#files-that-stay-on-this-device-live-in-the-device-store), outside the clone, and moves into the task's folder on save. Discarding deletes it.
- An image's alt text is its file's name without the extension, or `image` for an image from the clipboard, escaped by the [shared platform's](platform.md#text-rules-two-modules-follow-live-here-once) rule so it shows as written.
- 1 MB is 1,048,576 bytes. A PNG or JPEG file of at most 1 MB is saved unchanged. An image in any other format is first saved as PNG at its own size, and goes through the steps above when that's over 1 MB.
- JPEG is saved at quality 85 first. Step 3 lowers the quality by 10 at a time, down to 55, and then scales the longest side down by a quarter at a time. An image still over 1 MB at 256 pixels isn't saved.
- PNG images end in `.png`, and JPEG images in `.jpg`.

Git history keeps every task and image ever saved, so the repository only grows. This is an [accepted risk](overview.md#risks-the-later-rows-must-resolve); the 1 MB cap limits each image.

### A draft waits on this device until it's saved

A draft is a task being written that isn't saved yet: its task ID, Title, Description, and Tags. Each device keeps at most one, in the shared platform's [device store](platform.md#files-that-stay-on-this-device-live-in-the-device-store), so it never syncs. Each app's Task editor decides when the draft is kept and discarded, as the desktop [Task editor](desktop/task-editor.md#closing-without-saving-keeps-a-draft-on-this-device) and the Android [Task editor](android/task-editor.md#back-keeps-a-draft-on-this-phone) say.

Tasks owns the draft, so both apps keep it in one format. Tasks writes the draft as one JSON file in its folder of the device store and keeps the draft's images beside it. Adding the draft as a task moves its images into the task's folder.

Proposed, each a decision to confirm:

- A draft gets its task ID, a UUID version 7, when it starts, so its image links are final and don't change on save.
- A draft with no Title, Description, or Tags, or only spaces and line breaks in them, is no draft: keeping one deletes the kept draft and its images.
- Keeping a draft replaces the kept one, and deletes images saved for a draft with another task ID, so only one draft's images stay.
- Adding the task deletes the draft last, after the task's file and images are in the clone, so a failed save keeps the draft.
- A kept draft that can't be read stays as it is until a draft is next kept or discarded, which replaces or deletes it.

### Tags and search ignore case

Proposed, each a decision to confirm:

- A tag is one trimmed, non-empty line. Tags that differ only in case are the same tag, and a new tag takes the spelling of a matching tag in use.
- While the user types a tag, Tasks suggests each tag in use that, from the start of any of its words, starts with the typed text, ignoring case, as the [snippet picker](editing.md#typing-a-slash-opens-the-snippet-picker) matches, with the [shared platform's](platform.md#text-rules-two-modules-follow-live-here-once) rule. Empty typed text suggests nothing. The task's own tags are left out, and the most-used come first, with ties in alphabetical order.
- Search matches a task when every word typed appears in its title, description, or tags.
- The tag filter shows the tasks that have the tag.

## Public interface

Each app's modules and Sync use these parts:

- **The task list**: every task, open and resolved, in rank order, with a notice when any task changes here or through sync.
- **The repository folder**: give Tasks the clone's folder, or none, as [above](#tasks-reads-the-clones-folder-which-can-change-while-the-app-runs).
- **Task operations**: add a task at the top, save an edit, move, resolve, reopen, and delete. Each writes the task's file at once.
- **Tags and search**: the tags in use with how many tasks use each, the tag that typed text makes, the tags to suggest for typed text, and the tasks matching a search and a tag.
- **Task links**: a task's link, and the task ID in a link's address, if it's a task link.
- **Drafts**: load, keep, and discard this device's draft, and save an image for it and get the image's Markdown.
- **Images**: save an image for a task being edited and get its Markdown, and keep or discard the edit's images.
- **Shared settings**: read and change the deletion delay, within its range.
- **Merging, for Sync**: merge a task's three versions, or `settings.json`'s; list the tasks due for deletion; reload the files a sync changed.

The same three versions always merge to the same result, on every device. Each app implements Tasks' interface for image shrinking, and gives Tasks the [shared platform's](platform.md) file system, device store, and clock, as [How the apps fit together](overview.md#two-apps-share-one-c-library) says.

## Failure modes

- **A task file can't be read**, such as broken JSON from the remote: Tasks skips the task and never deletes its file, and the sync status names it.
- **A task file has a newer `version`**: Tasks skips it and leaves it unchanged, and the sync status asks the user to update the app.
- **A task file can't be written**, such as on a full disk: the screen that asked shows the error and keeps the user's text.
- **An image can't be read, or can't be shrunk to 1 MB**: nothing is saved, and the editor says why.
- **No repository folder is chosen**: adding a task fails before any file is written, as [above](#tasks-reads-the-clones-folder-which-can-change-while-the-app-runs), and the screen that asked keeps the user's text.
- **The kept draft can't be read**: loading it fails, and the draft's file stays as it is.

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
- Automatic deletion:
  - **Delete only when both the resolution and the last edit are older than the delay**: offered, and not chosen.
- Image format:
  - **Always JPEG**: screenshot text blurs, and transparency is lost.
  - **WebP everywhere**: WPF has no WebP encoder ([BitmapEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapencoder)), so Windows would need a new imaging library such as SkiaSharp.
- Who owns a draft's format:
  - **Each app's Task editor writes its own draft into the device store**: each app would write the draft's format, which the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) forbids once Android has drafts.
  - **A shared Drafts module**: one more project, and a draft's text and its images would live in two modules.
