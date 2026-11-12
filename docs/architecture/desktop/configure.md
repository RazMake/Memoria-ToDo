# Configure: where the desktop app's settings live

Status: Drafting

Configure is the desktop app's settings window. It changes the two global shortcuts, starting with Windows, and the deletion delay, and it sets up sync and shows its status, which Sync owns.

## Behavior

### Each setting has one owner

The table lists every setting in Configure, the module that owns it, where it's kept, and its default:

| Setting | Owner | Kept | Default |
| --- | --- | --- | --- |
| Global shortcuts for Add new task and Manage tasks | Tasks stores the values; Configure checks them | In the repository's `settings.json`, shared by every device, from the [Sync step](README.md#delivery-backlog); until then, nowhere | As [Shell](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) decides |
| Start with Windows | Configure | In the current user's `Run` registry key, on this device, as [below](#start-with-windows-is-the-run-key-itself) | On |
| Days before a resolved task is deleted | Tasks | In the repository, shared by every device | One week, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides |
| The repository's SSH address, the device name, the device's SSH key, and the accepted host keys | Sync | On this device | The device name starts as the machine's name; the rest is set up as [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) says |

**Every device uses the same shortcuts.** Until the [Sync step](README.md#delivery-backlog), nothing saves them: the app starts with the defaults each time, and a new shortcut lasts until the app exits. From the Sync step, Configure saves them through Tasks in [`settings.json`](../tasks.md#the-repository-holds-task-files-image-folders-and-one-settings-file) and reads them there when the app starts. Another app may hold a shortcut on one PC only, yet picking a new one there changes it on every PC. This is an accepted risk, for the same shortcuts everywhere.

Tasks keeps the two shortcuts as shared text settings without interpreting them. Windows Configure checks their values against its [shortcut rules](#a-new-shortcut-is-checked-at-once). Android preserves them through merges but neither displays nor uses them.

**Until the Sync step, the window holds the shortcuts, Start with Windows, and the sync repository.** Each shortcut, the Start with Windows check box, and the repository section show their problems under them, such as a taken shortcut or a failed write. The window has no line naming features not built yet; this is an exception to that backlog rule, by the user's choice. A user who changes a shortcut finds its default back after the app restarts, with no warning, until the Sync step.

Configure reads and changes the deletion delay through Tasks, so a change on one device reaches every device; [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) sets its allowed range. Configure sets up sync through Sync, in the steps that [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) lists.

**Configure checks each value as it's entered, and applies or saves nothing invalid.**

Proposed: the deletion delay shows in days. This is a decision to confirm.

### The window shows its settings in labeled sections

The window shows each shortcut with the name of what it does, Add new task and Manage tasks, and it's wide enough to read at a glance. Proposed, each a decision to confirm:

- **Three collapsible sections, in order: Shortcuts, Startup, and Sync settings.** All start collapsed, and expanding one collapses the others, so one is open at a time.
- **Shortcuts starts with a line saying how to change one**: click its field and press the new keys, or press Esc to keep the current one. Then each shortcut has a row, Add new task first: its name on the left and its field on the right, with the row's message under the field. The names share one column, so both fields line up, 5 pixels apart while neither has a message.
- **Startup holds the Start with Windows check box**, a line of help under it, and its message.
- **Sync settings shows the current repository and its local path**, and a Configure button offers two ways to change them: clone a repository, or use a clone the user already has. See [below](#the-sync-settings-section-sets-the-devices-clone).
- **Text styles and colors come from the app's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it)**, so the window follows light and dark mode: headings in the theme's heading style, messages in its error color, and help lines small, in its secondary text color, at 70% opacity.
- **The window keeps its width.** The user can't resize it; its height follows the open section and its messages, which take no room while empty.
- **The window opens with no field focused**, so the shortcuts keep working until the user clicks a field, as [below](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) explains.

### A new shortcut is checked at once

The user clicks a shortcut's field and presses the new keys. The field shows them, and Configure checks them at once; Esc leaves the shortcut as it was. Configure accepts a shortcut only if it follows all three rules:

- **Two or more of Ctrl, Alt, Shift, and Win, plus one other key.** A shortcut with one modifier key can take a common key from every app: Ctrl+C would stop copying everywhere.
- **Never F12**, since Windows keeps it for debuggers ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).
- **Different from the other shortcut.** Two functions can't share keys: Windows would refuse whichever registered second, and the message would wrongly blame another app. A pick equal to the row's own shortcut changes nothing: Configure doesn't register it again, and the row's message stays as it was.

As the app starts, Configure registers each shortcut's default through its [shortcut action](#public-interface), then each valid shortcut the user picks on that row. If Windows or another app holds a shortcut, Configure [says why](#configure-explains-every-shortcut-it-cant-use) and lets the user pick another, and the previous shortcut stays active, as [Shell](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) decides.

Proposed, each a decision to confirm:

- The field always shows its row's active shortcut, or nothing when the row has none, and the message under it says why.
- The field shows modifier keys in the order Win, Ctrl, Alt, Shift.
- Plain Tab moves on from the field instead of being picked, so the field doesn't trap the keyboard.
- Holding a key picks it once: the field ignores the key's auto-repeat.
- Only modifier keys pressed while the field has focus count. Holding Ctrl+Shift, clicking into the field, then pressing K is rejected for too few modifier keys, so the user presses the keys again in the field. Counting keys held before the click would need the keyboard's real state, which only a real-input test with the window in the foreground can check.
- A pick that breaks both of the first two rules, such as Ctrl+F12, gets the F12 message.

### While a shortcut field has focus, the app's shortcuts are released

While either shortcut field has the keyboard focus, Configure unregisters both shortcuts, so their keys reach the field instead of opening a window. The user can press a shortcut's own keys again, or the other shortcut's keys and get the [message](#configure-explains-every-shortcut-it-cant-use) that it's in use. Before, pressing an active shortcut in the field opened its window.

- **When neither field has the focus, Configure registers each row's shortcut again.** That happens when the user tabs or clicks away, another app takes the focus, or the window closes, also in the middle of a pick. Moving from one field to the other keeps both released.
- **A valid pick registers at once**, which checks that it's free, then is released again until the focus leaves the fields.
- **A shortcut that can't register again**, such as when another app took it meanwhile, leaves its row with no shortcut, and the row's message says why.
- **A shortcut that can't be released when a field gets the focus** stays active, its row's message shows the error, and Configure still releases the other one. Proposed; a decision to confirm.

Proposed: Configure releases both shortcuts, not only the row's own, since pressing the other's keys would open its window and take the focus from the field. This is a decision to confirm.

### Configure explains every shortcut it can't use

Each shortcut row shows one message under its field whenever Configure can't use a shortcut, and each message says what to do next. A later pick that registers clears it; Esc leaves it showing. The table lists each case:

| Situation | The field shows | The message says |
| --- | --- | --- |
| A pick that Windows or another app holds | The row's shortcut, unchanged | Windows or another app already uses those keys; press different ones |
| A shortcut that can't register as the app starts, or again when the focus leaves the fields | Nothing, since the row has no shortcut | Windows or another app already uses those keys, so the row's function has no shortcut; click the field and press different ones |
| Two or more modifier keys pressed together, then all released with no other key reaching the field | The row's shortcut, unchanged | If a key was pressed with those modifier keys, Windows or another app uses that shortcut; press a different one |
| A pick the other shortcut uses | The row's shortcut, unchanged | The other shortcut's name uses those keys; press different ones |
| A pick that breaks one of the first two [rules](#a-new-shortcut-is-checked-at-once) | The row's shortcut, unchanged | Which rule it breaks |
| An action that fails for another reason | The row's shortcut, or nothing as the app starts | The error |

- **A key that Windows or another app takes never reaches the field.** Windows gives a registered shortcut's key to the app that holds it, and only the modifier keys reach the window. So the field reads two or more modifier keys pressed together and released with no other key as a sign of a taken shortcut. Proposed, each a decision to confirm:
  - It says nothing for a single modifier key, since a shortcut needs two and Ctrl or Shift alone is often pressed by itself.
  - It says nothing when the field lost the focus first, since the app that holds the key may have opened its own window.
  - Modifier keys released without pressing a key also show the message, which its "if" covers.
- **The field takes every modifier key's release, Alt's included.** Otherwise releasing Alt with no other key makes Windows move the keyboard to the window's hidden system menu ([WM_SYSKEYUP](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-syskeyup)), and later keys go to that menu instead of the field. That's the likely reason the user couldn't change a shortcut a second time: when a taken shortcut's key went to another app, the field got Alt alone.

Not confirmed by a run:

- **That the modifier keys reach the field when Windows gives the other key to a registered shortcut.** The taken-shortcut guess needs them.
- **That taking Alt's release keeps the keyboard in the field**, and that the system menu was why a shortcut couldn't change a second time.

### Start with Windows is the `Run` key itself

The setting is the current user's `Run` registry key ([Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)): Start with Windows is on while the key holds the app's entry. No file keeps a copy.

- **The entry starts the app through Velopack's starter**, the program Velopack keeps in the install folder, whose path survives updates ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). It adds the argument that marks a [start at sign-in](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-view-tasks).
- **Installing turns it on, and uninstalling removes it.** Velopack runs the app during install and uninstall for such steps, which must be fast and show no window ([hooks](https://docs.velopack.io/integrating/hooks)). The install step writes the entry once, and updates leave it alone, so no start turns back on what the user turned off. A step that can't write or remove the entry goes on quietly, since a step can't stop the install or uninstall: after a failed install step Configure shows the setting off, and after a failed uninstall step the entry stays. Proposed: the step writes its error to [Velopack's log](https://docs.velopack.io/troubleshooting/debugging), since a step shows no window. This is a decision to confirm.
- **Configure shows and switches it through a port Configure publishes.** The check box shows whether the entry is there when the window opens, and a switch writes or removes the entry at once. The composition root implements the port, and its Velopack install and uninstall steps use the same code.
- **Only an installed copy can switch it.** A copy run from a build folder shows the setting, but switching it fails, since the entry must name the installed starter.

The entry is named with the [app's name](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), and its value is the starter's quoted path followed by the [sign-in argument](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-view-tasks). Changing the app's name or the argument after the first release would leave existing entries out of date.

Not confirmed by a run: Windows' [Startup apps page](https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows) seems to keep its own on and off list beside the key, so Configure could show on while Windows keeps the app off.

### The Sync settings section sets the device's clone

The section keeps the folder of this device's git clone, through a port Configure publishes and the composition root implements.

- **The section shows three lines, each starting with its name in bold**: Repository, the clone's `origin` address; Local Path, the clone's folder; and Identity, the name and e-mail the clone's commits are made as, which git reads from the clone's settings, then the user's. Each shows "None" when there's none. A failure to read them shows as the section's message.
- **A Configure button opens a menu with two choices**: Clone a repo, and Use existing repo clone. Each shows only its own controls under the lines, replacing the other's. Clicking Clone or Use this clone hides the controls at once; on success the lines update, and on failure the controls show again with the error. Proposed; a decision to confirm.
- **Clone a repo** uses Sync's device-key SSH transport, into an empty or new folder. A clone failure shows its error, and the current folder stays.
- **Use existing repo clone** asks Sync to validate and select the folder. The currently selected clone remains the starting point; setup never forces a replacement clone.
- **The folder's path is kept on this device**, in `repository-folder.txt` under `%LocalAppData%\Memoria-ToDo`, apart from Velopack's install folder.

From the Sync step, [Sync's setup contract](../../../src/Shared/Sync/Setup/ISyncSetup.cs) replaces the interim
installed-Git port. Keep the existing section layout and clone/existing-clone choices. The Identity line now names
the device used for Sync commits, rather than reading a separate installed-Git identity. Address and device name
are editable during setup. Generate, Copy public key, and the host's add-key-page action use Sync's cached setup
state; an unknown custom host offers explicit fingerprint acceptance, never implicit trust. After selecting or
cloning, Configure awaits the first manual sync so a read-only key fails here. An offline or failed first pass
leaves the selected clone available for retry and local work, but does not mark setup complete.

### Configure shows how sync is doing

Configure shows the last successful sync time, the files sync skipped, and, while sync fails, the error's details; the tray shows the same in short, as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) says. Configure reads them from Sync. Its Sync now button starts a sync at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.

Available Offline details stay visible inline, including before the first success: "Not synced yet" does not
replace the reason. Shell's quiet-offline policy does not hide these details.
Setup and status presentation subscriptions belong to each open window and detach when it closes. Closing
cancels and observes that window's waits, not the shared sync. These subscriptions are separate from the
[settings/shortcut lifetime watcher](#shared-settings-refresh-even-while-configure-is-closed).

### Shared settings refresh even while Configure is closed

Configure receives [`ISharedTaskSettings`](../../../src/Shared/Tasks/ISharedTaskSettings.cs), not another settings
store. It loads the two shortcut texts when Shell connects their actions and keeps watching imported changes while
the window is closed, so global shortcuts update without opening Configure. Null uses the existing defaults.
The existing shortcut display formatter is also the Windows text format; one internal parser reverses it.
Invalid imported text stays stored, is reported, and leaves that action's previous valid local shortcut in place;
if there is none, Configure tries its existing default. It never writes a guessed repair into the shared file.

Valid local picks and deletion-delay edits save through the typed setters. Failed persistence restores the prior
local value and registration and reports the failure, including any restoration failure. Existing duplicate-key,
reserved-key, Esc, focus release/restore, and taken-shortcut behavior stays unchanged. Settings notices arriving
off-thread dispatch to the UI; a notice during a focused shortcut edit does not re-register a released action.

The deletion delay is shown in days inside Sync settings, using Tasks' range and defaults rather than new literals.
A failed delay read shows its error and retains the last successfully shown text, or leaves the field blank if no
value has been shown. It never substitutes or writes a guessed default. A successful refresh shows the current value
and clears the stale read error.
The settings/shortcut watcher outlives an ordinary window close and ends when Shell disposes Configure.
Queued UI callbacks do nothing after disposal.

## Public interface

The composition root and Shell use these parts:

- **[Create Configure](../../../src/Desktop/Configure/ConfigureWindow.cs)**: App supplies Start with Windows, typed Tasks settings, the shared Sync service, and the setup-actions port. The window opens only on Show Configure.
- **Show Configure**: opens the window, or brings it forward. Configure also says whether its window is open, so Shell can [ignore shortcuts](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) meanwhile.
- **Setup and status** come directly from Sync; Configure publishes no parallel clone, Git identity, or repository-settings port. The desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show) still governs opening and closing.
- **Connect the shortcuts**: Shell calls it once, when it starts, with one [shortcut action](#public-interface) for Add new task and one for Manage tasks. Configure registers each shortcut's default at once through its action, then each valid pick on that row, and releases and registers both as [a field's focus](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) comes and goes, always on the UI thread. It returns whether both shortcuts registered, so Shell can show Configure when one didn't. When an action fails, Configure treats it as a setting that [can't be applied](#failure-modes), and that shortcut counts as not registered.
- **A shortcut action**: an interface Configure publishes and Shell implements, one for each shortcut, as [Shell](shell.md#public-interface) says. It has two parts:
  - Register takes a shortcut and registers it in place of the action's previous one. It returns false, keeping the previous one, when Windows or another app holds the shortcut, and true at once when the shortcut is already the action's.
  - Unregister releases the action's shortcut, so its keys reach the focused window, and does nothing when there's none.
  - Both fail on any other error, and a failed Unregister keeps the shortcut registered.
- **A shortcut**: a value of modifier keys and one other key. Configure makes only shortcuts that follow the first two of its [rules](#a-new-shortcut-is-checked-at-once); Shell and its global shortcut port pass them on. Proposed, each a decision to confirm:
  - It's made of WPF's [modifier keys](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.modifierkeys) and [key](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.key), so no module converts between key formats.
  - It accepts only Ctrl, Alt, Shift, and Win as modifier keys, and only a real key: one WPF defines, but not the placeholders WPF reports for a key pressed with Alt, a key an input method handles, or a dead key. So every shortcut a caller gets follows the rules.
- **Dispose Configure**: detaches shared settings/setup/status subscriptions and closes its window. Shell still owns shortcut unregistration and Sync's lifetime.

Configure publishes two ports:

- **Start with Windows** retains its existing `Run`-key contract.
- **[Setup actions](../../../src/Desktop/Configure/ISetupActions.cs)** copies the displayed public key and opens Sync's supplied add-key page. App adapts its existing clipboard and browser components; Configure never reaches another window module's port. Failures are shown under setup.

The composition root implements these ports. Their real adapters have integration tests; Configure's UX tests use
the existing shared fake projects. The old repository-folder port and its production/test adapters are retired,
not retained as compatibility paths beside Sync.

## Failure modes

- **A shortcut is taken**: Configure says why under its field, and the previous shortcut stays active. When a default is taken as the app starts, that shortcut has none: Shell opens Configure, which says why, and the tray menu still works.
- **A key Windows or another app takes never reaches the field**: Configure guesses from the modifier keys alone and says the shortcut is probably taken.
- **A shortcut can't register again when the focus leaves the fields**: its row has no shortcut, and its message says why.
- **A shortcut can't be released when a field gets the focus**: it stays active, and its row shows the error.
- **A shortcut breaks the rules**, such as one with a single modifier key, F12, or the other shortcut's keys: Configure says why under the field, and the active shortcut stays.
- **A setting can't be applied or saved**, such as a failed write to the `Run` key, a switch in a copy that isn't installed, or a shortcut that fails for a reason other than being taken: Configure shows the error and keeps the previous value.
- **Installing can't write the `Run` key**: the app still installs, since Velopack's install step can't stop the install ([hooks](https://docs.velopack.io/integrating/hooks)). The app doesn't start at sign-in, and Configure shows the setting off.
- **Setting up sync fails**, such as when the host rejects the key or the repository isn't found: Configure shows the error, as [Sync through git](../sync.md#failure-modes) lists, and the device stays unconfigured until setup succeeds.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Where the shortcuts are kept:
  - **Each device keeps its own shortcuts, in a file on the device**: each PC could work around its own conflicts, but the same shortcuts on every device were preferred.
- Which module owns the shared shortcuts:
  - **Tasks keeps named settings of any kind, and Configure owns the shortcuts' names and meaning**: an extension point for desktop-only settings, with different rules for the deletion delay and shortcuts.
  - **Tasks owns the shortcuts' validation too**: Windows keyboard rules would live in the library Android loads.
- Where Start with Windows is kept:
  - **A file of this device's settings under `%LocalAppData%`, which each start makes the `Run` key match**: two homes for one fact, and a file that can be unreadable. A key another tool removed would come back at the next start.
  - **No setting in Configure, only Windows' Startup apps page**: Configure loses a setting, and users must find Windows' page.
  - **Shell writes the `Run` key for Configure**: Shell would only pass Configure's calls along.
- How Shell and Configure share the shortcuts:
  - **Configure announces each change, and Shell reports whether Windows took it**: after a taken shortcut, Configure must step back to the previous one without announcing it, or Shell registers it again. Each test follows a notice and a report.
  - **Shell owns the shortcuts' values, and Configure reads and changes them through an interface it publishes and Shell implements**: Shell's public interface gains a second create part. From the Sync step, Configure must still hand the saved shortcuts to Shell, which may not use Tasks.
  - **One function per shortcut that makes it active, as the first desktop step had for its one shortcut**: Configure now also releases the shortcuts while a field has focus, which a function can't do.
  - **One action with an argument naming the shortcut**: both modules would share a public list of shortcuts, and a new shortcut would change it.
- Pressing the app's own shortcuts in a field:
  - **Shell sends presses of its shortcuts to the field while a field has focus**: Shell would follow Configure's focus, and the field would read the press as a taken shortcut.
  - **Releasing only the row's own shortcut**: pressing the other's keys would open its window instead of showing why they can't be used.
- Telling that a key never reached the field:
  - **Raw input, which may see keys a registered shortcut takes**: unsure whether Windows delivers them. A probe that registers for raw keyboard input and logs what arrives while Win+Alt+T is pressed would tell, but the modifier keys already allow the message.
  - **Saying nothing**: the user pressed keys and saw nothing happen, with no reason.
- Laying out the window:
  - **Sizing the window to what it holds, the first desktop step's way**: it was cramped, and would jump in size as messages come and go.
- Entering a shortcut:
  - **Check boxes for Ctrl, Alt, Shift, and Win, and a list of keys**: slower, and looks dated.
  - **Typing it, such as `Win+Alt+N`**: typing mistakes, and a format to explain.
- Which shortcuts Configure accepts:
  - **One modifier key is enough, except F12**: a user could take Ctrl+C or Alt+F from every app by mistake.
  - **Anything Windows accepts**: the same, and Shift+T would stop typing a capital T.
- Saying the shortcuts aren't saved:
  - **A line under the shortcuts always says they last until the app exits, and that saving comes with sync**: one more line to read, for a limit that ends with the Sync step.
  - **The line shows only after the user changes a shortcut**: the same line, shown later.

The other settings Configure shows were chosen elsewhere, with their rejected options: the default shortcuts in [Shell](shell.md#ruled-out), the deletion delay in [purpose and scope](../purpose.md#ruled-out), and the sync settings in [Sync through git](../sync.md#ruled-out).
