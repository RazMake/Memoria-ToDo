# Shell: how the desktop app starts, opens its windows, and reports trouble

Status: Decided

Shell is the desktop app's tray icon and everything around it: the four global shortcuts, one running instance, what each start shows, starting and stopping [Updates](updates.md), and sync status. It opens the [Task editor](task-editor.md), the [Task list](task-list.md), the [Contact list](contact-list.md), and [Configure](configure.md); the [desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists what it needs from later designs.

## Behavior

### Four global shortcuts and the tray menu open the app's four functions

Four global shortcuts work from any app. The user changes each one in [Configure](configure.md#each-setting-has-one-owner), and each is the same on every device:

| Shortcut | Opens | Default |
| --- | --- | --- |
| Add new task | Add | **Win+Alt+N** |
| Task View | Task View | **Win+Alt+V** |
| Configure | Configure | **Win+Alt+C**, the user's choice |
| Contacts | [Contacts](contact-list.md) | **Win+Alt+O**, the user's choice |

The user asked for the Configure shortcut, and decided that it reaches Shell as a third [shortcut action](configure.md#public-interface) and [switches](#a-shortcut-switches-between-add-and-task-view) like the other two.

The user also asked for a Contacts shortcut, changed in Configure like the others, and a Contacts tray item, which opens the [Contact list's](contact-list.md#a-global-shortcut-and-the-tray-menu-open-contacts) window. The Contacts shortcut reaches Shell as a fourth shortcut action and switches as the table below says. Win+Alt+O wasn't probed with RegisterHotKey; a PC where it's taken follows the taken-shortcut rule below.

The tray icon's menu offers Add new task, Task View, Contacts, Configure, and Exit, for when the mouse is closer than the keyboard. Proposed, a decision to confirm: Contacts sits after Task View, beside the other window that lists things.

The user decided that **the Contacts tray item and shortcut show only when the clone [has contact files](../contacts.md#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts)**, in `autocomplete/contacts/`. Shell reads this from the [Contacts window port's](contact-list.md#public-interface) `IsAvailable` and follows its `AvailableChanged`, so a clone or a sync that adds or removes the files updates the menu at once. Proposed, each a decision to confirm:

- **The tray item is hidden, not disabled**, while there are no contact files.
- **Shell's Contacts shortcut action keeps the shortcut Configure gave it, and holds it in Windows only while contacts are available.** Register and Unregister keep their meaning to Configure; while unavailable, Register checks nothing with Windows, stores the shortcut, and returns true, and the keys reach the focused app. When contacts become available, Shell registers the stored shortcut; when Windows or another app holds it, Shell opens Configure, as a failed start-time registration does. When contacts become unavailable, Shell releases it. So Configure stays unaware of Contacts.
- **Configure's Contacts row always shows**, so the user can pick the keys before adding contact files.
- **An open Contacts window stays open when the files go away**; it shows what's left, and closes as usual.

Proposed for building in step 15's slice (e), each a decision to confirm:

- **What the Contacts action holds.** It keeps the shortcut Configure last gave it, until Unregister forgets it, and whether Windows holds that shortcut now. "The action's shortcut", which Register compares with, is the kept one while contacts are unavailable, and the one Windows holds while they're available. So a kept shortcut Windows refused when contacts arrived isn't the action's: Configure's next Register of it goes to Windows, fails, and Configure says why.
- **When contacts become available**, Shell registers the kept shortcut. It registers nothing when there's none, such as while a shortcut field in Configure has released the shortcuts; Configure registers it again when the focus leaves the fields.
- **When that registration finds the shortcut taken**, the action keeps no shortcut. If the start's [one automatic choice](#sync-stays-quiet-unless-something-is-wrong) hasn't run yet, the failure joins it, as a start-time registration failure does, so Configure opens last and in front. Otherwise Shell opens Configure at once. Configure [registers its rows again](configure.md#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) each time it's shown, so its Contacts row shows the shortcut as taken.
- **When contacts become unavailable**, Shell releases the shortcut and keeps it, so it registers again when they come back.
- **Any other failure of the global shortcut port** while availability changes isn't Configure's to show, since no Configure call made it: it passes on to the platform's unhandled-error path, which shows it and ends the app, as for every error no module handles. It's rare: Windows refuses a shortcut almost only because it's taken.
- **The menu changes as availability does.** Shell builds the menu from `IsAvailable` when it starts, and again on each `AvailableChanged`, and hands the new menu to the tray icon port's `UpdateMenu`, the user's choice, so the rule of which items show stays in Shell, where its unit tests reach it.
- **The Contacts item and shortcut follow the setup rule** that Task View's do: until the device is configured, they open Configure, and each counts as the user's action for the start's one automatic choice.
- **The defaults are the user's choice.** A RegisterHotKey probe on the user's PC found Win+Alt+T taken by Windows and Win+Alt+N free; Win+Alt+V and Win+Alt+C weren't probed. Microsoft's list gives Win+C to Copilot but lists no Win+Alt+C ([keyboard shortcuts in Windows](https://support.microsoft.com/en-au/accessibility/windows/keyboard-shortcuts-in-windows)). The defaults avoid Ctrl+Alt, which equals AltGr on many keyboard layouts ([WM_SYSKEYUP](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-syskeyup)). The OneNote desktop app uses Win+Alt+N for Quick Notes ([OneNote shortcuts](https://shortcut-tools.com/en/shortcuts/onenote/)); on a PC that runs it, Add new task has no shortcut until the user picks another, and Configure says why.
- **A shortcut never opens a window beside another.** It [switches between Add and Task View](#a-shortcut-switches-between-add-and-task-view) instead, and does nothing while Configure is open. Each window port says whether its window is open, and Shell checks all four before showing one.
- **A left click on the tray icon opens Task View, and a right click opens the menu.** The user's choice: checking the tasks is what a click on the icon is for, and Add keeps its global shortcut.
- **The tray icon uses [H.NotifyIcon.Wpf](https://www.nuget.org/packages/H.NotifyIcon.Wpf/2.4.1) 2.4.1.** Its menu is a Windows Presentation Foundation (WPF) menu, so it follows the app's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).
- **Shell runs each shortcut's action, and Configure picks the keys.** Shell gives Configure one [shortcut action](configure.md#public-interface) for each shortcut, which registers keys through Shell's [global shortcut port](#public-interface) and opens its window when pressed. The composition root implements the port with the Windows [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) function, with no shortcut library. [Configure](configure.md#a-new-shortcut-is-checked-at-once) checks each shortcut before Shell sees it.
- **A taken shortcut is reported, not hidden, and the previous one stays active.** Registration fails when Windows or another app holds the shortcut; the action's answer tells Configure, which [says why](configure.md#configure-explains-every-shortcut-it-cant-use) and lets the user pick another. The previous shortcut stays active until a new one registers. Windows reserves shortcuts with the Windows key for itself ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)), so a Windows update could take a default later; the same path handles that.

Proposed: a left click opens Task View at once, without waiting to tell it from a double click, since the icon has no double-click action. This is a decision to confirm.

Proposed: **when any shortcut can't register at start, Shell opens Configure**, which shows the shortcut without keys and why. A shortcut that silently does nothing looked broken. After a start by hand on a configured device, Task View opens first, then Configure, so Configure is in front; after a start at sign-in, Configure is the only window that opens. An unconfigured device or failed local initialization follows the decided setup rule below instead. A later start of a running app registers nothing, so it doesn't open Configure. This is a decision to confirm.

Proposed: the tray menu's first two items take the shortcuts' names, so the menu and Configure call each function the same. The windows keep their titles, Add new Task and Task View. This is a decision to confirm.

**Decided by the user:** until the device is configured, the first start, Add, and Task View open Configure.
After local initialization reports an unconfigured device or fails, startup opens Configure even at Windows
sign-in, so setup or its error is visible.

### A shortcut switches between Add and Task View

The user decided that pressing the other window's shortcut closes the open window and opens the other one, and pressing the open window's own shortcut brings it to the front, even from behind other windows. The table lists what each shortcut does, by the window that's open:

| Open window | Add new task shortcut | Task View shortcut | Configure shortcut | Contacts shortcut |
| --- | --- | --- | --- | --- |
| None | Opens Add | Opens Task View | Opens Configure | Opens Contacts |
| Add | Brings Add to the front | Closes Add, keeping its [draft](task-editor.md#closing-without-saving-keeps-a-draft-on-this-device) as Esc does, then opens Task View | Closes Add, keeping its draft, then opens Configure | Closes Add, keeping its draft, then opens Contacts |
| Task View | Closes Task View, asking first about a changed edit, then opens Add once it's closed | Brings Task View to the front, empties its search, and puts the cursor there | Closes Task View, asking first about a changed edit, then opens Configure once it's closed | Closes Task View, asking first about a changed edit, then opens Contacts once it's closed |
| Contacts | Closes Contacts, asking first about a changed edit, then opens Add once it's closed | Closes Contacts the same way, then opens Task View | Closes Contacts the same way, then opens Configure | Brings Contacts to the front, empties its search, and puts the cursor there |
| Configure | Nothing | Nothing | Brings Configure to the front | Nothing |

The user decided the Configure shortcut's column, and that Contacts is changed and switched like the other shortcuts. Proposed, a decision to confirm: the Contacts row and column mirror Task View's, as the [Contact list](contact-list.md#a-global-shortcut-and-the-tray-menu-open-contacts) says.

Closing Task View this way works as its close button does: a pending Undo finishes, as the [Task list](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete) says. The user decided that an [edit](task-list.md#edit-description-turns-the-right-side-into-the-editor) with changes asks first, offering Save, Discard, or Cancel, as the [Task list](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) says. Shell closes each window through its port's close part. It opens Add only when Task View's close part says the window closed, so Cancel, or a save that fails, leaves Task View open and Add closed. Add's close part hides it with its draft, then Shell opens Task View.

Proposed, each a decision to confirm:

- **A shortcut whose window is open brings that window forward**, the user decided, even while Configure is open; for Task View it also empties the search and puts the cursor there.
- **While Configure is open, the shortcut of a window that isn't open does nothing**, as before, since Configure is where the shortcuts change.
- **While two or more of Add, Task View, and Contacts are open**, which only the tray menu can cause, a shortcut whose window is open brings it forward, and a shortcut whose window isn't open does nothing, since closing two windows would ask two questions.
- **The tray menu and a left click on the tray icon keep opening their window** without closing another, since the user's rule is about the shortcuts.
- **Until the device is configured, every shortcut opens Configure**, as the setup rule above decides.

### Only one instance runs, and a start by hand shows Task View

- **One instance runs per Windows sign-in session.** Starting the app while it runs shows Task View in the running instance. The new instance lets the running one come to the front, which Windows may otherwise refuse ([AllowSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow)). Then it ends, without adding a tray icon, registering the shortcuts, or starting update checks. Every instance counts as the same app, even one run from a build folder.
- **For a configured device, a start by hand shows Task View, and a start at sign-in shows only the tray icon.** A start from the Start menu is by hand, and so is the first start after installing. The [`Run` key's entry](configure.md#start-with-windows-is-the-run-key-itself) adds the argument `--at-sign-in`, which marks a start at sign-in, and the composition root tells Shell which kind of start it is. The [startup exceptions above](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) govern unconfigured devices, failed initialization, and shortcut-registration failures.
- **Add is ready before it's needed.** The Add window is created hidden when the app starts, as the [Task editor](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) decides, so Shell shows it at once.

Proposed, each a decision to confirm:

- **The one-instance signal is a named semaphore, named with the [app's name](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), not the [installer's ID](#velopack-installs-the-app-and-keeps-it-up-to-date).** A copy run from a build folder has no installer ID. A semaphore counts each later start, so several quick starts each show Task View.
- **A later instance lets any process come to the front**, since it doesn't know the running instance's process ID. Letting only that process come forward would need the running instance to publish its ID.

Not confirmed by a run:

- **That Velopack's starter passes the sign-in argument on to the app**, also after an update. Its [source](https://github.com/velopack/velopack/blob/develop/src/bins/src/stub.rs) passes every argument on. No automated test can check it, since it needs an installed copy. To check it by hand:
  1. Install a release with its `setup.exe`. Start the app by hand once and finish device setup, so the [startup exceptions](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) don't open Configure: Sync is set up and every shortcut registers. Then exit the app from the tray menu.
  2. Run `%LocalAppData%\MemoriaToDo\Memoria.ToDo.Desktop.App.exe --at-sign-in`, the starter the `Run` key names. Only the tray icon shows, with no window.
  3. Exit the app, then run the starter with no argument. The tray icon and Task View show.
  4. Install a newer release over it, or let the app update itself, then repeat step 2. The result is the same.
- **That a second start hands over while a downloaded update waits.** Velopack applies a downloaded update when any instance starts, and its updater ends instances running from the install folder ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). Proposed by [Updates](updates.md#updates-tells-you-about-a-new-version-and-installs-it-only-when-you-agree): the composition root turns that apply at start off ([SetAutoApplyOnStartup](https://docs.velopack.io/reference/cs/Velopack/VelopackApp)), so a second start never applies one. To check it by hand, start an update, cancel Task View's prompt about a changed edit after the download, then start the app again: the running instance shows Task View.
- **That Task View comes to the front after a second start.**
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

Shell starts the shared service only after winning the one-instance claim. Before Start, it subscribes to setup
and status notices. Automatic startup navigation waits for Sync's
[initial local publication](../sync.md#one-owned-worker-keeps-synchronous-git-and-ssh-off-the-ui-thread), not its first
network result: the cold snapshot's false `IsConfigured` must not send an already configured device to Configure.
The initialization result, including failure, resolves pending startup navigation under the
[decided startup rule](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions).
This uses the existing local publication, not an additional status-only startup route.

This pending navigation runs at most once, on the UI thread. Shortcut-registration failure is an input to that
startup choice, handled by the [startup rules above](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions),
not a separate early navigation that consumes it. Opening a window for an explicit user action consumes the
pending choice; shutdown cancels it. Queued notices must not open a window after either event. No polling,
timeout, or blocking startup wait is added.

Manual Add, Task View, left click, and later-instance handoff still use the cached setup state at the time:
until configured they show Configure instead. This preserves the existing "no shortcut action while a window is open" guard.
An already configured device that becomes offline keeps opening its task windows; offline is not lost setup.

[`ITrayIcon`](../../../src/Desktop/Shell/ITrayIcon.cs) adds status updates and error notifications, not Sync or WPF
types. Its adapter marshals those calls to the UI, while Shell's status handling uses no WPF objects. Idle/working
use the plain icon, offline uses its badge without a notification, and errors use the warning badge. Shell remembers
notified failure kinds until a clean success, so retries do not spam notifications. Notification clicks open Configure.

Decision to confirm: the tooltip has one line for the app's name, then `Last synced` and the successful time in
the user's local short date/time format when there is one, then error/offline details when present. Put the sync
time before free-form details so long errors do not displace it when the tray adapter truncates the text.
Before the first success, the time line is absent. Lines use the platform's newline. Working adds no extra line.

### Shutdown joins owned work without blocking the dispatcher

[`IAppLifetime.StopAsync`](../../../src/Desktop/Platform/IAppLifetime.cs) replaces synchronous Stop.
The first call detaches sync notices, cancels and observes Shell's pending waits and startup navigation, disposes Configure, and releases
every shortcut and the tray on the UI thread. It then awaits the shared Sync service's disposal, then Updates' stop, and only then releases the one-instance claim. Proposed by [Updates](updates.md#an-update-downloads-the-version-then-restarts-the-app-into-it): Updates stops after Sync, since Updates' stop starts Velopack's updater when an update ended the app, and the updater waits only 60 seconds for the app to end. This is a decision to confirm. Cleanup attempts every step and retains
all failures. Repeated calls join the same cleanup; cancelling a caller's wait does not abandon it.

Updates retains its run Task instead of discarding it, and observes a check that
outlives cancellation, as [Updates](updates.md#a-check-runs-at-start-and-each-night-at-3-am) says. Shell never waits synchronously for either update or sync work.
The platform's [shutdown path](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) keeps WPF's
dispatcher running through the awaits. No public compatibility Stop method is retained.

### Velopack installs the app and keeps it up to date

The app installs with [Velopack](https://www.nuget.org/packages/Velopack/1.2.0) 1.2.0: a per-user setup.exe that installs under `%LocalAppData%` without administrator rights ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). [Updates](updates.md) checks this repository's GitHub Releases for a newer version and installs it, as that page says. The user decided that Updates is a module of its own, which Shell starts and stops; it replaces Shell's update checks, its updates port, and its update schedule.

The composition root runs Velopack's own startup step first each time the app starts, before anything else, as Velopack requires ([Velopack integration](https://docs.velopack.io/integrating/overview)). When Velopack starts the app to install, update, or uninstall it, that step does the work and ends the app. Its install and uninstall steps also turn [Start with Windows](configure.md#start-with-windows-is-the-run-key-itself) on and off.

The [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) puts each version on GitHub Releases.

**The installer's ID is `MemoriaToDo`, and it never changes.** Velopack installs the app in `%LocalAppData%\MemoriaToDo`, and uninstalling deletes that whole folder ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). The repository's name, `Memoria-ToDo`, can't be the ID: each developer PC keeps the [pinned SDK](../technology-stack.md#three-files-pin-every-version) in `%LocalAppData%\Memoria-ToDo\dotnet`, which uninstalling would delete. Installed copies keep their ID, so changing it after the first release would strand them.

Proposed, each a decision to confirm:

- The installer shows the [app's name](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) in its Start menu shortcut and in Windows' list of installed apps.
- The installer adds a Start menu shortcut and no desktop shortcut, since the app starts with Windows and lives in the tray. Velopack adds both by default ([vpk options](https://docs.velopack.io/reference/cli/content/vpk-windows)).
- The installer isn't code-signed, so Windows may warn when it's first run. Velopack recommends signing, since an unsigned app may be flagged as a virus ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)).

## Public interface

No module uses Shell. Only the composition root does, through one part:

- **[Create the tray app](../../../src/Desktop/Shell/TrayApp.cs)** retains its existing arguments and adds the shared Sync service last. It returns the platform's async-stop lifetime; no duplicate instance starts Sync. Proposed: its updates port argument becomes Updates' lifetime, `IUpdateLifetime updates`, in the same place, and its `TimeProvider time` argument goes, since only the update checks used it. This is a decision to confirm.
  - **Starting** first asks the one-instance port to claim the sign-in session. If another instance runs, Starting ends the app and does nothing else. Otherwise it adds the tray icon with its menu, connects Configure's shortcuts, starts [Updates](updates.md#a-check-runs-at-start-and-each-night-at-3-am) with Shell's app exit, and resolves its windows through the [startup rules](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) after local initialization. Each menu item calls its window's Show part, and Exit ends the app. A left click calls Show Task View, the global shortcuts follow the [switching rule](#a-shortcut-switches-between-add-and-task-view), and each later start calls Show Task View.
  - **Connecting the shortcuts** hands Configure four [shortcut actions](configure.md#public-interface): Add new task's opens Add, Task View's opens Task View, Configure's opens Configure, and Contacts' opens Contacts, each as the [switching rule](#a-shortcut-switches-between-add-and-task-view) says. Each action has at most one shortcut registered, through the global shortcut port, and none touches another's.
    - **Register** registers a new shortcut; when Windows takes it, the action releases its previous one. The shortcut already registered stays, and Register returns true without registering it again.
    - **Unregister** releases the action's shortcut, so its keys reach the focused window, and does nothing when there's none. A later Register registers again.
    - Any failure of the port other than a taken shortcut passes on to Configure.
  - **Stopping** follows the [owned asynchronous cleanup](#shutdown-joins-owned-work-without-blocking-the-dispatcher), so a later instance cannot claim the session while this one still owns native sync work.
  - Proposed: Stopping works even if Starting failed or never ran, and when it's repeated. When the icon can't be added, Starting fails before it connects the shortcuts and starts Updates. This is a decision to confirm.
  - Proposed: **Shell gives Updates the [app exit](updates.md#public-interface) it needs to restart the app.** Any window open is true while Add, Task View, or Configure is open. Close windows calls [Close Task View](task-list.md#public-interface), which asks about a changed edit, and returns its result; Add and Configure close with the app at End, and Add keeps its draft then, as it does on Exit. End ends the app as Exit in the tray menu does. This is a decision to confirm.
  - **Create the tray app takes the [Contacts window](contact-list.md#public-interface), `IContactsWindow contacts`, after the Task View window**, as the user's Contacts shortcut and tray item need. Its menu item and its shortcut action call its Show part as the switching rule says, and Configure's shortcut connection gets a fourth action for it. Proposed, a decision to confirm: any window open is also true while Contacts is open, and Close windows closes Contacts too, through its close part, which asks about a changed edit; it returns false when either Task View or Contacts stays open.
  - Proposed for building, each a decision to confirm: Close windows closes Task View first, and when Task View stays open it returns false without asking about Contacts, so the user answers one question at a time. Shell calls each close part whether or not its window is open, since a close part answers true for a window that isn't open. Stopping also releases the Contacts shortcut and stops following `AvailableChanged`, so a late notice changes no menu and registers nothing. The internal overload that takes the UI thread gets the same argument in the same place.

    ```csharp
    public static IAppLifetime Create(ITrayIcon trayIcon, IAddWindow add, IViewTasksWindow viewTasks,
        IContactsWindow contacts, IConfigureWindow configure, IUpdateLifetime updates, IGlobalShortcut shortcut,
        IOneInstance oneInstance, bool startedAtSignIn, ISyncService sync);
    ```
  - Proposed: when a new shortcut registers but releasing the action's previous one fails, the action releases the new one again, keeps the previous one, and passes the failure on to Configure. When releasing a shortcut fails, Stopping still releases the other, removes the tray icon, and gives up the claim, then passes the failures on. This is a decision to confirm.
  - If releasing the new one again fails too, both stay registered until the app ends, the previous one stays active, and Configure gets both failures.

Shell publishes three ports. The composition root implements each one, and Shell's unit tests use each one's [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test); the real adapters have integration tests. Shell's former updates port, its one part that downloaded a newer version for the next start, moves to Updates as its [install port](updates.md#velopack-checks-downloads-and-applies-behind-one-port), and its update schedule and the time source it needed move with it, as the user decided.

- **The tray icon port** receives the menu items and the left-click action, and removes the icon on Stop. Its implementation uses the [chosen tray library](#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions). The user decided: it also replaces the shown icon's menu, which the Contacts item needs:

  ```csharp
  /// Replaces the shown icon's menu, on the UI thread. Ignored before Show and after Remove.
  void UpdateMenu(IReadOnlyList<TrayMenuItem> menu);
  ```
- **The global shortcut port** has two parts. Register takes a [shortcut](configure.md#public-interface) and the action its press runs, and returns whether Windows took it: false means Windows or another app holds it. Unregister releases a shortcut it registered. Both fail on any other error, and a failed Unregister leaves the shortcut registered. A press runs its action on the UI thread. Its implementation uses [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
- **The one-instance port** has two parts. Claim takes the action to run when a later instance starts, and returns whether this instance is the first in the Windows sign-in session. If it is, the action runs on the UI thread each time a later instance starts. If it isn't, the port has told the running instance and let it come to the front. Release gives up the claim, and does nothing when there's none. Its implementation uses a named Windows signal, one per sign-in session.

## Failure modes

- **A shortcut is taken**: registration fails, and Configure says why and lets the user pick another; at start, Shell opens Configure to say so. The previous shortcut, if there is one, stays active, and the tray menu still works.
- **Sync fails**: a warning badge, one notification per new kind of error, and details in the tooltip and Configure.
- **An update can't be checked, downloaded, or applied**: the app keeps running its current version, as [Updates](updates.md#failure-modes) says.
- **The app is started a second time**: the new instance hands over to the running one, which shows Task View, and ends.
- **A start overlaps the running instance's exit**: the new instance finds the running one's signal, hands over, and ends, while the running one is already closing. Then no instance runs. Nothing is lost, and starting the app again works; this is an accepted risk.
- **Windows can't add the tray icon**: starting fails, so the app shows the error and ends, as the [desktop platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) does for every error no module handles. Without the icon, nothing opens Task View or Configure, or ends the app. A start at sign-in can come before the taskbar exists, and an app started that soon can't add its icon ([The Old New Thing](https://devblogs.microsoft.com/oldnewthing/20181213-00/?p=100475)). Windows gives no reason when adding fails, so a late taskbar looks like any other failure ([Shell_NotifyIcon](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw)). The user then sees the error at sign-in, and the app stays closed until started again; this is an accepted risk.

## Ruled out

The options below were considered and rejected, grouped by decision.

- The shortcuts:
  - **Win+Alt+T for Add, the first default**: Windows itself holds it, so registering it fails even with no other app running (the probe above).
  - **Ctrl+Alt combinations**: on European keyboard layouts they equal AltGr, which types characters.
  - **One shortcut, for Add only**: the user wants Task View one key press away too.
  - **A low-level keyboard hook, which sees keys before Windows' own shortcuts**: the user chose RegisterHotKey, with a message for each shortcut it can't use.
  - **The tray menu only**: slower than a shortcut.
  - **The NHotkey library**: RegisterHotKey takes a few lines.
- How the user learns that a shortcut couldn't register at start:
  - **A tray notification**: the tray icon port would need a new part, and the user would still open Configure to pick another.
  - **Nothing until Configure opens, the first desktop step's way**: the shortcut seemed broken, with no reason given.
- Tray icon:
  - **The Windows Forms NotifyIcon**: its menu looks like Windows Forms and ignores dark mode.
  - **Either mouse button opens the menu**: opening the tasks would take two clicks.
  - **A left click opens Add**: the user wanted a click to show the tasks.
  - **Waiting for the taskbar when the icon can't be added**: more code for a case the first desktop step can't reach.
  - **Keeping the app running without the icon, and adding it when the taskbar announces itself**: if the taskbar never came, the app would run without an icon until the user ended it in Task Manager.
  - **Waiting at most a few minutes for the taskbar, then failing**: a slow sign-in past the limit would still show an error, and the limit is one more value to choose and test.
  - **The tray implementation stays inside Shell**: unit and UX tests cannot reach the menu without starting a real tray icon.
  - **The desktop platform hosts the tray implementation**: only Shell uses it, so it does not belong among code shared by modules.
- What a start shows:
  - **Every first start shows only the tray icon**: Windows may hide the icon in the tray's overflow, so a start from the Start menu would seem to do nothing.
  - **Remain tray-only at sign-in when local initialization reports an unconfigured device or fails**: the user
    chose to show Configure immediately rather than leave setup trouble unnoticed.
- How a second start reaches the running instance:
  - **The platform checks before it creates the WPF application, and Run the app returns at once in a later instance**: the platform's public interface would grow, and the rule would leave Shell for a place with only integration tests.
  - **The running instance confirms each handover, and a new instance that gets no confirmation in time takes over**: more code and a time limit, for a rare case that loses nothing and that starting again fixes.
- Sync status:
  - **A notification for every failure**: it nags during long offline stretches.
  - **Status only in Configure**: a broken sync would go unnoticed.
  - **Error details before the last successful sync time in the tooltip**: a long error can push the time past
    the tray adapter's text limit.
- Installing:
  - **Inno Setup**: no automatic updates.
  - **No installer, a zipped folder**: updates by hand.
  - **A desktop shortcut too, Velopack's default**: the app starts with Windows and lives in the tray, so the shortcut would rarely be used.
- The update checks' earlier options are ruled out on [Updates' page](updates.md#ruled-out).
- The installer's ID:
  - **`Memoria-ToDo`, with the SDK moved to another folder**: every developer PC would move its SDK, and a decided path would change.
  - **`Memoria.ToDo`, like the projects' names**: the dot reads like a file extension.
- What happens to a changed edit in Task View when a shortcut closes it:
  - **Close anyway, discarding the edit**: one key press could lose a long edit, with no Undo, since the bar closes with the window.
  - **Don't switch while the edit has changes, and say why in Task View's message line**: the shortcut would seem to do nothing beyond the message.
- How Configure learns that the Contacts shortcut was taken when contacts arrived:
  - **A Lost event on the shortcut action**: Configure's public interface would grow, for a case its next Show already finds.
- How the tray menu shows and hides the Contacts item:
  - **Each item says when it shows, through `Func<bool>? IsShown` on `TrayMenuItem`, asked as the menu opens**: the adapter would run a rule only an integration test reaches.
  - **The item always shows, and does nothing without contact files**: it goes against the user's decision that the item shows only with contact files.

## Open questions

None.
