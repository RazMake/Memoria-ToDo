# Shell: how the desktop app starts, opens its windows, and reports trouble

Status: Decided

Shell is the desktop app's tray icon and everything around it: the two global shortcuts, one running instance, what each start shows, updates, and sync status. It opens the [Task editor](task-editor.md), the [Task list](task-list.md), and [Configure](configure.md); the [desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists what it needs from later designs.

## Behavior

### Two global shortcuts and the tray menu open the app's three functions

Two global shortcuts work from any app. The user changes either one in [Configure](configure.md#each-setting-has-one-owner), and each is the same on every device:

| Shortcut | Opens | Default |
| --- | --- | --- |
| Add new task | Add | **Win+Alt+N** |
| Manage tasks | View Tasks | **Win+Alt+V** |

The tray icon's menu offers Add new task, Manage tasks, Configure, and Exit, for when the mouse is closer than the keyboard.

- **The defaults are the user's choice.** A RegisterHotKey probe on the user's PC found Win+Alt+T taken by Windows and Win+Alt+N free; Win+Alt+V wasn't probed. The defaults avoid Ctrl+Alt, which equals AltGr on many keyboard layouts ([WM_SYSKEYUP](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-syskeyup)). The OneNote desktop app uses Win+Alt+N for Quick Notes ([OneNote shortcuts](https://shortcut-tools.com/en/shortcuts/onenote/)); on a PC that runs it, Add new task has no shortcut until the user picks another, and Configure says why.
- **While any of the app's windows is open, a shortcut press opens nothing.** So the user can't open Add from Configure, or View Tasks from Add. Each window port says whether its window is open, and Shell checks all three before showing one.
- **A left click on the tray icon opens Add, and a right click opens the menu.** Adding a task is the app's most common action, so it takes one click.
- **The tray icon uses [H.NotifyIcon.Wpf](https://www.nuget.org/packages/H.NotifyIcon.Wpf/2.4.1) 2.4.1.** Its menu is a Windows Presentation Foundation (WPF) menu, so it follows the app's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).
- **Shell runs each shortcut's action, and Configure picks the keys.** Shell gives Configure one [shortcut action](configure.md#public-interface) for each shortcut, which registers keys through Shell's [global shortcut port](#public-interface) and opens its window when pressed. The composition root implements the port with the Windows [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) function, with no shortcut library. [Configure](configure.md#a-new-shortcut-is-checked-at-once) checks each shortcut before Shell sees it.
- **A taken shortcut is reported, not hidden, and the previous one stays active.** Registration fails when Windows or another app holds the shortcut; the action's answer tells Configure, which [says why](configure.md#configure-explains-every-shortcut-it-cant-use) and lets the user pick another. The previous shortcut stays active until a new one registers. Windows reserves shortcuts with the Windows key for itself ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)), so a Windows update could take a default later; the same path handles that.

Proposed: a left click opens Add at once, without waiting to tell it from a double click, since the icon has no double-click action. This is a decision to confirm.

Proposed: **when either shortcut can't register at start, Shell opens Configure**, which shows the shortcut without keys and why. A shortcut that silently does nothing looked broken. After a start by hand, Configure opens after View Tasks, so it's in front; after a start at sign-in, it's the only window that opens. A later start of a running app registers nothing, so it doesn't open Configure. This is a decision to confirm.

Proposed: the tray menu's first two items take the shortcuts' names, so the menu and Configure call each function the same. The windows keep their titles, Add new Task and View Tasks. This is a decision to confirm.

Proposed: until the device is configured, the first start, Add, and View Tasks open Configure, since there are no tasks to show yet. This is a decision to confirm.

### Only one instance runs, and a start by hand shows View Tasks

- **One instance runs per Windows sign-in session.** Starting the app while it runs shows View Tasks in the running instance. The new instance lets the running one come to the front, which Windows may otherwise refuse ([AllowSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow)). Then it ends, without adding a tray icon, registering the shortcuts, or starting update checks. Every instance counts as the same app, even one run from a build folder.
- **A start by hand shows View Tasks, and a start at sign-in shows only the tray icon.** A start from the Start menu is by hand, and so is the first start after installing. The [`Run` key's entry](configure.md#start-with-windows-is-the-run-key-itself) adds the argument `--at-sign-in`, which marks a start at sign-in, and the composition root tells Shell which kind of start it is. From the Sync step, the [proposed](#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) redirect to Configure applies to a start by hand until the device is configured. Either kind of start also opens Configure when a shortcut can't register, as [proposed](#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) above.
- **Add is ready before it's needed.** The Add window is created hidden when the app starts, as the [Task editor](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) decides, so Shell shows it at once.

Proposed, each a decision to confirm:

- **The one-instance signal is a named semaphore, named with the [app's name](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), not the [installer's ID](#velopack-installs-the-app-and-keeps-it-up-to-date).** A copy run from a build folder has no installer ID. A semaphore counts each later start, so several quick starts each show View Tasks.
- **A later instance lets any process come to the front**, since it doesn't know the running instance's process ID. Letting only that process come forward would need the running instance to publish its ID.

Not confirmed by a run:

- **That Velopack's starter passes the sign-in argument on to the app**, also after an update. Its [source](https://github.com/velopack/velopack/blob/develop/src/bins/src/stub.rs) passes every argument on. A [manual check](../../../tests/Desktop/App/VelopackStarterTests.cs) covers it, since it needs an installed copy.
- **That a second start hands over after an update was downloaded.** Velopack applies a downloaded update when any instance starts ([SetAutoApplyOnStartup](https://docs.velopack.io/reference/cs/Velopack/VelopackApp)), and its updater ends instances running from the install folder ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). A second start could then end the running instance instead of handing over. If a run shows this, it needs a decision of its own.
- **That View Tasks comes to the front after a second start.**
- **How long a later instance takes to end.** It still starts WPF and makes the hidden Add window before Shell ends it.

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

The composition root runs Velopack's own startup step first each time the app starts, before anything else, as Velopack requires ([Velopack integration](https://docs.velopack.io/integrating/overview)). When Velopack starts the app to install, update, or uninstall it, that step does the work and ends the app. Its install and uninstall steps also turn [Start with Windows](configure.md#start-with-windows-is-the-run-key-itself) on and off.

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

## Public interface

No module uses Shell. Only the composition root does, through one part:

- **Create the tray app**: takes the Show parts of the [Task editor](task-editor.md#public-interface) and the [Task list](task-list.md#public-interface); [Configure's part](configure.md#public-interface), which Shell uses to show Configure and to connect the shortcuts; the implementations of Shell's four ports below; whether the app started at sign-in; and a time source. It returns the app's lifetime, which the [desktop platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) starts and stops.
  - **Starting** first asks the one-instance port to claim the sign-in session. If another instance runs, Starting ends the app and does nothing else. Otherwise it adds the tray icon with its menu, connects Configure's shortcuts, begins the [update checks](#velopack-installs-the-app-and-keeps-it-up-to-date), and shows View Tasks after a start by hand. Then, when Configure answers that a shortcut didn't register, it shows Configure. Each menu item calls its window's Show part, and Exit ends the app. A left click and the Add new task shortcut call Show Add, the Manage tasks shortcut calls Show View Tasks, and each later start calls Show View Tasks.
  - **Connecting the shortcuts** hands Configure two [shortcut actions](configure.md#public-interface): Add new task's opens Add, and Manage tasks' opens View Tasks. Each action has at most one shortcut registered, through the global shortcut port, and neither touches the other's.
    - **Register** registers a new shortcut; when Windows takes it, the action releases its previous one. The shortcut already registered stays, and Register returns true without registering it again.
    - **Unregister** releases the action's shortcut, so its keys reach the focused window, and does nothing when there's none. A later Register registers again.
    - Any failure of the port other than a taken shortcut passes on to Configure.
  - **Stopping** ends the update checks, releases both shortcuts, removes the tray icon, and gives up the claim, so a later start becomes the running instance.
  - Proposed: Stopping works even if Starting failed or never ran, and when it's repeated. It ends the update checks before it removes the icon. When the icon can't be added, Starting fails before it connects the shortcuts and begins the update checks. This is a decision to confirm.
  - Proposed: when a new shortcut registers but releasing the action's previous one fails, the action releases the new one again, keeps the previous one, and passes the failure on to Configure. When releasing a shortcut fails, Stopping still releases the other, removes the tray icon, and gives up the claim, then passes the failures on. This is a decision to confirm.
  - If releasing the new one again fails too, both stay registered until the app ends, the previous one stays active, and Configure gets both failures.

Shell publishes four ports. The composition root implements each one, and Shell's unit tests use each one's [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test); the real adapters have integration tests.

- **The tray icon port** receives the menu items and the left-click action, and removes the icon on Stop. Its implementation uses the [chosen tray library](#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions).
- **The updates port's** one part downloads a newer version, if there is one, for Velopack to apply at the next start. It does nothing when the app isn't installed, such as when it runs from a build folder, since Velopack updates only a copy it installed ([UpdateManager](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)). It fails when the check or download fails. Its implementation uses Velopack, so only the composition root uses Velopack.
- **The global shortcut port** has two parts. Register takes a [shortcut](configure.md#public-interface) and the action its press runs, and returns whether Windows took it: false means Windows or another app holds it. Unregister releases a shortcut it registered. Both fail on any other error, and a failed Unregister leaves the shortcut registered. A press runs its action on the UI thread. Its implementation uses [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
- **The one-instance port** has two parts. Claim takes the action to run when a later instance starts, and returns whether this instance is the first in the Windows sign-in session. If it is, the action runs on the UI thread each time a later instance starts. If it isn't, the port has told the running instance and let it come to the front. Release gives up the claim, and does nothing when there's none. Its implementation uses a named Windows signal, one per sign-in session.
- **Proposed: an implementation of the update port does no slow work before its first wait**, since Shell starts the first check on the UI thread while the app starts. This is a decision to confirm.
- **The time source is .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)**, as for [every module](../modules.md#the-platform-serves-every-module-and-knows-none), which gives the update checks the time and their timer. The composition root passes the system's. Tests pass a hand-written fake, kept with the [shared fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test), that moves time forward only when told, so no test waits. It needs no package and no interface of ours.

## Failure modes

- **A shortcut is taken**: registration fails, and Configure says why and lets the user pick another; at start, Shell opens Configure to say so. The previous shortcut, if there is one, stays active, and the tray menu still works.
- **Sync fails**: a warning badge, one notification per new kind of error, and details in the tooltip and Configure.
- **An update can't be checked, downloaded, or applied**: the app keeps running its current version and tries again at the next check.
- **The app is started a second time**: the new instance hands over to the running one, which shows View Tasks, and ends.
- **A start overlaps the running instance's exit**: the new instance finds the running one's signal, hands over, and ends, while the running one is already closing. Then no instance runs. Nothing is lost, and starting the app again works; this is an accepted risk.
- **Windows can't add the tray icon**: starting fails, so the app shows the error and ends, as the [desktop platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) does for every error no module handles. Without the icon, nothing opens View Tasks or Configure, or ends the app. A start at sign-in can come before the taskbar exists, and an app started that soon can't add its icon ([The Old New Thing](https://devblogs.microsoft.com/oldnewthing/20181213-00/?p=100475)). Windows gives no reason when adding fails, so a late taskbar looks like any other failure ([Shell_NotifyIcon](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw)). The user then sees the error at sign-in, and the app stays closed until started again; this is an accepted risk.

## Ruled out

The options below were considered and rejected, grouped by decision.

- The shortcuts:
  - **Win+Alt+T for Add, the first default**: Windows itself holds it, so registering it fails even with no other app running (the probe above).
  - **Ctrl+Alt combinations**: on European keyboard layouts they equal AltGr, which types characters.
  - **One shortcut, for Add only**: the user wants View Tasks one key press away too.
  - **A low-level keyboard hook, which sees keys before Windows' own shortcuts**: the user chose RegisterHotKey, with a message for each shortcut it can't use.
  - **The tray menu only**: slower than a shortcut.
  - **The NHotkey library**: RegisterHotKey takes a few lines.
- How the user learns that a shortcut couldn't register at start:
  - **A tray notification**: the tray icon port would need a new part, and the user would still open Configure to pick another.
  - **Nothing until Configure opens, the first desktop step's way**: the shortcut seemed broken, with no reason given.
- Tray icon:
  - **The Windows Forms NotifyIcon**: its menu looks like Windows Forms and ignores dark mode.
  - **Either mouse button opens the menu**: adding a task, the most common action, would take two clicks.
  - **Waiting for the taskbar when the icon can't be added**: more code for a case the first desktop step can't reach.
  - **Keeping the app running without the icon, and adding it when the taskbar announces itself**: if the taskbar never came, the app would run without an icon until the user ended it in Task Manager.
  - **Waiting at most a few minutes for the taskbar, then failing**: a slow sign-in past the limit would still show an error, and the limit is one more value to choose and test.
  - **The tray implementation stays inside Shell**: unit and UX tests cannot reach the menu without starting a real tray icon.
  - **The desktop platform hosts the tray implementation**: only Shell uses it, so it does not belong among code shared by modules.
- What a start shows:
  - **Every first start shows only the tray icon**: Windows may hide the icon in the tray's overflow, so a start from the Start menu would seem to do nothing.
- How a second start reaches the running instance:
  - **The platform checks before it creates the WPF application, and Run the app returns at once in a later instance**: the platform's public interface would grow, and the rule would leave Shell for a place with only integration tests.
  - **The running instance confirms each handover, and a new instance that gets no confirmation in time takes over**: more code and a time limit, for a rare case that loses nothing and that starting again fixes.
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
  - **A timer port of Shell's own**: it would repeat TimeProvider, which every module uses; [Modules](../modules.md#ruled-out) rules out a clock interface of ours.
- The installer's ID:
  - **`Memoria-ToDo`, with the SDK moved to another folder**: every developer PC would move its SDK, and a decided path would change.
  - **`Memoria.ToDo`, like the projects' names**: the dot reads like a file extension.
