# How the desktop app is built

The desktop app is a Windows Presentation Foundation (WPF) app that lives in the notification area (the tray) and opens Add new Task, View Tasks, and Configure. This page lists its four modules, each with one design file in this folder, and the other designs they rely on.

## Four modules make up the desktop app

Each module is a project of its own, and the [module map](../modules.md#module-map) says which modules each may use. The table lists them in the order a user meets them:

| Module | What its design file decides |
| --- | --- |
| [Shell](shell.md) | The tray icon and its menu, the global shortcut, starting with Windows, one running instance, updates, the theme, and how sync status shows |
| [Task editor](task-editor.md) | The Add window: its fields, keys, draft, and tags. View Tasks edits a task with the same editor. |
| [Task list](task-list.md) | View Tasks: the list in rank order, expanding and editing in place, the tag filter, search, resolving, deleting, and the Sync button |
| [Configure](configure.md) | The settings window: this device's settings, the shared deletion delay, and sync status details |

Two shared modules serve the desktop app: [Editing](../editing.md) holds the formatting and snippet commands, and [Rendering](../rendering.md) turns Markdown into HTML and styled text. [How the apps fit together](../overview.md) picks the main libraries, such as AvalonEdit for the editor and WebView2 for rendered descriptions.

## Later designs answer what these files leave open

The desktop design relies on rules that belong to the shared Tasks and Sync modules and to the technology stack, a later row of the [design backlog](../README.md#design-backlog). The table lists each, the desktop modules that need it, and the design that answers it:

| Needed | Used by | Answered by |
| --- | --- | --- |
| The rank that puts a new task first, and a task's rank after a move | Task editor, Task list | [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) |
| The tags in use, how often each is used, and when two tags are the same | Task editor, Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| How search and the tag filter match tasks | Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| The link format of a reference to another task | Task editor, Task list | [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it) |
| Where an image is saved, its path in the Markdown, its shrinking to 1 MB, and where an unsaved task's images wait | Task editor, Task list | [Tasks](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) |
| The allowed range of the deletion delay | Configure | [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) |
| What happens when another device changes a task that's being edited here | Task list | [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) |
| The repository address, the device's SSH key, and when a device counts as configured | Configure | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| When sync runs | Shell, Task list, Configure | [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) |
| The kinds of sync error, telling "offline" from an error, and the last successful sync time | Shell, Configure | [Sync through git](../sync.md#failure-modes) |
| What uninstalling does with the clone and the device's key | Shell | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| The release step that publishes installers to GitHub Releases, and whether the app carries its own .NET runtime | Shell | Technology stack |
