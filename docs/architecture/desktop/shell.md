# Shell: how the desktop app starts, opens its windows, and reports trouble

Status: Decided

Shell is the desktop app's tray icon and everything around it: the global shortcut, starting with Windows, one running instance, updates, the theme, and sync status. It opens the [Task editor](task-editor.md), the [Task list](task-list.md), and [Configure](configure.md); the [desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists what it needs from later designs.

## Behavior

### A global shortcut and the tray menu open the app's three functions

A global shortcut opens Add from any app: **Win+Alt+T** by default, changed in [Configure](configure.md). The tray icon's menu offers Add new Task, View Tasks, and Configure, for when the mouse is closer than the keyboard.

- **The tray icon uses [H.NotifyIcon.Wpf](https://www.nuget.org/packages/H.NotifyIcon.Wpf/2.4.1) 2.4.1.** Its menu is a Windows Presentation Foundation (WPF) menu, so it follows the app's theme.
- **Shell registers the shortcut itself**, with the Windows [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) function. Proposed: no shortcut library. This is a decision to confirm.
- **A taken shortcut is reported, not hidden.** Registration fails when another app holds the shortcut; Shell tells Configure, which says so and lets the user pick another. Windows reserves shortcuts with the Windows key for itself ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)), so a Windows update could take Win+Alt+T later; the same path handles that.

Proposed, each a decision to confirm:

- The menu also has Exit, and either mouse button opens it.
- Until the device is configured, the first start, Add, and View Tasks open Configure, since there are no tasks to show yet.

### The app starts with Windows and runs once per user

- **Starting with Windows.** Proposed: Shell starts the app at sign-in through the current user's `Run` registry key ([Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)). It's on by default and switched in Configure. This is a decision to confirm.
- **One instance.** Proposed: starting the app while it runs shows View Tasks in the running instance. This is a decision to confirm.
- **Add is ready before it's needed.** Shell creates the hidden Add window at start, so the [Task editor](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) shows it at once.
- **The theme.** Proposed: the app follows the Windows light or dark setting, which WPF supports since .NET 9 ([WPF theme mode](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)). This is a decision to confirm.

### Sync stays quiet unless something is wrong

Shell shows sync status in the tray and never interrupts while sync works. The table lists what the user sees in each situation:

| Situation | Tray icon | Notification |
| --- | --- | --- |
| Sync works | The plain icon. The tooltip shows the last successful sync time. | None |
| No network | Proposed: an offline badge, since working offline is normal. This is a decision to confirm. | None |
| Sync fails | A warning badge. The tooltip shows the error and the last successful sync time. | One Windows notification for each new kind of error |

[Configure](configure.md#configure-shows-how-sync-is-doing) shows the same details. Proposed, each a decision to confirm:

- A kind of error notifies again only after a sync succeeds.
- Clicking the notification opens Configure.
- Shell reads sync status from Sync directly, which adds Sync to the modules Shell may use in the [module map](../modules.md#module-map).

### Velopack installs the app and keeps it up to date

The app installs with [Velopack](https://www.nuget.org/packages/Velopack/1.2.0) 1.2.0: a per-user setup.exe that installs under `%LocalAppData%` without administrator rights ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). The app checks this repository's GitHub Releases for updates, downloads them in the background, and applies them on the next start ([Velopack integration](https://docs.velopack.io/integrating/overview)). The repository is public, so the check needs no token.

Publishing needs a release step that puts each build on GitHub Releases; the Technology stack row of the [design backlog](../README.md#design-backlog) adds it.

Proposed, each a decision to confirm:

- The app checks for updates at start and once a day. A failed check or download waits for the next check, with no message.
- The installer isn't code-signed, so Windows may warn when it's first run. Velopack recommends signing, since an unsigned app may be flagged as a virus ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)).
- Uninstalling removes the `Run` key.

## Public interface

None. Shell is the desktop app's entry point, and no module uses it.

## Failure modes

- **The shortcut is taken**: registration fails, and Configure says so and lets the user pick another. The tray menu still works.
- **Sync fails**: a warning badge, one notification per new kind of error, and details in the tooltip and Configure.
- **An update can't be checked, downloaded, or applied**: the app keeps running its current version and tries again at the next check.
- **The app is started a second time**: the new process hands over to the running one, which shows View Tasks, and exits.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Opening Add:
  - **Win+Alt+N**: the OneNote desktop app uses it for Quick Notes ([OneNote shortcuts](https://shortcut-tools.com/en/shortcuts/onenote/)).
  - **Ctrl+Alt combinations**: on European keyboard layouts they equal AltGr, which types characters.
  - **The tray menu only**: slower than a shortcut.
  - **The NHotkey library**: RegisterHotKey takes a few lines.
- Tray icon:
  - **The Windows Forms NotifyIcon**: its menu looks like Windows Forms and ignores dark mode.
- Sync status:
  - **A notification for every failure**: it nags during long offline stretches.
  - **Status only in Configure**: a broken sync would go unnoticed.
- Installing:
  - **Inno Setup**: no automatic updates.
  - **No installer, a zipped folder**: updates by hand.
