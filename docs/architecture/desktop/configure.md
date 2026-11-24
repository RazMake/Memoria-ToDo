# Configure: where the desktop app's settings live

Status: Decided

Configure is the desktop app's settings window. It changes the four global shortcuts, the shortcuts that close the app's windows and save an edit, starting with Windows, and the deletion delay, and it sets up sync and shows its status, which Sync owns. Its Application section opens with a card that shows the app's version and updates, which [Updates](updates.md) owns.

## Behavior

### Each setting has one owner

The table lists every setting in Configure, the module that owns it, where it's kept, and its default:

| Setting | Owner | Kept | Default |
| --- | --- | --- | --- |
| Global shortcuts for Add new task, Task View, Configure, and Contacts | Tasks stores the values; Configure checks them | In the repository's `settings.json`, shared by every device | As [Shell](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) decides |
| The Close window shortcut, which closes the app's windows, as [below](#esc-and-the-close-window-shortcut-close-the-window) | Tasks stores the value; Configure checks it | In the repository's `settings.json`, shared by every device, like the global shortcuts | **Shift+Esc** |
| The Save shortcut, which saves the task being edited in Add and Task View, as [below](#the-save-shortcut-saves-an-edit-in-any-window) | Tasks stores the value; Configure checks it | In the repository's `settings.json`, shared by every device, like the Close window shortcut | **Ctrl+Enter** |
| Start with Windows | Configure | In the current user's `Run` registry key, on this device, as [below](#start-with-windows-is-the-run-key-itself) | On |
| Days before a resolved task is deleted | Tasks | In the repository, shared by every device | One week, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides |
| The repository's SSH address, the device name, the device's SSH key, and the accepted host keys | Sync | On this device | The device name starts as the machine's name; the rest is set up as [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) says |
| Auto update, on the [Update card](#the-update-card-leads-the-application-section) | Updates | In a file on this device, as [Updates](updates.md#auto-update-updates-each-night-once-no-window-is-open) proposes | Off, proposed |

**Every device uses the same shortcuts.** Configure saves them through Tasks in [`settings.json`](../tasks.md#the-repository-holds-task-files-image-folders-settings-and-tag-colors) and reads them there when the app starts. Another app may hold a shortcut on one PC only, yet picking a new one there changes it on every PC. This is an accepted risk, for the same shortcuts everywhere.

Tasks keeps the shortcuts as shared text settings without interpreting them. Windows Configure checks their values against its [shortcut rules](#a-new-shortcut-is-checked-at-once). Android preserves them through merges but neither displays nor uses them.

Configure reads and changes the deletion delay through Tasks, so a change on one device reaches every device; [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) sets its allowed range. Configure sets up sync through Sync, in the steps that [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) lists.

**Configure checks each value as it's entered, and applies or saves nothing invalid.** Each setting shows its own problems under it, such as a taken shortcut or a failed write.

Proposed: the deletion delay shows in days. This is a decision to confirm.

The user asked for a fourth global shortcut, [Contacts](contact-list.md#a-global-shortcut-and-the-tray-menu-open-contacts), default **Win+Alt+O**, changed here like the others. It's one more global shortcut in the table's first row, kept in `settings.json` as [Tasks' `contactsShortcut`](../tasks.md#one-versioned-file-holds-the-three-shared-settings), checked by the same rules, and Connect the shortcuts takes its action, as [below](#public-interface). Proposed, a decision to confirm: its row in the Shortcuts card comes after Task View's, with the description "Opens Contacts from any app." The row always shows; [Shell](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) holds the keys in Windows only while the clone has contact files, so Configure doesn't know about Contacts.

### The window shows its settings in labeled sections

The window shows each shortcut with the name of what it does, Add new task, Task View, Configure, Close window, and Save, and it's wide enough to read at a glance.

The user decided the rules on sections below: only a click on another section's title closes the open one, sections have rounded corners and no expand or collapse arrow, an open section's content starts at its top rather than centered in the window's height, titles look like heading 1, and titles show the hand pointer. The user also decided that the section once called Shortcuts is named Application and holds two cards, Update and then Shortcuts. The rest are proposed, each a decision to confirm:

- **Three sections, in order: Application, Startup, and Sync settings, and exactly one is always open.** The window opens with Application open. Clicking a closed section's title opens it and closes the open one; clicking the open section's title does nothing. No section can be closed on its own, so titles show no expand or collapse arrow.
- **Application shows two cards, in order: Update and Shortcuts.** Update is the [Update card](#the-update-card-leads-the-application-section). Shortcuts holds the line on how to change a shortcut and every shortcut row, below.
- **A title works from the keyboard too**: Tab reaches it, and Enter or Space opens its section.
- **Each section is a box with rounded corners**, and so is each card inside Application and [Sync settings](#the-sync-settings-section-sets-the-devices-clone). Their colors and corners come from the theme, and a card stands out from the section around it. One Configure part makes every card, so the Update card and the Sync settings cards look alike.
- **Everything in a section starts at its top.** The open section's content starts right under its title, and space the content doesn't need stays at the section's bottom. Inside a section or a card, each label lines up with the top of the control beside it, not with its middle, and nothing is centered in the height around it.
- **Section titles look like a description's heading 1**: in heading 1's color for the theme, bold, at heading 1's size, all from Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme). They follow the theme when Windows switches between light and dark.
- **Card titles look like a description's heading 2**, the user decided: in heading 2's color for the theme, bold, at heading 2's size, from the same text look, and they follow the theme the same way. One Configure part applies a heading level's look to a title and follows the theme, and both section and card titles use it, so neither holds a color or size of its own.
- **Section titles are buttons, so they show the hand pointer**, as [every button in the app](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) does.
- **The Shortcuts card starts with a line saying how to change one**, in the window's normal text size and color and ending with a colon: "Click a box, then press the new shortcut. Esc keeps the current one:". Then each shortcut has a row, Add new task first, then Task View, Configure, Close window, and Save: its name on the left and its field on the right. Under the field, a short description says what the shortcut does, in the help style and in italics, then the row's message. The names share one column, so the fields line up. The user decided the Configure row's name and place.
- **The descriptions are**: Add new task, "Opens the Add window from any app."; Task View, "Opens Task View from any app."; Configure, "Opens Configure from any app."; Close window, "Closes the app's window you're in, whatever has the focus."; Save, "Saves the task you're editing, in Add and Task View."
- **Startup holds the Start with Windows check box**, a line of help under it, and its message.
- **Sync settings shows its settings on three cards**: Git Access, Repository, and Auto, as [below](#the-sync-settings-section-sets-the-devices-clone) says.
- **Text styles and colors come from the app's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it)**, so the window follows light and dark mode: messages in its error color, and help lines small, in its secondary text color, at 70% opacity.
- **The window keeps its size.** It's 800 pixels wide and 850 tall, or as tall as the screen allows on a shorter screen, and neither the user, the open section, nor its messages change that. The titles of the sections below the open one stay at the window's bottom, and the open section fills the space between; it scrolls when its content is taller. Messages take no room while empty.
- **The window opens with no field focused**, so the shortcuts keep working until the user clicks a field, as [below](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) explains.
- **Esc and the Close window shortcut close the window**, as [below](#esc-and-the-close-window-shortcut-close-the-window) says.

### The Update card leads the Application section

The Update card shows the app's version and whether a newer one is available, and holds the update controls. The user decided it's the first card in the [Application section](#the-window-shows-its-settings-in-labeled-sections), above Shortcuts, and no longer sits above the sections. [Updates](updates.md#updates-tells-you-about-a-new-version-and-installs-it-only-when-you-agree) owns the state the card shows and what each control does; Configure only shows them. Proposed, each a decision to confirm:

- **Its title is "Update", in [heading 2's look](#the-window-shows-its-settings-in-labeled-sections)**, like every card's.
- **It shows while Application is open**, which it is each time the window opens. Opening Startup or Sync settings hides it, as it hides Shortcuts; the status bar in Add and Task View still shows an available version.
- **It looks like the other cards**: the same box, background, and edge as the Sync settings cards.
- **While a version is available, the card has a highlighted background** instead, the theme's caution background, which goes with the status bar's [caution color](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar). So it draws the eye only when there's something to do.
- **It shows, in order**:
  - "Current version: 2.1.0".
  - While a version is available, "Available new version: **2.2.0**"; while updating, "Updating to: **2.2.0**" instead. With no update, there's no second line.
  - While a version is available or updating, What is new, which opens [the notes](updates.md#what-is-new-shows-each-newer-releases-notes), and Update Now, which starts the [update](updates.md#an-update-downloads-the-version-then-restarts-the-app-into-it). Update Now is hidden while Auto update is on, and turned off while updating.
  - The Auto update check box, with the help line "Checks for a new version each night around 3 AM, and installs it." It's turned off while updating.
  - A message in the error color, when an update or Auto update failed, as [Updates](updates.md#failure-modes) words it.
- **A copy run from a build folder** shows its version, the help line "Updates work only in an installed copy.", and Auto update off and turned off, as [Updates](updates.md#a-copy-run-from-a-build-folder-shows-its-version-and-doesnt-update) proposes.
- **The card follows the state as it changes**, while the window is open.

### Esc and the Close window shortcut close the window

Esc closes Configure, and the Close window shortcut closes any of the app's windows, Configure included. Both close a window exactly as its close button does, so nothing is saved or lost that the close button wouldn't save or lose.

- **Esc closes Configure unless a control uses it first.** An open menu, such as the Configure button's, closes first. In a shortcut field, Esc keeps the current shortcut and moves the focus out of the fields, so the shortcuts register again; a second Esc closes the window. Add [closes an open list first](task-editor.md#keys-save-close-and-move-between-fields) the same way.
- **Esc closes through the platform's [Esc part](README.md#window-modules-read-keys-through-the-platform)**, as Task View does. Anything typed and not yet applied, such as a repository address, is dropped, and a running sync goes on, as [below](#configure-shows-how-sync-is-doing) says.
- **The Close window shortcut closes the window whatever has the focus in it**: a shortcut field, a text box, or an open menu. It acts before any control sees the keys, so it also wins over a key a window uses for something else.
- **It never registers with Windows.** It works only while one of the app's windows has the focus, so it takes no keys from other apps.
- **It closes Add, Task View, and Configure**, through one [platform part](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows), so the key handling has one home.

Not confirmed by a run: that the shortcut reaches the window while the Configure button's menu is open, since a WPF menu takes the keys in a window of its own.

### The Save shortcut saves an edit in any window

The user decided that the key that saves an edit, Ctrl+Enter at first, can be changed, and that it's a shared setting kept beside Close window's: a row in the Shortcuts card, "Save", kept by Tasks in [`settings.json`](../tasks.md#one-versioned-file-holds-the-three-shared-settings), so every device uses the same keys.

- **It saves in Add, and in Task View's [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor) and [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row)**, through one [platform part](README.md#the-save-shortcut-saves-in-any-window-that-edits), Save keys, so the key handling has one home. Configure sets it; the Task editor and the Task list read it.
- **It never registers with Windows.** It works only while one of the app's windows has the focus, so it takes no keys from other apps, and it has nothing to release while a shortcut field has the focus.
- **A missing or invalid stored value uses Ctrl+Enter**, or the previous valid keys, as for [Close window](#shared-settings-refresh-even-while-configure-is-closed).
- **A change applies at once**, to an edit already open too.

### A new shortcut is checked at once

The user clicks a shortcut's field and presses the new keys. The field shows them, and Configure checks them at once; Esc leaves the shortcut as it was. Configure accepts a global shortcut, Add new task's, Task View's, Configure's, or Contacts', only if it follows all three rules:

- **Two or more of Ctrl, Alt, Shift, and Win, plus one other key.** A shortcut with one modifier key can take a common key from every app: Ctrl+C would stop copying everywhere.
- **Never F12**, since Windows keeps it for debuggers ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).
- **Different from the other shortcuts**, Close window's and Save's included. Two functions can't share keys: Windows would refuse whichever global shortcut registered second, and the message would wrongly blame another app; a global shortcut takes its keys before any window sees them, so Close window would never work. A pick equal to the row's own shortcut changes nothing: Configure doesn't register it again, and the row's message stays as it was.

**Close window has rules of its own**, since it never registers with Windows and works only inside the app's windows: two or more modifier keys plus any other key, F12 included, or one modifier key plus Esc or a function key, such as Shift+Esc; and different from the other five shortcuts. Configure checks it the moment it's picked, saves it as it saves the others, and hands it to the app's windows at once. Invalid stored text is handled as for the global shortcuts [below](#shared-settings-refresh-even-while-configure-is-closed): the previous valid keys, or the default, stay in use. Configure can't know which keys the other windows use, so Close window wins over them; this is an accepted risk.

**Save has rules of its own too**, since it never registers with Windows either: one or more modifier keys plus any other key, but never Shift alone with a key that types a character, such as Shift+A, which would stop typing a capital A; and different from the other five shortcuts. The user decided these rules. Configure checks it, saves it, and hands it to the app's windows as it does Close window. Save wins over any other use of its keys in an editor, as Close window does; this is an accepted risk. The one exception is Ctrl+F in Task View, which the user decided always [moves to the search box](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete), even when it's the Save shortcut.

As the app starts, Configure registers each row's stored shortcut through its [shortcut action](#public-interface), or the row's default when none is stored or the stored text is [invalid](#shared-settings-refresh-even-while-configure-is-closed), then each valid shortcut the user picks on that row. If Windows or another app holds a shortcut, Configure [says why](#configure-explains-every-shortcut-it-cant-use) and lets the user pick another, and the previous shortcut stays active, as [Shell](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) decides.

Proposed, each a decision to confirm:

- The field always shows its row's active shortcut, or nothing when the row has none, and the message under it says why.
- The field shows modifier keys in the order Win, Ctrl, Alt, Shift.
- Plain Tab moves on from the field instead of being picked, so the field doesn't trap the keyboard.
- Plain Esc keeps the current shortcut, but Esc pressed with a modifier key is a pick, so Shift+Esc can be chosen for Close window. A global pick such as Ctrl+Shift+Esc follows the global rules; Windows holds that one, so it gets the taken message.
- Holding a key picks it once: the field ignores the key's auto-repeat.
- Only modifier keys pressed while the field has focus count. Holding Ctrl+Shift, clicking into the field, then pressing K is rejected for too few modifier keys, so the user presses the keys again in the field. Counting keys held before the click would need the keyboard's real state, which only a real-input test with the window in the foreground can check.
- A pick that breaks both of the first two rules, such as Ctrl+F12, gets the F12 message.

### While a shortcut field has focus, the app's shortcuts are released

While any shortcut field has the keyboard focus, Configure unregisters all four global shortcuts, so their keys reach the field instead of opening a window. The user can press a shortcut's own keys again, or another shortcut's keys and get the [message](#configure-explains-every-shortcut-it-cant-use) that it's in use. The Close window and Save shortcuts are the exception: they have nothing to release, and pressing Close window's current keys in any field closes the window, as [above](#esc-and-the-close-window-shortcut-close-the-window) says. The user picks new keys for it by pressing different ones.
- **When no shortcut field has the focus, Configure registers each row's shortcut again.** That happens when the user tabs or clicks away, another app takes the focus, or the window closes, also in the middle of a pick. Moving from one field to another keeps them all released.
- **A valid pick registers at once**, which checks that it's free, then is released again until the focus leaves the fields.
- **A shortcut that can't register again**, such as when another app took it meanwhile, leaves its row with no shortcut, and the row's message says why.
- **A shortcut that can't be released when a field gets the focus** stays active, its row's message shows the error, and Configure still releases the others. Proposed; a decision to confirm.

Proposed: Configure releases every global shortcut, not only the row's own, since pressing another's keys would open its window and take the focus from the field. This is a decision to confirm.

Proposed, a decision to confirm: **each time Configure is shown, it registers each global row's shortcut again through its action**, as it does when the focus leaves the fields, unless a shortcut field has the focus. An action that still holds the shortcut answers true at once, so this changes nothing then. It lets a row learn that its shortcut was lost while Configure was closed, as [Shell's](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) Contacts action does when contacts arrive and the shortcut is taken: the row then shows no shortcut and the taken message.

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

- **The entry starts the app through Velopack's starter**, the program Velopack keeps in the install folder, whose path survives updates ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). It adds the argument that marks a [start at sign-in](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view).
- **Installing turns it on, and uninstalling removes it.** Velopack runs the app during install and uninstall for such steps, which must be fast and show no window ([hooks](https://docs.velopack.io/integrating/hooks)). The install step writes the entry once, and updates leave it alone, so no start turns back on what the user turned off. A step that can't write or remove the entry goes on quietly, since a step can't stop the install or uninstall: after a failed install step Configure shows the setting off, and after a failed uninstall step the entry stays. Proposed: the step writes its error to [Velopack's log](https://docs.velopack.io/troubleshooting/debugging), since a step shows no window. This is a decision to confirm.
- **Configure shows and switches it through a port Configure publishes.** The check box shows whether the entry is there when the window opens, and a switch writes or removes the entry at once. The composition root implements the port, and its Velopack install and uninstall steps use the same code.
- **Only an installed copy can switch it.** A copy run from a build folder shows the setting, but switching it fails, since the entry must name the installed starter.

The entry is named with the [app's name](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases), and its value is the starter's quoted path followed by the [sign-in argument](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view). Changing the app's name or the argument after the first release would leave existing entries out of date.

Not confirmed by a run: Windows' [Startup apps page](https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows) seems to keep its own on and off list beside the key, so Configure could show on while Windows keeps the app off.

### The Sync settings section sets the device's clone

The section keeps the folder of this device's git clone, through [Sync's setup contract](../../../src/Shared/Sync/Setup/ISyncSetup.cs).

The user decided the cards' order: Git Access, Repository, and Auto. The rest are proposed, each a decision to confirm, so the section reads at a glance and fits the window:

- **Three cards, each with its title, in [heading 2's look](#the-window-shows-its-settings-in-labeled-sections), at its top, in order: Git Access, Repository, and Auto.** Git Access holds the device name, the [key buttons](#git-access-makes-copies-and-forgets-the-devices-key), and the [Accept host key button](#git-access-accepts-a-custom-hosts-new-or-changed-key). Repository holds the help line, the three lines, the sync status, the Configure button, and the clone controls. Auto holds the settings that act on their own; today, only the deletion delay.
- **The sync status sits in the Repository card, right after the three lines.** Its first line starts with a Sync button, followed by "Last successful sync:" and the time, or "Not synced yet."; the other status lines and the status message follow under it, as [below](#configure-shows-how-sync-is-doing) says.
- **In Git Access, labels share one column on the left, and their controls sit beside them**: Device name beside its box, and SSH key beside its buttons in one row.
- **Auto lists its settings as bulleted items.** The deletion delay is one item that reads as one sentence: a bullet, "Delete resolved tasks after", a narrow days box, then "days", with its message under it.
- **Buttons that act together sit in one row**: the SSH key's buttons, and Set key before Clone.

- **The Repository card shows three lines, each starting with its name in bold**: Repository, the clone's `origin` address; Local Path, the clone's folder; and Identity, the device name, which Sync puts on each commit it makes ([Sync through git](../sync.md#device-metadata-has-one-owner-and-never-travels-in-task-commits)). Repository and Local Path show "None" when there's none. A failure to read them shows as the card's message.
- **A Configure button opens a menu with two choices**: Clone a repo, and Use existing repo clone. Each shows only its own controls under the lines, replacing the other's. Clicking Clone or Use this clone hides the controls at once; on success the lines update, and on failure the controls show again with the error. Proposed; a decision to confirm.
- **Clone a repo** uses Sync's device-key SSH transport, into an empty or new folder. A clone failure shows its error, and the current folder stays.
- **Use existing repo clone** asks Sync to validate and select the folder. The currently selected clone remains the starting point; setup never forces a replacement clone.
- **The folder's path is kept on this device**, in `repository-folder.txt` under `%LocalAppData%\Memoria-ToDo`, apart from Velopack's install folder.

The user can change the address and the device name during setup. The key buttons use Sync's cached setup state.

After Clone or Use this clone, Configure runs one sync and waits for it, so a read-only key fails here. An offline or failed first sync keeps the selected clone for a retry and local work, but doesn't mark setup complete.

### Git Access makes, copies, and forgets the device's key

The repository's host lets this device sync only after the user adds the device's public key there, as [Sync through git](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) says. The user decided what each key button does:

- **Generate key**, while the device has no key, makes the key, then copies its public key.
- **Copy public key**, while the device has a key, only copies the public key.
- **Forget key**, while the device has a key, deletes the device's key once the user confirms the risk. It never changes host trust.
- **Set key** sits before Clone, in the Repository card. It's on only while the address box holds an address Sync accepts. It copies the public key, then opens the add-key page of that address's host, whatever clone is selected or saved.

The user approved Sync-owned deletion, a shared themed question dialog, and a Sync setup API for checking the typed address. Proposed, each a decision to confirm:

- **The SSH key row shows only the buttons that apply**: Generate key with no key, and Copy public key and Forget key with one.
- **Set key with no key asks the user to generate one first**, without opening a page. Generating remains an explicit action.
- **Set key sits before Clone only**, as requested.
- **On a host with no known add-key page**, Set key still copies the key, and its message says to add it in the host's own settings.
- **Each key button's tooltip says what it does**; Set key's says it also opens the host's page.
- **Each message shows in its button's card**: Generate key's, Copy public key's, and Forget key's under Git Access, and Set key's under Repository.
- **A dialog asks before Forget key deletes anything.** Its title is "Forget key", and its text is "Delete this device's SSH key? Sync stops until you generate a new key and add it at your repository's host. The old key stays listed at the host until you remove it there." Its buttons are Forget key and Cancel. Cancel is the default, so Enter picks it; Esc and the close button pick it too. It's modal, owned by Configure, centered on it, and follows the theme, like Task View's [close prompt](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit).
- **After Forget key, the SSH key row shows Generate key**, and the device isn't configured until a new key completes a first sync. Scheduled syncs don't run, as on a device that never had a key, and a sync the user starts fails with "The device has no key yet." ([SyncEngine.cs](../../../src/Shared/Sync/Service/SyncEngine.cs)).
- **Forget key shows only while the public key shows.** A key that's stored but can't be opened, such as one Windows can no longer decrypt, shows no public key, so Forget key can't remove it. That would need setup to report a stored key it can't open, which this change doesn't add.

### Git Access accepts a custom host's new or changed key

**Decided by the user:** Accept host key is the only host-trust control, in Git Access. It shows while Sync offers a key it lets the user accept ([`ISshHostKey.CanAccept`](../../../src/Shared/Sync/Setup/ISshHostKey.cs)): a custom host's key that is unknown, or that differs from the key saved for that host and port. Accept trusts that exact offered key, replacing any saved one, as [Sync through git](../sync.md#device-metadata-has-one-owner-and-never-travels-in-task-commits) decides. When Accept succeeds, the button hides. It never shows for a built-in host, for a key already trusted, or while the trust file can't be loaded.

There is no Forget host key button: Accept replaces a changed key, so trust never has to be removed first. Forget key deletes only the device's own key.

The user also decided to remove the host, port, algorithm, and fingerprint string in every state. Sync's host verification and built-in pins don't change.

### Configure shows how sync is doing

Configure shows the last successful sync time, the files sync skipped, and, while sync fails, the error's details; the tray shows the same in short, as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) says. Configure reads them from Sync. Its Sync button starts a sync at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.

Available Offline details stay visible inline, including before the first success: "Not synced yet" does not
replace the reason. Shell's quiet-offline policy does not hide these details.
Setup and status presentation subscriptions belong to each open window and detach when it closes. Closing
cancels and observes that window's waits, not the shared sync. These subscriptions are separate from the
[settings/shortcut lifetime watcher](#shared-settings-refresh-even-while-configure-is-closed).

### Shared settings refresh even while Configure is closed

Configure receives [`ISharedTaskSettings`](../../../src/Shared/Tasks/ISharedTaskSettings.cs), not another settings
store. It loads the four global shortcut texts when Shell connects their actions, and the Close window and Save texts when it's
created, and keeps watching imported changes while
the window is closed, so every shortcut updates without opening Configure. Null uses the existing defaults.
The shortcut text form, such as `Win+Alt+N` or `Ctrl+Enter`, is written by the platform's [`ShortcutText`](README.md#the-save-shortcut-saves-in-any-window-that-edits); one internal parser reverses it, and also reads "Return" as "Enter".
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

- **[Create Configure](../../../src/Desktop/Configure/ConfigureWindow.cs)**: App supplies Start with Windows, typed Tasks settings, the shared Sync service, and the setup-actions port. The window opens only on Show Configure. App also supplies the platform's [Close window keys](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows). Configure sets them to the stored or default Close window shortcut when it's created and after each change, and attaches its own window to them. The user decided that App also supplies the platform's [Save keys](README.md#the-save-shortcut-saves-in-any-window-that-edits), which Configure sets the same way from the stored or default Save shortcut. App also supplies the platform's theme, so section and card titles take [heading 1's and heading 2's](#the-window-shows-its-settings-in-labeled-sections) colors for the theme the windows show; the user decided that Configure may use Rendering for that look:

  ```csharp
  public static IConfigureWindow Create(
      IStartWithWindows startWithWindows, ISharedTaskSettings settings, ISyncService sync,
      ISetupActions setupActions, CloseWindowKeys closeWindowKeys, ITheme theme, IUpdateStatus updates,
      SaveKeys saveKeys);
  ```

  The user decided that Configure may use [Updates](updates.md), for the [Update card](#the-update-card-leads-the-application-section). Proposed: `Create` takes Updates' [update status](updates.md#public-interface), `IUpdateStatus updates`, after `theme`, and the Save keys come last, after it. This is a decision to confirm.
- **Show Configure**: opens the window, or brings it forward. Configure also says whether its window is open, so Shell can [ignore shortcuts](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) meanwhile.
- **Close Configure**: closes the window when it's open, and does nothing otherwise, so Shell can close it when the [Logs window](logs.md#the-tray-menus-logs-item-opens-one-window) opens, as the user decided. It closes as Esc does.
- **Setup and status** come directly from Sync; Configure publishes no parallel clone, Git identity, or repository-settings port. The desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show) still governs opening and closing.
- **Connect the shortcuts**: Shell calls it once, when it starts, with one [shortcut action](#public-interface) for each global shortcut: Add new task, Task View, Configure, and Contacts. Configure registers each row's stored shortcut, or its default, at once through its action, then each valid pick on that row, and releases and registers them all as [a field's focus](#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released) comes and goes, always on the UI thread. It returns whether every shortcut registered, so Shell can show Configure when one didn't. When an action fails, Configure treats it as a setting that [can't be applied](#failure-modes), and that shortcut counts as not registered. The user decided that Configure's action is the third argument, after Task View's, and that the Contacts shortcut is changed here like the others, so Contacts' action is the fourth, after Configure's. Step 15's slice (e) built it in place of the three-argument form, with no overload kept. The Contacts row joins the rows every other row's pick must differ from, and the shortcuts a focused field releases:

  ```csharp
  /// Returns true only when all four initial shortcuts registered.
  /// Throws ArgumentNullException when any action is null.
  bool ConnectShortcuts(
      IShortcutAction addNewTask, IShortcutAction manageTasks, IShortcutAction configure, IShortcutAction contacts);
  ```
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
- **[Setup actions](../../../src/Desktop/Configure/ISetupActions.cs)** copies the public key and opens the add-key page Sync gives. Generate key and Copy public key only copy; Set key copies, then opens the page, as [above](#git-access-makes-copies-and-forgets-the-devices-key) says. The port itself doesn't change. App adapts its existing clipboard and browser components; Configure never reaches another window module's port. Failures are shown under setup.

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
- **A key button can't copy, or Set key can't open the host's page**: the message in the button's card shows the error. A failed copy opens no page, and a failed page leaves the key copied. When Generate key made the key but couldn't copy it, the key stays.
- **Forget key can't delete the key**: the message under Git Access shows the error, and the key stays.
- **Accept host key can't save the trust**: the message under Git Access shows the error, and the saved trust doesn't change.

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
  - **The update card above the sections, outside any section**: the user asked to move it into Application, as its first card.
- Adding the device's key at the host:
  - **Copy public key also opens the host's page**: the user chose a Copy public key that only copies, and a Set key beside Clone that opens the page.
  - **Opening the add-key page of the selected clone's host**: the user wants the page of the address being typed, which works before any clone exists.
- Showing the host key:
  - **After every connection, built-in hosts included**: the user removed that line.
- Replacing a custom host's changed key:
  - **A Forget host key button beside Accept**: the user removed it, as [Sync through git](../sync.md#ruled-out) says.
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
  - **Only Configure closes with it**: Add and Task View wouldn't get it.
  - **Shell passes the keys to each window's module**: three modules would each detect the keys, duplicating the handling.
- Where the Save shortcut is kept:
  - **A setting kept on this device only**, in a file under `%LocalAppData%`: no shared format change, but it breaks the rule that every device uses the same shortcuts, and adds a port and its adapter, as for Close window.
  - **Fixed at Ctrl+Enter**: no change at all, but the user asked for a key that can be changed.
- Esc in a shortcut field:
  - **Esc always closes the window**: a user who clicked a box and changed their mind would lose the window.
  - **Esc only keeps the shortcut, as before**: the user must click away before Esc closes.
- Where the hand pointer shows:
  - **Only on Configure's section titles and buttons**: the user wants it on every button in the app.
- How section titles get heading 1's look:
  - **A heading control from the Formatted view**: a module for showing a task would also serve settings titles.
  - **Brushes in the app's resources, under platform keys**: App would update them on every theme switch, and tests could check only the key.
- How Configure's shortcut reaches Shell:
  - **Configure registers its own shortcut**, through a global shortcut port the composition root passes to Create: Configure would repeat Shell's registration code, and Shell's switching rule couldn't cover a press.
- What Configure's shortcut does while another of the app's windows is open:
  - **It always shows Configure, as the tray menu's item does**: it breaks the rule that a shortcut never opens a window beside another.
  - **It follows the switching rule, but brings an open Configure forward**: it would act differently from Task View's shortcut in the same case.
- The Configure row's name and place:
  - **Last, after Save**: it splits the global shortcuts.
  - **"Configuration window"**: it differs from the window's title and the tray menu's item.
- How `settings.json` keeps Configure's shortcut:
  - **Version 2, with the new field**: every older app's settings reads and writes fail until it updates, which Tasks [ruled out](../tasks.md#ruled-out) for tag colors.

The other settings Configure shows were chosen elsewhere, with their rejected options: the default shortcuts in [Shell](shell.md#ruled-out), the deletion delay in [purpose and scope](../purpose.md#ruled-out), and the sync settings in [Sync through git](../sync.md#ruled-out).

## Approved setup contracts

The user approved the recommended choices below. They add no dependency or stored-format change.

### 1. How Sync deletes the device's key

Forget key must delete the key Sync signs in with. Nothing can today: [`ISyncSetup`](../../../src/Shared/Sync/Setup/ISyncSetup.cs) only makes a key, and [`IDeviceKeyStore`](../../../src/Shared/Sync/DeviceKeys/IDeviceKeyStore.cs) has no delete.

- **Recommended: Sync deletes it, as a setup action.** `ISyncSetup` gains `ForgetKeyAsync`, and `IDeviceKeyStore` gains `Delete`, which App implements by deleting `device-key.json`. Like every setup action, it waits for a running sync, then refreshes the setup, so the old public key stops showing. It adds two members, and no type, dependency, or stored format. The [configured receipt](../sync.md#device-metadata-has-one-owner-and-never-travels-in-task-commits) already makes a new key need a first sync.
- **App deletes the file, through Configure's [setup actions](#public-interface) port.** Sync's public interface stays as it is. But Sync remembers a public key once read ([SetupModel.cs](../../../src/Shared/Sync/Setup/SetupModel.cs)), so it would show the deleted key, and count the device as configured, until the app restarts.

The recommended declarations:

```csharp
// In ISyncSetup:
/// Deletes the device key, if any. Host trust and the selected clone don't change.
/// The device isn't configured again until a new key completes a first writable sync.
Task ForgetKeyAsync(CancellationToken cancellationToken);

// In IDeviceKeyStore:
/// Deletes the stored key, whether or not it can be opened. Does nothing when there's none.
void Delete();
```

### 2. Where the Forget key dialog lives

The Forget key dialog does what Task View's [close prompt](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) does: a modal question owned by a window, with a default answer and a cancel answer. That dialog belongs to the Task list ([CloseDialog.cs](../../../src/Desktop/TaskList/CloseDialog.cs)), so a second use must share it, as the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) says. A copy in Configure is ruled out by that rule.

- **Recommended: move it into the desktop [platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) as a question dialog**, used by both Task View's close prompt and Forget key. Code two modules share moves there, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) says, and as the theme follower did. The platform's public interface gains one part, and Task View behaves as before.
- **WPF's [message box](https://learn.microsoft.com/en-us/dotnet/api/system.windows.messagebox), with OK and Cancel, and Cancel the default**: no new code, and the platform's error box already uses one. But it doesn't follow the app's dark theme, and its button says OK, not Forget key.

The recommended declaration:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// A modal question, owned by and centered on a window, that follows the theme.
public static class QuestionDialog
{
    /// Shows the text with one button per answer, in order, and returns the answer picked.
    /// Enter picks defaultAnswer; Esc and the close button pick cancelAnswer.
    public static T Ask<T>(
        System.Windows.Window owner, string title, string text,
        System.Collections.Generic.IReadOnlyList<(string Label, T Answer)> answers, T defaultAnswer, T cancelAnswer);
}
```

### 3. How Configure checks the typed address

Set key needs two answers about the address being typed: whether Sync accepts it, and its host's add-key page. Only Sync's internal [address parser](../../../src/Shared/Sync/Setup/AddressParser.cs) knows both. The setup state gives only the selected clone's page, and copying the parser into Configure is ruled out by the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication).

- **Approved: `ISyncSetup` gains `CheckAddress`**, which gives both answers on the caller's thread, with no I/O, so Configure can call it as the user types. `ISyncSetupState.AddKeyPage` stays for compatibility, but Configure no longer reads it. The fake setup answers the check too; Sync's tests cover the parser.
- **A public static class in Sync, such as `SyncAddress.Check`**: Configure's tests run the real parser. But Configure would depend on a concrete Sync type, which [Code design](../../../AGENTS.md#code-design) rules out.

The recommended declaration:

```csharp
// In ISyncSetup:
/// Whether Clone and Use existing accept this address, checked on the caller's thread with no I/O.
/// When they do, addKeyPage is the host's page for adding the device key, or null when Sync knows none.
bool CheckAddress(string address, out Uri? addKeyPage);
```

## Decided questions

### 1. Where the user sees the offered fingerprint before Accept

**Decided by the user: keep showing nothing.** Git Access shows no host, port, or fingerprint in any state, including while Accept host key shows. The user accepts the risk below.

Accept now replaces a custom host's changed key, which is what an impostor between the device and the host would also cause. [Sync through git](../sync.md#the-app-checks-each-host-key-against-its-own-list) expects the user to compare the new fingerprint with the host's first, but Git Access [shows no fingerprint in any state](#git-access-accepts-a-custom-hosts-new-or-changed-key), and the sync error's details don't name it ([HostKeyCheck.cs](../../../src/Shared/Sync/Trust/HostKeyCheck.cs)).

- **Recommended: show the offered fingerprint, with its host and port, only while Accept host key shows.** The user can compare before trusting, and the card stays clean once trust is settled. It narrows the earlier removal of the details string to the states with nothing to accept. Sync's interface already gives every value ([`ISshHostKey`](../../../src/Shared/Sync/Setup/ISshHostKey.cs)).
- **Put the fingerprint in Accept's tooltip**: no visible change, but easy to miss, and hard to read or copy.
- **Keep showing nothing (chosen)**: no change, but the user accepts a changed key without anything to compare.

Earlier proposed settings remain marked above.

## Open questions

None.
