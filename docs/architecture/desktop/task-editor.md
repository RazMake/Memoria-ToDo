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

Proposed: Add accepts typing within 100 ms of the shortcut, measured from the key press to the first typed character showing. This is a decision to confirm.

Proposed, each a decision to confirm:

- Show Add opens Add with the desktop platform's [Show on the pointer's screen](README.md#windows-open-on-the-screen-with-the-mouse-pointer), which says what the active screen and on top mean. The pointer is also where a click on the tray icon happens.
- Closing Add, with Esc or its close button, hides it, so the next Show opens the same window at once.
- The user can't resize Add; it sizes to what it holds.

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
- Pasting or dropping an image runs the image snippet.

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

### Closing without saving keeps a draft on this device

Closing Add without saving, with Esc or by clicking elsewhere, keeps its text as a draft. The next Add opens with the draft, and a Discard command clears it. Drafts stay on this device and never sync.

Proposed, each a decision to confirm:

- The draft is saved outside the clone, for example under `%LocalAppData%`, when Add hides and when the app exits.
- Discard is a button in the Add window.
- Saving the task clears the draft.

### Tags come from the tags already in use

The Tags line suggests tags already in use as the user types. Below it, the five most-used tags show as chips; clicking one adds it. The user expects few tags, so five chips usually cover them.

Proposed, each a decision to confirm:

- Most-used counts every stored task, open and resolved, and ties sort alphabetically.
- Chips are buttons, so the keyboard reaches them with Tab.

### The editor styles Markdown in place, with no preview

Add shows Markdown source styled in place: bold looks bold, and headings, links, and code are styled. The styling comes from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no rendered preview and uses no web view, so it never waits for one.

Proposed, each a decision to confirm:

- Colored text shows in its color, such as `:red[urgent]` in red.
- Styling never delays typing: typed text shows at once, and its styling follows.

### A new task goes to the top

A saved new task goes to the top of the order, where the user sees it first in View Tasks. Tasks computes its rank, as [the desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists.

## Public interface

The composition root, Shell, and the Task list use these parts:

- **Create the Add window**: the composition root creates it when the app starts, as [above](#add-opens-at-once-with-the-cursor-in-the-title), and gives it to Shell.
- **Show Add**: shows the Add window, with the draft if there is one. Shell calls it from the shortcut and the tray menu.
- **The editor**: the Title, Description, and Tags of one task, with every key above. It reports when the user saves or cancels. The Task list uses it to edit a task in place.
- **The tag chips**: the five most-used tags as buttons, reporting the chosen tag. The Task list uses them to filter.

## Failure modes

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
- Tags:
  - **Inline #tags in the Title, plus a Tags line**, or **inline #tags only**: tags would mix into the title's text.
- Where a new task goes:
  - **The bottom**: it can scroll out of sight.
  - **A setting**: one more option to test.
- Preview:
  - **A preview key**: the first preview waits about 0.2–0.85 s for WebView2 to start ([WebView2 start-up](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1909)).
  - **A live preview, always on**: about 40–50 MB more memory all day ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)).
