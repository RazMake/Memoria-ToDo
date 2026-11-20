# Changelog

What changed in each version of Memoria-ToDo, for the people who use it.

<!-- How to write entries, and when: docs/contributing/changelog.md -->

## Unreleased

- The app now keeps a log of what it does, such as syncs, added, changed, and deleted tasks and contacts, and every error, with the time to the millisecond. When the app says files can't be loaded, the log names each file and why. The log stays on your computer, never holds passwords or keys, and cleans up after itself: entries older than two weeks are deleted, and it never grows past about 30 MB.
- Logs, a new item in the tray menu, opens a window that lists the log's entries. Filter them by time range, by the part of the app that wrote them, and by how serious they are, and type in the search box to find entries whose text contains what you typed, in any letter case. Press F5 or Refresh to load new entries.
- When something goes wrong while the app is running, it now shows the error and keeps running, instead of closing.
- Typing "@" in a task's title or description lists your contacts; pick one to mention them, and press Tab to switch how the mention is written. A mention shows as one piece with the contact's current details, in the editor and in Task View, and updates when you change the contact.
- Contacts window, opened with Win+Alt+O (you can change this shortcut in Configure) or from the new Contacts item in the tray menu. Both appear only when your repository has contact files. The window lists your contacts by group, searches as you type, and shows a contact's details. You can edit a contact, add one to a group, delete one, and drag a group's heading to reorder the groups. A line above the list tells you about contact files the app skipped.
- Sync now carries your contact files between devices. When two devices change the same contact, both changes are kept field by field. A contact file the app can't read is left as it is and listed in the sync status.
- A task file with damaged text, such as a broken character escape, no longer stops all your tasks from loading. That file is skipped and listed in the sync status, like other files the app can't read, and the rest of your tasks load.
- In the status bar at the bottom of Add and Task View, clicking "Update available" now opens What is new instead of starting the update. To update, use Update Now in Configure. A small gap now separates "Update available" from the version.
- Fixed: in Task View, the window that lists your tasks, the Close window shortcut now asks before dropping an edit you changed, as the close button does.

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
