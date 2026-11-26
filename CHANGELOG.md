# Changelog

What changed in each version of Memoria-ToDo, for the people who use it.

<!-- How to write entries, and when: docs/contributing/changelog.md -->

## Unreleased

- When saving an edit to a task fails, the images you added in that edit are now put back, so they're no longer left in your synced folder: trying to save again adds them, and discarding the edit removes them. If putting them back fails too, the editor says the change may be only partly saved.

- When a change may be only partly saved, Add, the window for adding a task, and Task View, the window that lists your tasks, now say so in the same words, followed by what went wrong. In Task View this now also covers moving, resolving, reopening, or deleting a task, moving a group, and changing a task's tags, which used to show an error box, and the list then shows what was really saved.

## 1.4.0

- Prevent screen saver, a new setting in Configure, keeps the screen on and the PC awake while the app runs: no screen saver, no screen turning off, and no lock for lack of input. It's off at first, and each PC keeps its own choice. When you lock the PC, it pauses until you unlock it, without changing the setting. In Task View, the window that lists your tasks, a sun just left of the version shows whether it's on: lit in amber while it's on, faded and partly transparent while it's off or paused, and in red when it isn't working. Rest the pointer on the sun to read which.

- Task View, the window that lists your tasks, now reopens at the size and place where you last closed it, even after you restart the app or your PC, and reopens maximized if you closed it maximized. Each PC keeps its own place. If that place is no longer on any screen, Task View opens on the screen with the mouse pointer, as before.

- Configure's Update card has a Check for updates button that checks for a new version right away. It says "You have the latest version." when there's nothing new, and shows why in red when the check fails. Update Now now shows whenever a new version is available, even with Auto update on.

- In Configure, the Startup section is now called Behavior. It holds Start with Windows, Prevent screen saver, and the switch that lets AI assistants on this PC use Memoria-ToDo, which moved there from the Application section. An Examples… button next to that switch opens a window with the lines each AI assistant needs to connect, each with a Copy button. It now includes GitHub Copilot CLI.

- In the status bar at the bottom of Add and Task View, "Update available" now sits a little further from the version. What is new, the window that shows a new version's notes, now scrolls when the notes are long, and has an Update Now button next to Close.

- Closing the app no longer leaves it running without a tray icon or keyboard shortcuts when cleanup gets stuck. After 40 seconds it requests exit, then forces itself to end 3 seconds later if needed; unfinished writes or sync may be interrupted. Shutdown errors go to the log instead of waiting for a dialog, and launches during shutdown no longer reopen Task View.

## 1.3.0

- A file you change by hand in your synced folder, such as your list of mention formats, now shows in the app about half a second after you save it, in every window and for AI assistants and your own programs. Before, it showed only after the next sync.

- Mentions now show their format's Markdown as styling: with the format `{fullName} (_{alias}_)`, the alias shows in italic with no underscores, in the line at the top of the "@" list, in the editor, and in Task View, the window that lists your tasks. A contact's name or alias shows exactly as written, so a name like `Ana *QA*` keeps its stars. A mention format with unpaired brackets, a link or image, a trailing backslash, a backslash right before a `{field}`, or a line break is now reported as not valid. The "Tab or click: next template" hint is now fainter and indented.

- The "@" list now starts on the mention format you used last, on every device you sync: picking a contact moves its format to the top of your list of mention formats, and Tab goes through the others in their order. The first time, the app rewrites the list in its own layout, keeping every format. If you edit the list on one device while another moves a format to the top, your edit is kept. Until every device you sync is updated, an older version can still drop such an edit.

- Erasing back into a partly typed mention now reopens the "@" list, as if you had typed it: erasing the "e" of `@ale` lists the contacts for `@al`. This works in the Add window, in Task View, and in Task View's search box.

- A task can now have up to 15 tags. In Add and Task View, each tag shows as a pill; drag a pill to reorder the tags, or move the selected pill with Alt+Left and Alt+Right. The first tag groups the task in Task View, so it shows a little larger than the others. Searching finds a task by any of its tags. Tags are now always lowercase, with a hyphen in place of spaces: "My Work" becomes "my-work", and tags that differed only in case become one. Your existing tasks keep their tags, written this way the next time you change them. AI assistants and your own programs can now set several tags on a task and find the tasks that have all the tags they name. Until every device you sync is updated, older versions show a task with several tags under each of its tags.

## 1.2.0

- The app now keeps a log of what it does, such as syncs, added, changed, and deleted tasks and contacts, and every error, with the time to the millisecond. When the app says files can't be loaded, the log names each file and why. The log stays on your computer, never holds passwords or keys, and cleans up after itself: entries older than two weeks are deleted, and it never grows past about 30 MB.
- Logs, a new item in the tray menu, opens a window that lists the log's entries. Filter them by time range, picking a custom range's start and end with a date and time picker, by the part of the app that wrote them, and by how serious they are, and type in the search box to find entries whose text contains what you typed, in any letter case. Errors show in red and warnings in orange. Press F5 or Refresh to load new entries. Opening Logs closes the app's other windows.
- When something goes wrong while the app is running, it now shows the error and keeps running, instead of closing. Click a sync error notification to open Logs.
- Typing "@" in a task's title or description lists your contacts in a popup right under the cursor, each as the contact's name and alias; pick one to mention them. The line at the top of the popup shows how the mention will be written; press Tab, or click that line, to switch how it's written. A mention shows as one piece with the contact's current details, in the editor and in Task View, and updates when you change the contact. Rest the pointer on a mention to see the contact's card, and hold Ctrl to see all their details. In Task View's search box, typing "@" lists your contacts too; pick one, or type "@" and their alias, to find the tasks that mention them in the title or description.
- Contacts window, opened with Win+Alt+O (you can change this shortcut in Configure) or from the new Contacts item in the tray menu. Both appear only when your repository has contact files. The window lists your contacts by group, searches as you type, and shows a contact's details. You can edit a contact, add one to a group, delete one, and drag a group's heading to reorder the groups. A line above the list tells you about contact files the app skipped.
- A contact can now have a nickname, which you can see and edit in the Contacts window. Searching for a contact, in the Contacts window or in the list of contacts that "@" opens, finds them by nickname too. A mention's format can use {nickname}, which writes the contact's nickname, or their first name when they have none. Until every device you sync is updated, don't add {nickname} to your mention formats: older versions of the app skip the whole list of formats when one of them uses it.
- Sync now carries your contact files between devices. When two devices change the same contact, both changes are kept field by field. A contact file the app can't read is left as it is and listed in the sync status.
- A task file with damaged text, such as a broken character escape, no longer stops all your tasks from loading. That file is skipped and listed in the sync status, like other files the app can't read, and the rest of your tasks load.
- In the status bar at the bottom of Add and Task View, clicking "Update available" now opens What is new instead of starting the update. To update, use Update Now in Configure. A small gap now separates "Update available" from the version.
- Fixed: in Task View, the window that lists your tasks, the Close window shortcut now asks before dropping an edit you changed, as the close button does.
- In the dark theme, windows and dialogs no longer flash white for a moment when they open.
- In Task View, the pointer now turns into a hand only over a task's checkbox, not over its title.
- The Add new task window no longer disappears when you click another window. It stays open behind it, like Task View and the app's other windows, so Alt+Tab or the taskbar brings it back with what you typed.
- In Logs, the Severity list now offers "Debug and up", which also shows the app's detailed entries that explain its choices, such as where a window opened. The log files now keep these entries too.
- A contact card no longer closes immediately when you move the pointer out of the text toward the card. It gives you the same short time to cross as when you leave the mention itself.
- Task titles now stay on one line when you press an Enter combination that is not assigned to Save.
- Fixed: in Task View, the window that lists your tasks, moving a task within its group could give it the same place in the overall order as a task in another group. The list of resolved tasks, which shows them all in one list, could then show the two in an order you didn't choose. A moved task now always gets a place of its own.
- AI assistants on your PC, such as GitHub Copilot in VS Code, Claude Desktop, Claude Code, and Codex, can now use Memoria-ToDo while it runs. An assistant can list, search, add, edit, move, resolve, reopen, and delete tasks, add images to tasks, change tag colors and order, manage contacts, sync, change settings such as Start with Windows and Auto update, and read the app's log with the same filters as the Logs window. Only your own programs on this PC can connect. It can't set up Git access or the repository, update the app, or change shortcuts. An assistant's task never touches the task you're typing in Add. To connect one, open Configure: the new AI assistants card has the text to copy into your assistant, and a check box to turn access off on this PC. See [Connect an AI assistant](docs/user/connect-an-ai-assistant.md).
- In Logs, a custom time range that falls around the hour skipped when clocks move forward in spring, and so ends before it starts, now shows in red. Before, it showed an empty list.
- Your own programs and scripts can now work with the app while it runs. They can list, add, change, and delete tasks and contacts, see the sync status, start a sync, and read and change the tag colors and order. Only programs running under your Windows account on the same computer can connect; nothing is opened to the network. Each release now includes a client library for .NET and one for Node.js, ready to use from your own code.

## 1.1.1

- The app no longer shows a "Cannot access a disposed object" error when the tray icon's sync status changes more than once, such as when sync goes offline or shows a warning and then recovers.

## 1.1.0

- In Configure, Git Access no longer has Forget host key. Accept host key now also replaces the saved key of a custom host when that host's key has changed.
- In Configure, the settings window, Git Access now appears before Repository. Generate key copies the new public key, Copy public key only copies, and Forget key deletes this device's key after confirmation.
- Set key, beside Clone, copies your public key and opens the Git provider's settings for the repository address you entered. The host-key details string is no longer shown.
- In Task View, the window that lists your tasks, you can now drag a tag's group to a new position by its heading, and the order is kept on every device you sync. A line shows where a dragged task or group will land, and the list scrolls while you drag near its top or bottom edge. Until older versions of the app are updated, they show default tag colors after this change.
- Task View now searches as you type; the magnifier button is gone, and the search box is narrower. The cursor starts in the search box: Down there selects the first task, and Up the last. Ctrl+F jumps to the search box from anywhere in the window and selects its text, even from a task's right-click menu or when you made Ctrl+F your Save shortcut.
- In Task View's list, Up on the first task and Down on the last one take you back to the search box. Esc now first clears the search when it holds text, and closes the window on the next press.
- Pressing a window's shortcut while it's open behind other windows now brings it to the front. For Task View, it also clears the search and puts the cursor there.
- Task View's list now takes about two thirds of the window at first. A collapsed group's count reads "( 3 )", and tag names sit centered in their colored tags.
- A task now has at most one tag. In Add, the window for a new task, and in Task View, the window that lists your tasks, the tag shows as one colored pill, or "No tag". Click the pill to open a menu where you pick a tag, search for one, or make a custom one, or click the × beside it to clear the tag. In Task View a tag you choose is saved at once, and an Undo bar says "Tag changed" or "Tag cleared". While you edit a task's title or description, a tag you choose is saved together with the edit, and Esc drops it. Tasks you saved with several tags keep them until you change the tag, and show the first one. If two devices change a task's tag, the later change wins.

## 1.0.0

- The app now tells you when a new version is available. Task View, the window that lists your tasks, and Add, the window for a new task, show it along their bottom edge, and Configure shows it in a card at its top. Click What is new to read what each newer version brings, and Update Now to install it; the app restarts into the new version. Turn on Auto update in Configure and the app checks each night around 3 AM and installs the new version for you, once none of its windows is open.
- The window that lists your tasks is now called Task View: in the tray menu, in its title bar, and in its shortcut's name in Configure, the settings window. It used to be Manage tasks in the menu and View Tasks in the title bar.
- Headings in descriptions have new colors in both themes: orange, yellow, dark yellow, then blue for smaller headings. The card titles in Configure use the same look.
- Task View's status bar is now one short line in its own shade, slightly different from the window's. It has a Sync button, a small blue sync icon, and the app's version at its right end. Point at the button to see "Sync (F5)". While a sync runs, whether you started it or the app did, the button turns green and can't be pressed, and "Syncing…" shows beside it. When a sync fails, the error shows beside the button.
- Task View's search bar now stretches across the whole window, is shorter, and has its own shade like the status bar. The search box has a "Search:" label, uses the free width, and has a magnifier button after it; Enter still searches. The tag filter is gone: click a tag's name above its group to collapse or expand that group instead. Tasks are indented under their tag.
- The task count beside a collapsed group's name is now quieter: it uses the secondary text color, only its number is bold, and it lines up with the middle of the tag.
- Tags are always lowercase and have no spaces.
- In the task editor, the + button now opens a drop-down menu with your 8 most-used tags, a Search box, and Custom. Search lists the tags that contain what you type, anywhere in the tag, in a menu beside it. Custom opens a "New tag:" box below the tags; type a name and 30 colors appear, and a click on a color adds the tag in that color. Enter adds it with no color change. Esc closes the menu first, then Custom, then the window.
- Edit Description now edits only the description. Ctrl+Enter saves, and you can change that shortcut in Configure; Esc drops your changes. A Save button sits at the bottom right, and the Description grows to fill the space above it; the right side no longer scrolls as a whole, unless the window is very short. A Preview tab shows how the description will look, and there you can drag an image's corner to resize it. The size is kept with the task.
- A new Edit Title item in a task's right-click menu edits its title right in the list.
- In Task View, Enter, F2, Shift+F2, Ctrl+D, Delete, Alt+Up and Alt+Down now also work on the selected task after you click its description on the right side. They used to do nothing there.
- Tab no longer stops on the bar between Task View's two sides, or on the list as a whole.
- Configure's Shortcuts section is now called Application. It holds two cards: Update, which shows the app's version and updates and used to sit above the sections, and Shortcuts.
- A new shortcut opens Configure from any app: Win+Alt+C at first. Change it in Configure, beside the other shortcuts. Like those, it closes Add or Task View first if one is open.
- Pressing Ctrl+Tab while you edit a task no longer draws a light outline around Task View's right side or the editor's fields.
