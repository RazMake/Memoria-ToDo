# Configure: where the desktop app's settings live

Status: Decided

Configure is the desktop app's settings window. It changes the two global shortcuts, the shortcut that closes the app's windows, starting with Windows, and the deletion delay, and it sets up sync and shows its status, which Sync owns.

## Behavior

### Each setting has one owner

The table lists every setting in Configure, the module that owns it, where it's kept, and its default:

| Setting | Owner | Kept | Default |
| --- | --- | --- | --- |
| Global shortcuts for Add new task and Manage tasks | Tasks stores the values; Configure checks them | In the repository's `settings.json`, shared by every device | As [Shell](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) decides |
| The Close window shortcut, which closes the app's windows, as [below](#esc-and-the-close-window-shortcut-close-the-window) | Tasks stores the value; Configure checks it | In the repository's `settings.json`, shared by every device, like the global shortcuts | **Shift+Esc** |
| Start with Windows | Configure | In the current user's `Run` registry key, on this device, as [below](#start-with-windows-is-the-run-key-itself) | On |
| Days before a resolved task is deleted | Tasks | In the repository, shared by every device | One week, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides |
| The repository's SSH address, the device name, the device's SSH key, and the accepted host keys | Sync | On this device | The device name starts as the machine's name; the rest is set up as [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) says |

**Every device uses the same shortcuts.** Configure saves them through Tasks in [`settings.json`](../tasks.md#the-repository-holds-task-files-image-folders-and-one-settings-file) and reads them there when the app starts. Another app may hold a shortcut on one PC only, yet picking a new one there changes it on every PC. This is an accepted risk, for the same shortcuts everywhere.

Tasks keeps the shortcuts as shared text settings without interpreting them. Windows Configure checks their values against its [shortcut rules](#a-new-shortcut-is-checked-at-once). Android preserves them through merges but neither displays nor uses them.

Configure reads and changes the deletion delay through Tasks, so a change on one device reaches every device; [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) sets its allowed range. Configure sets up sync through Sync, in the steps that [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) lists.

**Configure checks each value as it's entered, and applies or saves nothing invalid.** Each setting shows its own problems under it, such as a taken shortcut or a failed write.

Proposed: the deletion delay shows in days. This is a decision to confirm.

### The window shows its settings in labeled sections

The window shows each shortcut with the name of what it does, Add new task, Manage tasks, and Close window, and it's wide enough to read at a glance. Proposed, each a decision to confirm:

- **Three sections, in order: Shortcuts, Startup, and Sync settings, and exactly one is always open.** The window opens with Shortcuts open. Clicking a closed section's title opens it and closes the open one; clicking the open section's title does nothing. No section can be closed on its own, so titles show no expand or collapse arrow.
- **A title works from the keyboard too**: Tab reaches it, and Enter or Space opens its section.
- **Each section is a box with rounded corners**, and so is each [card](#the-sync-settings-section-sets-the-devices-clone) inside Sync settings. Their colors and corners come from the theme, and a card stands out from the section around it.
- **The open section's content starts right under its title.** Space the content doesn't need stays at the section's bottom.
- **Section titles and every button in the window show the hand pointer** when the mouse is over them, so the user sees what can be clicked. Other windows' buttons keep the default pointer.
- **Shortcuts starts with a line saying how to change one**, in the window's normal text size and color and ending with a colon: "Click a box, then press the new shortcut. Esc keeps the current one:". Then each shortcut has a row, Add new task first, then Manage tasks, then Close window: its name on the left and its field on the right. Under the field, a short description says what the shortcut does, in the help style and in italics, then the row's message. The names share one column, so the fields line up.
- **The descriptions are**: Add new task, "Opens the Add window from any app."; Manage tasks, "Opens View Tasks from any app."; Close window, "Closes the app's window you're in, whatever has the focus."
- **Startup holds the Start with Windows check box**, a line of help under it, and its message.
- **Sync settings shows its settings on three cards**: Repository, Git Access, and Auto, as [below](#the-sync-settings-section-sets-the-devices-clone) says.
- **Text styles and colors come from the app's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it)**, so the window follows light and dark mode: section titles in the theme's heading style, card titles small and bold, messages in its error color, and help lines small, in its secondary text color, at 70% opacity.
- **The window keeps its size.** It's 800 pixels wide and 850 tall, or as tall as the screen allows on a shorter screen, and neither the user, the open section, nor its messages change that. The titles of the sections below the open one stay at the window's bottom, and the open section fills the space between; it scrolls when its content is taller. Messages take no room while empty.
- **The window opens with no field focused**, so the shortcuts keep working until the user clicks a field, as [below](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) explains.
- **Esc and the Close window shortcut close the window**, as [below](#esc-and-the-close-window-shortcut-close-the-window) says.

### Esc and the Close window shortcut close the window

Esc closes Configure, and the Close window shortcut closes any of the app's windows, Configure included. Both close a window exactly as its close button does, so nothing is saved or lost that the close button wouldn't save or lose.

- **Esc closes Configure unless a control uses it first.** An open menu, such as the Configure button's, closes first. In a shortcut field, Esc keeps the current shortcut and moves the focus out of the fields, so the shortcuts register again; a second Esc closes the window. Add [closes an open list first](task-editor.md#keys-save-close-and-move-between-fields) the same way.
- **The Close window shortcut closes the window whatever has the focus in it**: a shortcut field, a text box, or an open menu. It acts before any control sees the keys, so it also wins over a key a window uses for something else.
- **It never registers with Windows.** It works only while one of the app's windows has the focus, so it takes no keys from other apps.
- **It closes Add, View Tasks, and Configure**, through one [platform part](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows), so the key handling has one home.

Not confirmed by a run: that the shortcut reaches the window while the Configure button's menu is open, since a WPF menu takes the keys in a window of its own.

### A new shortcut is checked at once

The user clicks a shortcut's field and presses the new keys. The field shows them, and Configure checks them at once; Esc leaves the shortcut as it was. Configure accepts a global shortcut, Add new task's or Manage tasks', only if it follows all three rules:

- **Two or more of Ctrl, Alt, Shift, and Win, plus one other key.** A shortcut with one modifier key can take a common key from every app: Ctrl+C would stop copying everywhere.
- **Never F12**, since Windows keeps it for debuggers ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).
- **Different from the other shortcuts**, Close window's included. Two functions can't share keys: Windows would refuse whichever global shortcut registered second, and the message would wrongly blame another app; a global shortcut takes its keys before any window sees them, so Close window would never work. A pick equal to the row's own shortcut changes nothing: Configure doesn't register it again, and the row's message stays as it was.

**Close window has rules of its own**, since it never registers with Windows and works only inside the app's windows: two or more modifier keys plus any other key, F12 included, or one modifier key plus Esc or a function key, such as Shift+Esc; and different from both global shortcuts. Configure checks it the moment it's picked, saves it as it saves the others, and hands it to the app's windows at once. Invalid stored text is handled as for the global shortcuts [below](#shared-settings-refresh-even-while-configure-is-closed): the previous valid keys, or the default, stay in use. Configure can't know which keys the other windows use, so Close window wins over them; this is an accepted risk.

As the app starts, Configure registers each row's stored shortcut through its [shortcut action](#public-interface), or the row's default when none is stored or the stored text is [invalid](#shared-settings-refresh-even-while-configure-is-closed), then each valid shortcut the user picks on that row. If Windows or another app holds a shortcut, Configure [says why](#configure-explains-every-shortcut-it-cant-use) and lets the user pick another, and the previous shortcut stays active, as [Shell](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) decides.

Proposed, each a decision to confirm:

- The field always shows its row's active shortcut, or nothing when the row has none, and the message under it says why.
- The field shows modifier keys in the order Win, Ctrl, Alt, Shift.
- Plain Tab moves on from the field instead of being picked, so the field doesn't trap the keyboard.
- Plain Esc keeps the current shortcut, but Esc pressed with a modifier key is a pick, so Shift+Esc can be chosen for Close window. A global pick such as Ctrl+Shift+Esc follows the global rules; Windows holds that one, so it gets the taken message.
- Holding a key picks it once: the field ignores the key's auto-repeat.
- Only modifier keys pressed while the field has focus count. Holding Ctrl+Shift, clicking into the field, then pressing K is rejected for too few modifier keys, so the user presses the keys again in the field. Counting keys held before the click would need the keyboard's real state, which only a real-input test with the window in the foreground can check.
- A pick that breaks both of the first two rules, such as Ctrl+F12, gets the F12 message.

### While a shortcut field has focus, the app's shortcuts are released

While any shortcut field has the keyboard focus, Configure unregisters both global shortcuts, so their keys reach the field instead of opening a window. The user can press a shortcut's own keys again, or another shortcut's keys and get the [message](#configure-explains-every-shortcut-it-cant-use) that it's in use. The Close window shortcut is the exception: it has nothing to release, and pressing its current keys in any field closes the window, as [above](#esc-and-the-close-window-shortcut-close-the-window) says. The user picks new keys for it by pressing different ones.
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
| A pick another row uses | The row's shortcut, unchanged | That row's name uses those keys; press different ones |
| A pick that breaks one of its row's other [rules](#a-new-shortcut-is-checked-at-once) | The row's shortcut, unchanged | Which rule it breaks |
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

The section keeps the folder of this device's git clone, through [Sync's setup contract](../../../src/Shared/Sync/Setup/ISyncSetup.cs).

Proposed, each a decision to confirm, so the section reads at a glance and fits the window:

- **Three cards, each with its title, small and bold, at its top, in order: Repository, Git Access, and Auto.** Repository holds the help line, the three lines, the sync status, the Configure button, and the clone controls. Git Access holds the device name, the key buttons, and the host key. Auto holds the settings that act on their own; today, only the deletion delay.
- **The sync status sits in the Repository card, right after the three lines.** Its first line starts with a Sync button, followed by "Last successful sync:" and the time, or "Not synced yet."; the other status lines and the status message follow under it, as [below](#configure-shows-how-sync-is-doing) says.
- **In Git Access, labels share one column on the left, and their controls sit beside them**: Device name beside its box, and SSH key beside its two buttons in one row.
- **Auto lists its settings as bulleted items.** The deletion delay is one item that reads as one sentence: a bullet, "Delete resolved tasks after", a narrow days box, then "days", with its message under it.
- **Buttons that act together sit in one row**: Generate key and Copy public key; Accept host key and Forget host key, under the host key's text.
- **Copy public key copies the key, then opens the host's page for adding it**, and its tooltip says so. With no key yet, it says "Generate a key first." and opens nothing. When copying fails, it shows the error and opens no page, since the page is no use without the key. When the host has no known add-key page, the key is still copied, and the message says to add it in the host's own settings. When the page can't open, the message shows the error; the key stays copied.

- **The Repository card shows three lines, each starting with its name in bold**: Repository, the clone's `origin` address; Local Path, the clone's folder; and Identity, the device name, which Sync puts on each commit it makes ([Sync through git](../sync.md#device-metadata-has-one-owner-and-never-travels-in-task-commits)). Repository and Local Path show "None" when there's none. A failure to read them shows as the card's message.
- **A Configure button opens a menu with two choices**: Clone a repo, and Use existing repo clone. Each shows only its own controls under the lines, replacing the other's. Clicking Clone or Use this clone hides the controls at once; on success the lines update, and on failure the controls show again with the error. Proposed; a decision to confirm.
- **Clone a repo** uses Sync's device-key SSH transport, into an empty or new folder. A clone failure shows its error, and the current folder stays.
- **Use existing repo clone** asks Sync to validate and select the folder. The currently selected clone remains the starting point; setup never forces a replacement clone.
- **The folder's path is kept on this device**, in `repository-folder.txt` under `%LocalAppData%\Memoria-ToDo`, apart from Velopack's install folder.

The user can change the address and the device name during setup. Generate key and Copy public key use Sync's cached setup state. An unknown custom host's key is trusted only when the user accepts its fingerprint, never implicitly.

After Clone or Use this clone, Configure runs one sync and waits for it, so a read-only key fails here. An offline or failed first sync keeps the selected clone for a retry and local work, but doesn't mark setup complete.

### Configure shows how sync is doing

Configure shows the last successful sync time, the files sync skipped, and, while sync fails, the error's details; the tray shows the same in short, as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) says. Configure reads them from Sync. Its Sync button starts a sync at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.

Available Offline details stay visible inline, including before the first success: "Not synced yet" does not
replace the reason. Shell's quiet-offline policy does not hide these details.
Setup and status presentation subscriptions belong to each open window and detach when it closes. Closing
cancels and observes that window's waits, not the shared sync. These subscriptions are separate from the
[settings/shortcut lifetime watcher](#shared-settings-refresh-even-while-configure-is-closed).

### Shared settings refresh even while Configure is closed

Configure receives [`ISharedTaskSettings`](../../../src/Shared/Tasks/ISharedTaskSettings.cs), not another settings
store. It loads the two global shortcut texts when Shell connects their actions, and the Close window text when it's
created, and keeps watching imported changes while
the window is closed, so every shortcut updates without opening Configure. Null uses the existing defaults.
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

- **[Create Configure](../../../src/Desktop/Configure/ConfigureWindow.cs)**: App supplies Start with Windows, typed Tasks settings, the shared Sync service, and the setup-actions port. The window opens only on Show Configure. App also supplies the platform's [Close window keys](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows). Configure sets them to the stored or default Close window shortcut when it's created and after each change, and attaches its own window to them:

  ```csharp
  public static IConfigureWindow Create(
      IStartWithWindows startWithWindows, ISharedTaskSettings settings, ISyncService sync,
      ISetupActions setupActions, CloseWindowKeys closeWindowKeys);
  ```
- **Show Configure**: opens the window, or brings it forward. Configure also says whether its window is open, so Shell can [ignore shortcuts](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) meanwhile.
- **Setup and status** come directly from Sync; Configure publishes no parallel clone, Git identity, or repository-settings port. The desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show) still governs opening and closing.
- **Connect the shortcuts**: Shell calls it once, when it starts, with one [shortcut action](#public-interface) for Add new task and one for Manage tasks. Configure registers each row's stored shortcut, or its default, at once through its action, then each valid pick on that row, and releases and registers both as [a field's focus](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) comes and goes, always on the UI thread. It returns whether both shortcuts registered, so Shell can show Configure when one didn't. When an action fails, Configure treats it as a setting that [can't be applied](#failure-modes), and that shortcut counts as not registered.
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
- **[Setup actions](../../../src/Desktop/Configure/ISetupActions.cs)** copies the displayed public key and opens Sync's supplied add-key page. Copy public key calls both, in that order, as [above](#the-sync-settings-section-sets-the-devices-clone) says; the port itself doesn't change. App adapts its existing clipboard and browser components; Configure never reaches another window module's port. Failures are shown under setup.

The composition root implements these ports. Their real adapters have integration tests; Configure's UX tests use
the shared fakes.

## Failure modes

- **A shortcut is taken**: Configure says why under its field, and the previous shortcut stays active. When a row's shortcut is taken as the app starts, that row has none: Shell opens Configure, which says why, and the tray menu still works.
- **A key Windows or another app takes never reaches the field**: Configure guesses from the modifier keys alone and says the shortcut is probably taken.
- **A shortcut can't register again when the focus leaves the fields**: its row has no shortcut, and its message says why.
- **A shortcut can't be released when a field gets the focus**: it stays active, and its row shows the error.
- **A shortcut breaks the rules**, such as a global shortcut with a single modifier key, F12, or another row's keys: Configure says why under the field, and the active shortcut stays.
- **A setting can't be applied or saved**, such as a failed write to the `Run` key, a switch in a copy that isn't installed, or a shortcut that fails for a reason other than being taken: Configure shows the error and keeps the previous value.
- **Installing can't write the `Run` key**: the app still installs, since Velopack's install step can't stop the install ([hooks](https://docs.velopack.io/integrating/hooks)). The app doesn't start at sign-in, and Configure shows the setting off.
- **Setting up sync fails**, such as when the host rejects the key or the repository isn't found: Configure shows the error, as [Sync through git](../sync.md#failure-modes) lists, and the device stays unconfigured until setup succeeds.
- **Copy public key can't copy, or can't open the host's page**: the message under Git Access shows the error. A failed copy opens no page; a failed page leaves the key copied.

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
  - **A fixed width with a height that follows the open section**: the window jumped in size whenever the user opened another section.
  - **All sections collapsed at first, and the open one collapsible**: the window showed nothing but headings.
  - **Sections the user expands and collapses with an arrow**: the user wanted titles that only open, with no arrow.
  - **The sync status in a group, or a card, of its own**: the user wanted the Sync button beside the last sync time, on three cards.
- Adding the device's key at the host:
  - **An Add key at host button beside Copy public key**: the user always copies the key to add it, so one button does both.
- Entering a shortcut:
  - **Check boxes for Ctrl, Alt, Shift, and Win, and a list of keys**: slower, and looks dated.
  - **Typing it, such as `Win+Alt+N`**: typing mistakes, and a format to explain.
- Which shortcuts Configure accepts:
  - **One modifier key is enough, except F12**: a user could take Ctrl+C or Alt+F from every app by mistake.
  - **Anything Windows accepts**: the same, and Shift+T would stop typing a capital T.
- The Close window shortcut:
  - **Fixed at Shift+Esc, shown read-only**: no storage or rules, but the user couldn't move it off a key another tool uses.
  - **Kept in a file on each device, through a new Configure port**: no shared format change, but it breaks the rule that every device uses the same shortcuts, and adds a port, an adapter, and an integration test.
  - **One or more modifier keys plus any key**: one wrong pick, such as Shift+A, would break typing in every window.
  - **Only Configure closes with it**: Add and View Tasks wouldn't get it.
  - **Shell passes the keys to each window's module**: three modules would each detect the keys, duplicating the handling.
- Esc in a shortcut field:
  - **Esc always closes the window**: a user who clicked a box and changed their mind would lose the window.
  - **Esc only keeps the shortcut, as before**: the user must click away before Esc closes.
- Where the hand pointer shows:
  - **On every button in the app, through one platform style**: it changes Add and View Tasks, which nobody asked for.

The other settings Configure shows were chosen elsewhere, with their rejected options: the default shortcuts in [Shell](shell.md#ruled-out), the deletion delay in [purpose and scope](../purpose.md#ruled-out), and the sync settings in [Sync through git](../sync.md#ruled-out).
