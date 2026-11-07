# How the desktop app is built

The desktop app is a Windows Presentation Foundation (WPF) app that lives in the notification area (the tray) and opens Add new Task, View Tasks, and Configure. This page lists its four modules, each with one design file in this folder, and the other designs they rely on. It also says how the app starts and the steps it's built in.

## Four modules make up the desktop app

Each module is a project of its own, and the [module map](../modules.md#module-map) says which modules each may use. The table lists them in the order a user meets them:

| Module | What its design file decides |
| --- | --- |
| [Shell](shell.md) | The tray icon and its menu, the global shortcut, starting with Windows, one running instance, updates, the theme, and how sync status shows |
| [Task editor](task-editor.md) | The Add window: its fields, keys, draft, and tags. View Tasks edits a task with the same editor. |
| [Task list](task-list.md) | View Tasks: the list in rank order, expanding and editing in place, the tag filter, search, resolving, deleting, and the Sync button |
| [Configure](configure.md) | The settings window: this device's settings, the shared deletion delay, and sync status details |

Two shared modules serve the desktop app: [Editing](../editing.md) holds the formatting and snippet commands, and [Rendering](../rendering.md) turns Markdown into HTML and styled text. [How the apps fit together](../overview.md) picks the main libraries, such as AvalonEdit for the editor and WebView2 for rendered descriptions.

## The platform runs the app, and the composition root builds it

The desktop platform, `Memoria.ToDo.Desktop.Platform`, holds the app's startup, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides. The composition root, `Memoria.ToDo.Desktop.App`, builds the app from the modules and hands it to the platform to run.

The platform's public interface has two parts:

- **The app's lifetime**: an interface with two steps, start and stop. Start receives a way to end the app. [Shell](shell.md#public-interface) implements it, so the platform names no module.
- **Run the app**: takes a function that builds the lifetime, and returns the process's exit code. In order, it:
  1. rejects a call from a thread that isn't a single-threaded apartment (STA) thread, which WPF needs ([threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)), before doing anything else;
  2. creates the WPF application, which ends only when told to, not when its last window closes, since a tray app has no main window ([ShutdownMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.shutdownmode));
  3. builds the lifetime and starts it on the UI thread, so the modules' windows are made after the WPF application exists;
  4. when the lifetime ends the app, or Windows ends the session ([SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.sessionending)), stops the lifetime once and returns.

When the app starts, the composition root calls the create part of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface). It builds Shell's lifetime from the results and passes it to Run the app. The windows themselves stay `internal`, as all of a module's code does ([Modules](../modules.md#a-module-is-one-capability-behind-a-small-interface)).

**An error no module handles is shown, then the app ends.** The platform shows the error's message in a message box, then ends the app with a non-zero exit code. This covers errors on the UI thread ([DispatcherUnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception)) and on other threads ([UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception)). Each module still reports its own expected failures, such as a sync error. Catching nothing, WPF's default, was ruled out: the app would vanish from the tray with no message.

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

## Delivery backlog

The desktop app is built in steps, one row each, and each row links to the design sections it builds. Only the desktop shell is scheduled; you choose each next row. Status is **Next**, **Not scheduled**, or **Implemented**, which means every behavior the row lists is built and tested.

Until its row is built, a feature is missing, and the app says so:

- **Each window says which of its features aren't built yet.**
- **Nothing pretends to work.** No step reports a save, a sync, or a setting it didn't make. Nothing shows the device as configured or unconfigured before Sync can tell.

The table lists each step, what it builds, and its status:

| # | Step | Builds | Status |
| --- | --- | --- | --- |
| 1 | Desktop shell: the tray app and its windows, with no task saved | The [startup](#the-platform-runs-the-app-and-the-composition-root-builds-it); Shell's [tray icon and menu](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions), without the shortcut or the redirect to Configure; the [hidden Add window](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title), without its fields; and the create and Show parts of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface) | Next |
| 2 | Shortcut, start with Windows, one instance, and theme | Shell's [shortcut](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions), and its [start with Windows, one instance, and theme](shell.md#the-app-starts-with-windows-and-runs-once-per-user); Configure's [device settings](configure.md#each-setting-has-one-owner) and [shortcut check](configure.md#a-new-shortcut-is-checked-at-once); and a new look at the [tray icon failure](shell.md#failure-modes) | Not scheduled |
| 3 | Adding tasks | The rest of the [Task editor](task-editor.md#behavior), on [Tasks](../tasks.md), [Editing](../editing.md), and [Rendering](../rendering.md) | Not scheduled |
| 4 | Viewing tasks | The [Task list](task-list.md#behavior), except its Sync button | Not scheduled |
| 5 | Sync | [Sync through git](../sync.md); Shell's [sync status](shell.md#sync-stays-quiet-unless-something-is-wrong) and its redirect to Configure until the device is configured; Configure's [sync](configure.md#configure-shows-how-sync-is-doing) and deletion delay; the Task list's [Sync button](task-list.md#view-tasks-syncs-when-it-opens-and-on-its-sync-button) | Not scheduled |
| 6 | Install and updates | Shell's [installer and updates](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), and the [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) | Not scheduled |
