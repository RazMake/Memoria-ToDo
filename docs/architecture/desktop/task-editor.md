# Task editor: how the desktop app adds a task in seconds

Status: Decided

The Task editor is the desktop app's Add window, built so one key press puts the cursor in a new task's title with no wait. View Tasks edits a task in place with the same editor.

## Behavior

### Add opens at once, with the cursor in the Title

The Add window is created when the app starts and stays hidden until needed, so showing it loads nothing. It opens centered on the active screen, on top of other windows, with three fields:

1. **Title**: one line, where the cursor starts.
2. **Description**: grows as the user types.
3. **Tags**: the task's tags, with the most-used tags as chips below.

Title and Description use the editor that [How the apps fit together](../overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) picks, AvalonEdit.

Proposed: Add accepts typing within 100 ms of the Add new task shortcut, measured from the key press to the first typed character showing. This is a decision to confirm.

Proposed, each a decision to confirm:

- Show Add opens Add with the desktop platform's [Show on the pointer's screen](README.md#windows-open-on-the-screen-with-the-mouse-pointer), which says what the active screen and on top mean. The pointer is also where a click on the tray icon happens.
- Closing Add, with Esc or its close button, hides it, so the next Show opens the same window at once.
- The user can't resize Add. It has a fixed width, wide enough for a long title on one line, and never opens shorter than a set height; it grows taller with what it holds, up to the bottom of the screen's work area, and then the Description scrolls.
- Add never waits for Tasks to read the tasks. Until it has, the tag chips, the tag suggestions, and the list of tasks to reference are empty; they fill in when it has.

### Keys save, close, and move between fields

The table lists the keys that work across the window, and which are still proposals:

| Key | Where | What it does | Status |
| --- | --- | --- | --- |
| Enter or Tab | Title | Moves to the Description | Decided |
| Tab | Description, outside a list item | Moves to the Tags line | Proposed |
| Enter | Tags | Adds the typed or suggested tag | Proposed |
| Ctrl+Enter | Anywhere | Saves the task. Proposed: and closes Add. | Decided |
| Ctrl+Shift+Enter | Anywhere | Saves the task and clears Add for another one | Proposed |
| Esc | Anywhere | Closes Add and keeps a draft. Proposed: closes an open list, such as the snippet picker, first. | Decided |

Proposed, each a decision to confirm:

- A task with an empty Title can't be saved: Add stays open and marks the Title.
- Pasting or dropping an image runs the image snippet. A paste inserts the clipboard's text when it has text, even with an image beside it, as apps such as Excel put both; Ctrl+Shift+I inserts the image then.
- In the Title, a command or snippet that works only in the Description does nothing, pasted lines join with spaces, and a pasted or dropped image is ignored.
- Add has a Save button, which saves as Ctrl+Enter does.

### Every formatting command and snippet has a key

[Editing](../editing.md) defines each command and the fields it works in; this page only maps desktop keys to them. Typing "/" in the Description also opens Editing's snippet picker, which the Task editor shows as a list at the cursor.

The table maps each command to its desktop key. Proposed: these keys. This is a decision to confirm.

| Command | Key |
| --- | --- |
| Bold | Ctrl+B |
| Italic | Ctrl+I |
| Strikethrough | Ctrl+Shift+X |
| Inline code | Ctrl+E |
| Color: opens a list of the palette's colors | Ctrl+Shift+C |
| Link, filled from a URL on the clipboard | Ctrl+K |
| Heading 1, 2, or 3 | Ctrl+1, Ctrl+2, Ctrl+3 |
| Date stamp | Ctrl+; |
| Date and time stamp | Ctrl+Shift+; |
| Note, tip, or warning block | Ctrl+Shift+N, Ctrl+Shift+T, Ctrl+Shift+W |
| Image, from the clipboard or a file | Ctrl+Shift+I |
| Code block | Ctrl+Shift+E |
| Reference to another task | Ctrl+Shift+R |
| Table | Ctrl+Shift+G |
| Horizontal line | Ctrl+Shift+- (hyphen) |

No key uses Ctrl+Alt, which [Shell rules out](shell.md#ruled-out). The stamp keys follow Excel, where Ctrl+; inserts the date; Excel's Ctrl+Shift+; inserts only the time ([Excel](https://support.microsoft.com/en-us/office/insert-the-current-date-and-time-in-a-cell-b5663451-10b0-40ab-9e71-6b0ce5768138)), but here it inserts the date and time.

Proposed, each a decision to confirm:

- Ctrl+Shift+I inserts the image on the clipboard, or opens a file dialog for an image when the clipboard has none.
- Ctrl+Shift+R opens a list of open tasks at the cursor, in rank order, narrowed by Tasks' [search](../tasks.md#tags-and-search-ignore-case) as the user types. Enter inserts the reference to the chosen task.

### Closing without saving keeps a draft on this device

Closing Add without saving, with Esc or by clicking elsewhere, keeps its text as a draft. The next Add opens with the draft, and a Discard command clears it. Drafts stay on this device and never sync: the draft lives in the shared platform's [device store](../platform.md#files-that-stay-on-this-device-live-in-the-device-store), outside the clone, and [Tasks](../tasks.md#a-draft-waits-on-this-device-until-its-saved) keeps it.

Proposed, each a decision to confirm:

- The draft is kept when Add hides and when the app stops, through the [keep the draft](#public-interface) part, which Shell calls when it stops.
- Add hides when another window gets the focus, as clicking elsewhere does. A window Add opens itself, such as a list at the cursor or the file dialog, doesn't count.
- Discard is a button in the Add window. It clears Add and its draft at once, with no confirmation, and Add stays open.
- Saving the task clears the draft, and an empty Add keeps no draft.

### Tags come from the tags already in use

The Tags line suggests tags already in use as the user types. Below it, the five most-used tags show as chips; clicking one adds it. The user expects few tags, so five chips usually cover them.

Proposed, each a decision to confirm:

- Most-used counts every stored task, open and resolved, and ties sort alphabetically.
- Chips are buttons, so the keyboard reaches them with Tab.
- The task's tags show in the Tags line, each with a button that removes it. A chip whose tag the task has shows as chosen, and clicking it removes the tag, as on [Android](../android/task-editor.md#tags-sit-as-chips-under-the-title).
- Suggestions show in a list under the Tags line, as [Tasks](../tasks.md#tags-and-search-ignore-case) picks them.

### The editor styles Markdown in place, with no preview

Add shows Markdown source styled in place: bold looks bold, and headings, links, and code are styled. The styling comes from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no rendered preview and uses no web view, so it never waits for one.

Proposed, each a decision to confirm:

- Colored text shows in its color, such as `:red[urgent]` in red.
- Colors follow the desktop platform's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), also when Windows switches between light and dark while Add is open.
- Styling never delays typing: typed text shows at once, and its styling follows.

### Save writes the task into this device's clone

Save writes the task's file at once, through [Tasks](../tasks.md#tasks-reads-the-clones-folder-which-can-change-while-the-app-runs), into the folder [Configure keeps](configure.md#the-sync-settings-section-sets-the-devices-clone) for this device's clone. Until the [Sync step](README.md#delivery-backlog), nothing commits or pushes it; that step commits and pushes the files waiting in the clone. Tasks reads the folder's tasks when the app starts, and again when Configure changes the folder, so a folder chosen while the app runs works at once. With no folder, Save fails, as [Failure modes](#failure-modes) says.

### A new task goes to the top

A saved new task goes to the top of the order, where the user sees it first in View Tasks. Tasks computes its rank, as [the desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists.

## Public interface

The composition root, Shell, and the Task list use these parts:

- **Create the Add window**: the composition root creates it when the app starts, as [above](#add-opens-at-once-with-the-cursor-in-the-title), and gives it to Shell.
- **Show Add**: shows the Add window, with the draft if there is one. Shell calls it from the [Add new task shortcut](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions), the tray icon, and the tray menu.
- **Proposed: keep the draft**: keeps what Add holds as this device's draft, as hiding Add does, when it changed since it was last kept. Shell calls it when it stops, so the draft survives the app's exit. A later instance, which Shell ends at once, never showed Add, so it writes nothing over the running instance's draft. This is a decision to confirm.
- **The editor**: the Title, Description, and Tags of one task, with every key above. It reports when the user saves or cancels. The Task list uses it to edit a task in place.
- **The tag chips**: the five most-used tags as buttons, reporting the chosen tag. The Task list uses them to filter.

The Task editor publishes two ports, which the composition root implements beside itself, so its UX tests use fakes and never touch the real clipboard or open a dialog. Create the Add window takes them, with the parts of Tasks Add uses, the clock, and the desktop platform's [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).

- **The clipboard**: its text, and its image, if it has one. Proposed: it also reads the image a drag-and-drop carries, since Windows hands over dropped data as it does the clipboard's. This is a decision to confirm.
- **The image file dialog**: the image file the user picks, or none when the user cancels.

## Failure modes

- **No repository folder is chosen**: Save fails, and Add keeps the text and the draft, and says to choose a repository folder in Configure.
- **Saving fails**, for example because the task file can't be written: Add stays open with the text, shows the error, and keeps the draft.
- **An image can't be read or saved**: nothing is inserted, and Add says why.
- **The draft can't be read**: Add opens empty, says the draft couldn't be loaded, and leaves the draft file as it is.

## Ruled out

The options below were considered and rejected, grouped by decision.

- The Add window:
  - **A full editor window**: Add stays compact, for a quick task.
  - **Opening on the primary screen**: on a PC with two screens, Add could open away from where the user works.
  - **Always on top**: Add would cover a window the user wants to copy from.
- Closing without saving:
  - **Esc discards the text, after a confirmation**: a draft keeps the text without asking.
  - **Closing saves the task automatically**: an accidental close would create a half-written task on every device.
- Where the draft is kept:
  - **A device folder only Tasks gets from the composition root**: you chose a store in the shared platform, which other files that stay on the device can use.
  - **A draft file each app's Task editor writes, with Tasks keeping only its images**: each app would write the draft's format, which the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) forbids once Android has drafts, and a draft's text and images would live in two modules.
- What Save does before the Sync step:
  - **Tasks reads the folder only when the app starts**: right after the user chooses a folder, Save would fail until a restart, and its message would have to say so.
  - **No Save until the Sync step**: this step would keep no task, and Tasks' file code and the Save keys would wait for the Sync step.
- Tags:
  - **Inline #tags in the Title, plus a Tags line**, or **inline #tags only**: tags would mix into the title's text.
- Where a new task goes:
  - **The bottom**: it can scroll out of sight.
  - **A setting**: one more option to test.
- Preview:
  - **A preview key**: the first preview waits about 0.2–0.85 s for WebView2 to start ([WebView2 start-up](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1909)).
  - **A live preview, always on**: about 40–50 MB more memory all day ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)).
