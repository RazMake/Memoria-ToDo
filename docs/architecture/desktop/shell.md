# Shell: how the desktop app starts, opens its windows, and reports trouble

Status: Decided

Shell is the desktop app's tray icon and everything around it: the global shortcut, starting with Windows, one running instance, updates, the theme, and sync status. It opens the [Task editor](task-editor.md), the [Task list](task-list.md), and [Configure](configure.md); the [desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists what it needs from later designs.

## Behavior

### A global shortcut and the tray menu open the app's three functions

A global shortcut opens Add from any app: **Win+Alt+T** by default, changed in [Configure](configure.md). The tray icon's menu offers Add new Task, View Tasks, Configure, and Exit, for when the mouse is closer than the keyboard.

- **A left click on the tray icon opens Add, and a right click opens the menu.** Adding a task is the app's most common action, so it takes one click.
- **The tray icon uses [H.NotifyIcon.Wpf](https://www.nuget.org/packages/H.NotifyIcon.Wpf/2.4.1) 2.4.1.** Its menu is a Windows Presentation Foundation (WPF) menu, so it follows the app's theme.
- **Shell registers the shortcut itself**, with the Windows [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) function. Proposed: no shortcut library. This is a decision to confirm.
- **A taken shortcut is reported, not hidden.** Registration fails when another app holds the shortcut; Shell tells Configure, which says so and lets the user pick another. Windows reserves shortcuts with the Windows key for itself ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)), so a Windows update could take Win+Alt+T later; the same path handles that.

Proposed: a left click opens Add at once, without waiting to tell it from a double click, since the icon has no double-click action. This is a decision to confirm.

Proposed: until the device is configured, the first start, Add, and View Tasks open Configure, since there are no tasks to show yet. This is a decision to confirm.

### The app starts with Windows and runs once per user

- **Starting with Windows.** Proposed: Shell starts the app at sign-in through the current user's `Run` registry key ([Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)). It's on by default and switched in Configure. This is a decision to confirm.
- **One instance.** Proposed: starting the app while it runs shows View Tasks in the running instance. This is a decision to confirm.
- **Add is ready before it's needed.** The Add window is created hidden when the app starts, as the [Task editor](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) decides, so Shell shows it at once.
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

The composition root runs Velopack's own startup step first each time the app starts, before anything else, as Velopack requires ([Velopack integration](https://docs.velopack.io/integrating/overview)). When Velopack starts the app to install, update, or uninstall it, that step does the work and ends the app.

The [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) puts each version on GitHub Releases.

Proposed, each a decision to confirm:

- The app checks for updates at start and once a day. A failed check or download waits for the next check, with no message.
- A check is due 24 hours after the previous one began, by the clock, so time the PC spends asleep counts.
- The app looks at the clock at least once an hour while the PC is awake, so a check that fell due while the PC slept runs at most an hour after it wakes. Changing the PC's clock moves the next check: setting it forward brings the check sooner, and setting it back delays it by as much. Time zones and daylight saving time don't move it. Likely, but not confirmed by a run: one 24-hour timer wouldn't do, since on Windows 8 and later a timed wait doesn't count time asleep ([WaitForSingleObject](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject)), so it would wait 24 hours of awake time.
- Stopping the app also stops a check that's under way. Velopack's check can't be cancelled, so the app stops waiting for it and lets its request end in the background; a download is cancelled.
- The app and the [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) use Velopack's default channel, and the app ignores GitHub prereleases.
- **The installer's ID is `MemoriaToDo`.** Velopack installs the app in `%LocalAppData%\MemoriaToDo`, and uninstalling deletes that whole folder ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). The repository's name, `Memoria-ToDo`, can't be the ID: each developer PC keeps the [pinned SDK](../technology-stack.md#three-files-pin-every-version) in `%LocalAppData%\Memoria-ToDo\dotnet`, which uninstalling would delete. Installed copies keep their ID, so it must be confirmed before the first version tag is pushed; after that release, the ID can't change.
- The installer shows the [app's name](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) in its Start menu shortcut and in Windows' list of installed apps.
- The installer adds a Start menu shortcut and no desktop shortcut, since the app starts with Windows and lives in the tray. Velopack adds both by default ([vpk options](https://docs.velopack.io/reference/cli/content/vpk-windows)).
- The installer isn't code-signed, so Windows may warn when it's first run. Velopack recommends signing, since an unsigned app may be flagged as a virus ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)).
- Uninstalling removes the `Run` key.

## Public interface

No module uses Shell. Only the composition root does, through one part:

- **Create the tray app**: takes the Show parts of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface), the implementations of Shell's two ports below, and a time source. It returns the app's lifetime, which the [desktop platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) starts and stops.
  - **Starting** adds the tray icon with its menu, and begins the [update checks](#velopack-installs-the-app-and-keeps-it-up-to-date). Each menu item calls its window's Show part, and Exit ends the app. A left click calls Show Add.
  - **Stopping** removes the tray icon and ends the update checks.
  - Proposed: Stopping works even if Starting failed or never ran, and when it's repeated. It ends the update checks before it removes the icon. When the icon can't be added, Starting fails before the update checks begin. This is a decision to confirm.

- **The tray icon is a port Shell publishes.** Create takes its implementation, which receives the menu items and the left-click action, and removes the icon on Stop. The composition root implements it with the [chosen tray library](#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions). Shell's unit tests use the [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test); the real adapter has integration tests.
- **Updates are a port Shell publishes.** Its one part downloads a newer version, if there is one, for Velopack to apply at the next start. It does nothing when the app isn't installed, such as when it runs from a build folder, since Velopack updates only a copy it installed ([UpdateManager](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)). It fails when the check or download fails. Create takes its implementation, which the composition root writes with Velopack, so only the composition root uses Velopack. Shell's unit tests use the [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test); the real adapter has integration tests.
- **Proposed: an implementation of the update port does no slow work before its first wait**, since Shell starts the first check on the UI thread while the app starts. This is a decision to confirm.
- **Proposed: the time source is .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)**, which gives the update checks the time and their timer. The composition root passes the system's. Tests pass a hand-written fake, kept with the [shared fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test), that moves time forward only when told, so no test waits. It needs no package and no interface of ours. The shared library's [clock](../modules.md#the-platform-serves-every-module-and-knows-none) isn't designed yet; its design decides whether Shell moves to it. This is a decision to confirm.

## Failure modes

- **The shortcut is taken**: registration fails, and Configure says so and lets the user pick another. The tray menu still works.
- **Sync fails**: a warning badge, one notification per new kind of error, and details in the tooltip and Configure.
- **An update can't be checked, downloaded, or applied**: the app keeps running its current version and tries again at the next check.
- **The app is started a second time**: the new process hands over to the running one, which shows View Tasks, and exits.
- **Windows can't add the tray icon**: starting fails, so the app shows the error and ends, as the [desktop platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) does for every error no module handles. Without the icon, nothing opens View Tasks or Configure, or ends the app. Starting with Windows may start the app before the taskbar exists, so that step revisits this rule.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Opening Add:
  - **Win+Alt+N**: the OneNote desktop app uses it for Quick Notes ([OneNote shortcuts](https://shortcut-tools.com/en/shortcuts/onenote/)).
  - **Ctrl+Alt combinations**: on European keyboard layouts they equal AltGr, which types characters.
  - **The tray menu only**: slower than a shortcut.
  - **The NHotkey library**: RegisterHotKey takes a few lines.
- Tray icon:
  - **The Windows Forms NotifyIcon**: its menu looks like Windows Forms and ignores dark mode.
  - **Either mouse button opens the menu**: adding a task, the most common action, would take two clicks.
  - **Waiting for the taskbar when the icon can't be added**: more code for a case the first desktop step can't reach.
  - **The tray implementation stays inside Shell**: unit and UX tests cannot reach the menu without starting a real tray icon.
  - **The desktop platform hosts the tray implementation**: only Shell uses it, so it does not belong among code shared by modules.
- Sync status:
  - **A notification for every failure**: it nags during long offline stretches.
  - **Status only in Configure**: a broken sync would go unnoticed.
- Installing:
  - **Inno Setup**: no automatic updates.
  - **No installer, a zipped folder**: updates by hand.
  - **A desktop shortcut too, Velopack's default**: the app starts with Windows and lives in the tray, so the shortcut would rarely be used.
- When the app checks for updates:
  - **24 hours of the app running, not of the clock**: a PC that sleeps or is off most of the day would check far less than daily.
- How Shell reaches Velopack's update check:
  - **Shell calls Velopack directly**: its update rules could only be tested against GitHub, the reason the tray implementation left Shell.
  - **The composition root runs the update checks**: the update rules would leave Shell, and the composition root only builds the app from the modules.
- Where the update checks get the time:
  - **A timer port of Shell's own**: the shared library's clock would later duplicate it.
  - **The shared library's clock, built now**: it would be designed for one desktop need, before [Tasks](../tasks.md) says what it needs.
- The installer's ID:
  - **`Memoria-ToDo`, with the SDK moved to another folder**: every developer PC would move its SDK, and a decided path would change.
  - **`Memoria.ToDo`, like the projects' names**: the dot reads like a file extension.
