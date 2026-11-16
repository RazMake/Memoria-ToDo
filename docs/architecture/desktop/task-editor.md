# Task editor: how the desktop app adds a task in seconds

Status: Decided

The Task editor is the desktop app's Add window, built so one key press puts the cursor in a new task's title with no wait. Task View edits a task with the same editor, on its right side.

## Behavior

### Add opens at once, with the cursor in the Title

The Add window is created when the app starts and stays hidden until needed, so showing it loads nothing. It opens centered on the active screen, on top of other windows, with three fields:

1. **Title**: one line, where the cursor starts.
2. **Description**: grows with its content, then scrolls. Proposed: it is at least 140 and at most 400 device-independent pixels tall, so the window stays on screen. This is a decision to confirm. Proposed, a decision to confirm: where a window gives the [Description editor](#public-interface) a set height, as Task View's [right side](task-list.md#edit-description-turns-the-right-side-into-the-editor) does, the Description fills that height instead, with no upper limit, and scrolls inside itself. The Description editor then sets its `MinHeight` to its height with the Description at its smallest, so the container can tell when it must scroll.
3. **Tags**: the task's tags as colored badges, with a + button that [adds one](#tags-show-as-colored-badges-and-the-plus-button-adds-one).

Above the fields, a small **Clear** button empties Add and deletes its [draft](#closing-without-saving-keeps-a-draft-on-this-device). Below them, a **Save** button saves the task, exactly as the [Save shortcut](#keys-save-close-and-move-between-fields) does. Clear is smaller than Save and sits apart from it, so it isn't clicked by mistake.

Title and Description use the editor that [How the apps fit together](../overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) picks: [AvalonEdit](https://www.nuget.org/packages/AvalonEdit/6.3.1.120) 6.3.1.120, a dependency of the Task editor's project. It restores and builds on .NET 11, but its first run there is not confirmed until the UX tests run. It references the whole desktop framework, so the project turns off the WinForms analyzer warning WFO2000, which is meant for WinForms controls. The box where a new tag is typed stays a plain WPF text box.

Title and Description look like the tag box in both the light and the dark [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it): the same background, text color, border, and rounded corners. The theme styles only WPF's own text box, not AvalonEdit's editor, so one place in the Task editor, used by both fields, gives them the look from the theme's own text box resources. The Task editor copies no color, so the fields switch with the theme as the tag box does. A Title marked as missing keeps its red border.

Proposed: Add accepts typing within 100 ms of the Add new task shortcut, measured from the key press to the first typed character showing. This is a decision to confirm.

Proposed, each a decision to confirm:

- Show Add opens Add with the desktop platform's [Show on the pointer's screen](README.md#windows-open-on-the-screen-with-the-mouse-pointer), which says what the active screen and on top mean. The pointer is also where a click on the tray icon happens.
- Closing Add, with Esc or its close button, hides it, so the next Show opens the same window at once.
- The user can't resize Add. It has a fixed width, wide enough for a long title on one line, and never opens shorter than a set height; it grows taller with what it holds.
- Title and Description also match the tag box's padding, and its look under the pointer and with the focus.
- Clear sits at the top right, above the Title. Save sits at the bottom right, below the tags and the message line.
- Save has the theme's accent button look, as the window's main action. Clear has the theme's plain button look, with smaller text and padding, so it is shorter than Save.
- Save's tooltip names the Save shortcut's current keys, Ctrl+Enter at first, so the user learns them.
- Save isn't the window's default button, so Enter keeps its meaning in each field.
- The keyboard reaches both buttons with Tab, in the order they show.
- Both buttons belong to the Add window, not to the editors the Task list reuses, so the [public interface](#public-interface) doesn't change.
- **The Description editor shows a Save button too, at its bottom right under its message line**, for Task View's [edit](task-list.md#edit-description-turns-the-right-side-into-the-editor). The user decided this. It raises the editor's `SaveRequested`, as the Save shortcut does, so the public interface doesn't change. Proposed: one internal part of the Task editor makes Save for both, with the same look and tooltip, so the button keeps one home. This is a decision to confirm.
- **While a new version is available or updating, Add shows [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) along its bottom edge**, below Save, and grows taller by the bar's height, as it grows with what it holds. Otherwise the bar is hidden. A click on the bar's update text starts the update, which closes Add and keeps its draft.

### Keys save, close, and move between fields

The table lists the keys that work across the window, and which are still proposals:

| Key | Where | What it does | Status |
| --- | --- | --- | --- |
| Enter or Tab | Title | Moves to the Description | Decided |
| Tab | Description, outside a list item | Moves to the tags | Proposed |
| Enter | Tag box | Adds the typed tag, keeping its color | Proposed |
| [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window), Ctrl+Enter at first | Anywhere | Saves the task, as the Save button does. Proposed: and closes Add. The user decided the key can be changed, in Configure. | Decided |
| Ctrl+Shift+Enter | Anywhere | Saves the task and clears Add for another one. Proposed: these keys stay fixed when the Save shortcut changes, and the Save shortcut wins if the user picks them for it. | Proposed |
| Esc | Anywhere | Closes Add and keeps a draft. Proposed: closes an open list, such as the snippet picker or the tag menu, first. | Decided |
| [Close window shortcut](configure.md#esc-and-the-close-window-shortcut-close-the-window), Shift+Esc at first | Anywhere, even with a list open | Closes Add and keeps a draft, as its close button does | Decided |

Proposed, each a decision to confirm:

- The Save shortcut, Ctrl+Shift+Enter, and Esc also work while the Clear or Save button has the focus.
- A task with an empty Title can't be saved: Add stays open and marks the Title.
- Pasting text with line breaks into the Title joins its lines with spaces, since a title is one line.
- Pasting or dropping an image runs the image snippet.
- Ctrl+Shift+I inserts the clipboard's image when it holds one, and otherwise opens a file dialog to choose one. Cancelling the dialog inserts nothing and shows no error.
- Ctrl+V runs the image route only when the clipboard holds an image, and pastes as usual otherwise. Ctrl+V, Ctrl+Shift+I, and file drops work in the Description only; the Title ignores them.
- A chosen or dropped file's alt text is its name without the extension, and `image` when that is empty, as is an image from the clipboard's. A drop inserts at the cursor, in place of the selection, and uses only the first dropped file. A dropped file that isn't an image shows an error. Drops that carry no file, such as dragged text, keep their usual meaning in the editor.
- The snippet list's Image entry does what Ctrl+Shift+I does, in place of the "/" and what was typed after it. When the dialog is cancelled, the typed text stays.
- An image that can't be read, isn't an image, can't be shrunk to 1 MB, or can't be kept shows an error on the [editor's message line](#add-and-task-view-save-the-same-way-through-the-editor). The error mentions the image, nothing is inserted, and the text, cursor, and focus stay as they were. The next image that is added clears the message.
- Opening the file dialog doesn't hide Add, though the dialog takes the focus.
- **No focus outline frames the editors or their fields.** WPF draws a control's focus outline only when the keyboard moves the focus to it ([focus visual style](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle)), so a key such as Ctrl+Tab can frame a field or a whole editor in a light outline. The user asked that it never show. The fields show the focus with their own focused border and caret, as the tag box does. Buttons and badges keep their outline, so keyboard users still see which one has the focus. Proposed: in Add, Ctrl+Tab keeps WPF's move to the next control; in Task View's edit, it switches the [Edit and Preview tabs](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab). This is a decision to confirm.

### Every formatting command and snippet has a key

[Editing](../editing.md) defines each command and the fields it works in; this page only maps desktop keys to them. Typing "/" in the Description also opens Editing's snippet picker, which the Task editor shows as a list under the Description.

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

- The color list opens under the Description, not at the cursor, and shows only while open. Click a color, or press Down to move through them and Enter to choose, as with Search's matches in the [tag menu](#the--menu-adds-a-top-tag-or-one-found-by-search). Esc closes it first. The cursor stays in the field, and the chosen color wraps the field's selection.
- Each formatting and heading key runs on the field with the cursor. Each key's result replaces only the text that changed, as one undo step, and keeps the selection Editing returns.
- Editing sees every line break as "\n". If the text holds any "\r\n" or "\r", the key converts them first.
- In the Description, Enter, Tab, and Shift+Tab run Editing's list commands in a list item. Tab on a first item that can't move is swallowed: the cursor stays in the Description. Outside a list item, Enter breaks the line as usual, Tab moves to the tags, and Shift+Tab moves back to the Title.
- The snippet list and the task list open under the Description, like the color list, not at the cursor. The color, snippet, and task lists work alike, and at most one is open: opening one closes the others. Down moves through the items and Enter chooses the one moved to. Enter with a list open and nothing moved to closes the list and then does what Enter does with no list.
- Items are chosen by position, so tasks with equal titles stay apart.
- The snippet list closes when nothing matches, when the cursor moves before the "/", or when the "/" no longer opens Editing's picker, such as after Backspace removes it. Choosing a snippet replaces the "/" and what was typed after it, even for the link, whose text is then empty.
- Snippet keys do nothing in the Title unless Editing says the snippet works in a Title.
- Empty or missing clipboard text counts as no web address for the link, so the link gets an empty address and no error shows.
- The task list shows every open task, newest first, until the user types; what is typed after it opens narrows it by Tasks' search. With no open task, or none matching, no list shows. Choosing a snippet that needs a task from the snippet list opens the task list, and choosing a task replaces the "/" and what was typed.

The reference to another task opens a list, as above. It lists open tasks only, and typing filters them with [Tasks' search](../tasks.md#tags-and-search-ignore-case). The link's text is the chosen task's title with its Markdown kept, and only brackets and backslashes escaped; the list itself shows the title as a reader sees it, without its Markdown, which [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) mark.

### Closing without saving keeps a draft on this device

Closing Add without saving, with Esc or by clicking elsewhere, keeps its text as a draft. The next Add opens with the draft, and the Clear button clears it. Drafts stay on this device and never sync. Add never asks before it closes, since the draft keeps the text; Task View [asks](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) only before it drops a changed edit, which has no draft.

[Tasks keeps the draft](../tasks.md#a-draft-waits-on-this-device-until-its-saved), with its images, in `%LocalAppData%\Memoria-ToDo\draft\`, a folder the composition root gives it. Add keeps the draft when it hides and when the app exits, and saving the task clears it. Clicking Add's own buttons, its lists, or its file dialog isn't clicking elsewhere.

Add keeps the draft each time it hides, with every field empty meaning no draft: the kept draft is removed. Show restores the kept draft into all three fields with the cursor in the Title, and a draft whose task ID is already a task is deleted and Add opens empty. Clear empties the fields, deletes the kept draft and its images with [Tasks' `DiscardDraft`](../tasks.md#public-interface), starts a new task ID, and leaves Add open with the cursor in the Title. Clear is only the button's name: Tasks calls that step discard, so no public interface changes. A save that fails leaves the text, so Esc keeps it as the draft.

Proposed, each a decision to confirm:

- Add keeps the draft itself when its window closes as the app exits, so the composition root needs no extra call.
- After Clear, Add shows no message and no mark on the Title, as for a new task.
- Text typed in the tag box and not yet added as a tag isn't part of the draft, but the hidden window keeps it, so the next Show of the same window finds it. Clear and saving clear it.
- A save that fails with an unreadable-data error keeps Add open and shows the message, as for other failed saves. A save with no title marks the Title with a red border until it is edited.
- While a draft that can't be read is on disk and the fields are still empty, hiding leaves that file alone, so a window opened and closed by mistake doesn't destroy it. Typing something or saving replaces it, and Clear deletes it.
- If keeping the draft fails when Add hides, the error is not caught, so the failure is loud rather than a draft silently lost.

### Tags show as colored badges, and the plus button adds one

The user decided how tags look and are added. The task's tags show as badges, each in its own color, followed by a **+** drop-down button. The + opens a menu with three parts:

1. **The eight most-used tags**, which add with one click.
2. **Search**: a text box. The tags that match what is typed show in a submenu beside it, and a click on one adds it.
3. **Custom**: a text box labeled **New tag:**, with no spaces, and Rendering's 30 [tag colors](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes), in the current theme's set. The colors show only while the box holds text, and a click on a color adds the typed tag in that color.

[Tasks](../tasks.md#each-tags-color-is-kept-in-one-synced-file) keeps each tag's color, so every device shows it. [The menu](#the--menu-adds-a-top-tag-or-one-found-by-search) and [Custom](#custom-adds-a-new-tag-in-the-color-clicked) are described below.

The badges and the + button make up the tag control. It's part of the editor, so Add gets it, and the Task list shows it under the [selected task's](task-list.md#tags-show-as-colored-badges-and-change-without-edit) description. Tags still follow [Tasks' tag rules](../tasks.md#tags-and-search-ignore-case): case is ignored, and a typed tag takes the spelling of a matching tag in use.

The user decided that tags typed in the tag box, Custom's **New tag:** box, are lowercase and have no spaces, and that tags already in use stay as they are. So in the tag box:

- **Typed letters show lowercase**, whatever the Shift or Caps Lock state, and so do pasted ones, by the invariant culture's rules, so a tag reads the same on every PC.
- **A typed space does nothing**, and spaces in pasted text are removed, so "to do" pastes as "todo".
- **Only typed and pasted text changes.** Existing tags, the + menu's tags, Search's matches, and a typed tag that matches a tag in use, which takes that tag's spelling as Tasks decides, keep their spelling, so `Work` stays `Work` on every task that has it.

Proposed: **the Search box keeps what is typed as it is.** It only finds tags in use, and matching ignores case, so it needs no lowercase rule; keeping spaces lets it find an older tag that has one. This is a decision to confirm.

Proposed, each a decision to confirm:

- **Other whitespace, such as a tab, is removed as a space is**, so no tag has a gap in it.
- **A removed space joins its neighbors** rather than becoming a hyphen, so the user sees exactly what they typed, less the space.

Proposed, each a decision to confirm:

- **Each badge has a button that removes its tag.** Badges wrap onto more lines when a task has many.
- **The + button follows the last badge, in the same row**, and wraps with the badges, so it always sits where the next tag will go. With no tags, it starts the row.
- **A color belongs to the tag, not to one task.** Choosing a color for a tag already in use changes it on every task, and on every device after a sync.
- **A tag with no color kept shows its default color**, which [Rendering](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) picks from the tag's text with case ignored, so a tag looks the same in every window and on every device. A color name this app doesn't know, such as one a newer app wrote, shows the default and is kept as it is.
- **A badge takes its color's value and text color for the current theme**, from Rendering, and changes when the theme or a tag's color does.
- **Tab reaches each badge's remove button, then the +.**
- **A clicked color is kept when its tag is added**, not when the task is saved, so the [draft](#closing-without-saving-keeps-a-draft-on-this-device) keeps its format. The color stays with the tag even if the task is never saved.

### The + menu adds a top tag or one found by search

The + is a drop-down button: a click on it, or Enter, Space, or Down while it has the focus, opens the menu under it, over the window, and a second click closes it. The menu is drawn in the theme's menu colors, copying none. The user decided it's a WPF [Popup](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup) under the +, holding the Task editor's own items, with a second Popup beside Search for the matches. Keys pressed inside a popup still travel up to the window, since a popup's root passes routed events to the popup's parent ([PopupRoot.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/PopupRoot.cs)). So Esc, the [Close window shortcut](#keys-save-close-and-move-between-fields), and the Save shortcut keep working with the menu open.

Proposed, each a decision to confirm:

- **The top of the menu lists up to eight tags**: the tags in use that the task doesn't have yet, in Tasks' most-used order, each as a badge in its color. A line separates them from Search. With no such tag, the menu starts with Search. Eight has one home in the Task editor's code.
- **Search is a text box labeled "Search:"**. Typing in it shows the tags that match in a submenu to the right of the Search item, or to its left when the screen has no room. The user decided which tags match: the tags in use that contain the typed text anywhere, case ignored, that the task doesn't have yet, so `ork` finds `work`. They're [Tasks' tag suggestions](../tasks.md#tags-and-search-ignore-case). Matches show as badges in their colors, most-used first, and may include the top eight.
- **The submenu lists every match**, and scrolls when it's taller than the screen allows. With an empty Search box, no submenu shows. When nothing matches, it shows one dimmed line, "No matching tags", that can't be clicked.
- **Custom… closes the menu and opens [Custom](#custom-adds-a-new-tag-in-the-color-clicked).**
- **Adding a tag from the menu or its submenu closes both**, and the focus goes back to the +. Search's text is never part of the task, the draft, or the editor's changes, and closing the menu drops it.
- **A click outside the menu closes it**, as Esc does. Opening the menu doesn't take the window's focus away, so it never counts as [clicking elsewhere](#closing-without-saving-keeps-a-draft-on-this-device).
- **Adding a tag from the menu while Custom shows leaves Custom as it is**, with its typed text, so no typing is lost.

Keys in the menu, proposed, each a decision to confirm:

| Key | Where | What it does |
| --- | --- | --- |
| Up, Down | The menu | Move through the tags, Search, and Custom… Moving onto Search puts the cursor in its box. |
| Enter | A tag, or Custom… | Adds the tag, or opens Custom |
| A letter or digit | The menu, outside Search | Goes into the Search box, so typing starts a search at once |
| Down, Up | The Search box, with matches | Move a highlight through the matches, while the cursor stays in the box; Up from the first match leaves none highlighted |
| Down, Up | The Search box, with no matches | Move to Custom…, or to the last tag |
| Enter | The Search box | Adds the highlighted match, or the first match when none is; with no match, does nothing |
| Esc | Anywhere in the menu | Closes the menu and drops Search's text |

### Custom adds a new tag in the color clicked

Custom shows below the badges, with the cursor in its text box, labeled **New tag:**. The user decided that the box takes no spaces, that the colors show only while the box holds text, and that a click on a color adds the typed tag with that color. The user also decided that Custom opens below the badges, as today, and stays until a tag is added or Esc. So a click elsewhere can't lose the typed tag, and the Save button and shortcuts can add its text.

Proposed, each a decision to confirm:

- **The box suggests nothing.** Search finds the tags in use, so two lists would offer the same tags.
- **The colors show as a grid, one column per hue and one row per shade**, below the box. They hide again when the box is emptied.
- **The grid marks the color the typed tag would get with Enter**: its kept color, or its default. So typing `work` shows what `work` looks like now.
- **A click on a color keeps that color for the tag, then adds it**, spelled as a tag in use when one matches. A tag already in use is recolored on every task, as [a color belongs to the tag](#tags-show-as-colored-badges-and-the-plus-button-adds-one). Custom then hides, the box empties, and the focus goes to the +.
- **Enter in the box adds the typed tag with no new color**, so it keeps its kept color, or its default. That keeps adding a tag fast from the keyboard and never writes `tags.json`.
- **Keys**: Tab moves from the box to the colors. In the colors, the arrow keys move, and Space or Enter adds the tag in the color moved to. Backspace in the empty box removes the task's last tag. Esc hides Custom and drops what was typed there, before it closes Add.
- **The Save button, the Save shortcut, and Ctrl+Shift+Enter add any text still in the box**, as Enter does, before they save, so no typing is lost. That text counts as a change, as typed tag text does today.
- **When a clicked color can't be kept, the tag isn't added.** Custom stays open with the typed text, and says why under the colors. With no clone folder, it says to choose the repository folder in Configure, then add the tag again. Otherwise it says the tag wasn't added because its color couldn't be kept, followed by the reason, such as the file error. (Decision to confirm: the wording.) The message clears when the text changes or Custom hides.

### The editor styles Markdown in place, with no preview

Add shows Markdown source styled in place: bold looks bold, and headings, links, and code are styled. The styling comes from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no rendered preview and uses no web view, so it never waits for one. Task View's Edit Description has a [Preview](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab), which the Task list builds around the Description editor with its own web view; the editor itself still shows no preview.

Proposed, each a decision to confirm:

- Colored text shows in its palette color for the current [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), such as `:red[urgent]` in red.
- Markdown's own characters, such as `**` around bold text, show dimmed.
- Headings, links, code, and italic text take Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme) for the current theme: every heading in its color and at its size, a link underlined in the link color, code in its font and color on the faded background, and italic text in the italic color.
- Styling never delays typing: typed text shows at once, and its styling follows.

### A new task goes to the top

A saved new task goes to the top of the order, where the user sees it first in Task View. Tasks computes its rank, as [the desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists.

### Add and Task View save the same way, through the editor

The editors run every save themselves, so Add and the Task list's [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor) and [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row) save the same way. Their user passes the function that saves: Add passes [Tasks'](../tasks.md#public-interface) add, and the Task list passes Tasks' save of an edit. Each save takes three steps, each where the editor has the field it needs:

1. Tag text typed and not yet added becomes a tag, so no typing is lost.
2. An empty Title is marked, the cursor goes to it, and nothing is saved.
3. The editor calls the function. If it fails, the editor keeps the text and says why on its own message line.

The editor tells its user whether the task was saved, so Add can clear or close, and the Task list can leave the edit. The editors know the failures Tasks' saves throw, so each failure gets one message on both screens. In Add, the message line sits under the tags, so it is still above Save, and it also says why an image wasn't added. The Title editor and the Description editor each show theirs under their field. Add's editor also lets its user put the cursor in the Title.

### With no clone folder, saving points to Configure

Tasks keeps tasks in [Configure's repository folder](configure.md#the-sync-settings-section-sets-the-devices-clone). Until one is set, Add still opens and keeps a draft, but saving fails: Add stays open with the text and says to set the repository folder in Configure. App's [selection adapter](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) gives Tasks the selected folder. Configure and Tasks stay unaware of each other.

## Public interface

The composition root, Shell, and the Task list use these parts:

- **Create the Add window**: the composition root creates it when the app starts, as [above](#add-opens-at-once-with-the-cursor-in-the-title), and gives it to Shell.
- **Show Add**: shows the Add window, with the draft if there is one. Shell calls it from the [Add new task shortcut](shell.md#a-shortcut-switches-between-add-and-task-view), the tray icon, and the tray menu.
- **Close Add**: hides the Add window and keeps the draft, as Esc does, and does nothing while it's hidden. Shell calls it when the Task View shortcut [switches to Task View](shell.md#a-shortcut-switches-between-add-and-task-view).
- **The editor**: Add's Title, Description, and Tags of one task, with every key above. It reports when the user asks to save or cancels, [saves](#add-and-task-view-save-the-same-way-through-the-editor) through the function its user passes, puts the cursor in the Title, and says whether anything changed since its fields were set. Only Add uses it, so it's internal to the Task editor.
- **The Title editor and the Description editor**: the user decided that the Task list edits a title or a description alone through two public controls, each one field with the same keys, styling, paste rules, and save steps as Add's field, its own message line, and the same report of changes and requests to save or cancel. Add's editor is built from the same Title and Description parts, so each rule keeps one home. The Task list uses one at a time: the Title editor in a row, for [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row), and the Description editor on the right side, for [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor). The user decided that an editor tells what changed, so the Task list shows its Undo bar only for an edit with changes. The Description editor also sets an image's width in its text, for an image resized in the [edit's Preview](task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview): it writes the width with Rendering's [width function](../rendering.md#public-interface), as a change the user made, so the edit then has changes, and Ctrl+Z in the editor takes it back as one step.
- **The Save keys**: Add's editor and both field editors save on the platform's [Save keys](README.md#the-save-shortcut-saves-in-any-window-that-edits), so a new Save shortcut applies at once.
- **The tag control**: a task's tags as badges with remove buttons, then the + and its menu, as [above](#tags-show-as-colored-badges-and-the-plus-button-adds-one). It reports each tag the user adds or removes, and keeps a clicked color through Tasks, but never saves the task. The editor holds one, and the Task list shows one under the selected task's description.
- **Tag badges**: tags as read-only badges in their colors. The Task list shows one as each tag group's heading, and in place of the tag control during an edit.
- **Two ports**, the clipboard and the image file dialog, which the desktop app implements beside its composition root, so the Task editor's tests run on [fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test).

Proposed: the editor and the two tag parts follow Tasks' changes, the tag colors' changes, and the theme only while they're on screen, so a closed window or a left edit holds no subscription, as the [Formatted view](formatted-view.md#it-shows-what-rendering-decides-never-markdown) does. Each shows the current tags, colors, and theme when it's made, and again when it's back on screen, such as an edit reopened by Undo. This is a decision to confirm.

[Rendering](../rendering.md#public-interface) and [Editing](../editing.md#public-interface) are plain functions, so the Task editor calls them directly. The Task editor reads a dropped or chosen image file through Shared's `IFileSystem`, which `Create` and the editor's constructor take last (decision to confirm), and keeps each image through [Tasks' `SaveImage`](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), for a draft or an open edit alike.

Step 4 of the [desktop delivery backlog](README.md#delivery-backlog) adds the editor's `Save`, `FocusTitle`, and `HasChanges`, which the Task list needs. Row 7 adds `IAddWindow.Close`. Row 8 adds Tasks' [tag colors](../tasks.md#tag-colors-are-a-capability-of-their-own) to `Create` and the editor's constructor, right after `tasks`, and replaces `TagChips` with `TagEditor`, `TagBadges`, and `TopTags`. Row 11 removes `TopTags`, since the Task list dropped the tag filter that used it; the + menu keeps the eight most-used tags inside the Task editor, so the count still has one home. Row 11 also adds `TitleEditor` and `DescriptionEditor`, with `DescriptionEditor.SetImageWidth`, makes `TaskEditorControl` internal, and adds the platform's Save keys to `AddWindow.Create` as its new last argument, after the update status. Proposed: the two editors' members below. This is a decision to confirm.

```csharp
namespace Memoria.ToDo.Desktop.TaskEditor;

public static class AddWindow
{
    /// Creates the Add window. It opens only on IAddWindow.Show.
    public static IAddWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette,
        ITheme theme, TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files);
}
```

`AddWindow.Create` also takes the platform's [Close window keys](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows) as a new last argument, `CloseWindowKeys closeWindowKeys`, and attaches the Add window to them.

The user decided that the Task editor may use [Updates](updates.md), for Add's [status bar](#add-opens-at-once-with-the-cursor-in-the-title). Proposed: `AddWindow.Create` also takes Updates' [update status](updates.md#public-interface), `IUpdateStatus updates`, as its new last argument, after `closeWindowKeys`, and shows the status bar from it. This is a decision to confirm. Row 11 then adds `SaveKeys saveKeys` after it:

```csharp
public static IAddWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette,
    ITheme theme, TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
    CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys);
```

```csharp
/// The Add window, as Shell uses it.
public interface IAddWindow
{
    /// Shows the Add window, with the draft if there is one.
    void Show();

    /// Hides the Add window and keeps the draft, as Esc does. Does nothing while it's hidden.
    void Close();

    /// Whether the window is open now.
    bool IsOpen { get; }
}

/// One task's title, edited alone, as Add's Title edits it. Enter saves, since the title is one line.
public sealed class TitleEditor : UserControl
{
    public TitleEditor(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, SaveKeys saveKeys);

    /// The title's Markdown. Setting it replaces the text and starts HasChanges over.
    public string Value { get; set; }

    /// Whether the text differs at all from Value as last set.
    public bool HasChanges { get; }

    /// Calls save with Value, unless it's empty, which it marks. Returns false, keeping the text, when it's empty or
    /// save threw a failure Tasks' saves are known to throw; the editor says why under the field. Other failures propagate.
    public bool Save(Action<string> save);

    /// Puts the cursor at the end of the title.
    public void FocusEnd();

    public event EventHandler? SaveRequested;   // the Save shortcut, or Enter
    public event EventHandler? CancelRequested; // Esc, with no list open
}

/// One task's description, edited alone, as Add's Description edits it, images included.
public sealed class DescriptionEditor : UserControl
{
    public DescriptionEditor(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys);

    /// The task the description belongs to; an added image is kept for it.
    public string TaskId { get; set; }

    /// The description's Markdown. Setting it replaces the text and starts HasChanges over.
    public string Value { get; set; }

    /// Whether the text differs at all from Value as last set.
    public bool HasChanges { get; }

    /// Calls save with Value. Returns false, keeping the text, when save threw a failure Tasks' saves are known to
    /// throw; the editor says why under the field. Other failures propagate.
    public bool Save(Action<string> save);

    /// Sets the width of image number image, counted from 0 as the description page numbers images, as Rendering's
    /// ImageWidths.With writes it. It changes the text as typing does: HasChanges still compares with Value as last
    /// set, and the editor's undo takes it back. Throws ArgumentOutOfRangeException as ImageWidths.With does.
    public void SetImageWidth(int image, int width);

    /// Puts the cursor at the end of the description.
    public void FocusEnd();

    public event EventHandler? SaveRequested;   // the Save shortcut
    public event EventHandler? CancelRequested; // Esc, with no list open
}

/// A task's tags as colored badges, each with a button that removes it, then the + drop-down button. Its menu adds
/// one of the top tags the task doesn't have, adds a tag in use found by Search, or opens Custom: the New tag box
/// and the 30 tag colors, where a click on a color keeps that color through tagColors and adds the tag. It never
/// saves the task.
public sealed class TagEditor : UserControl
{
    public TagEditor(ITasks tasks, ITagColors tagColors, ITheme theme);

    /// The tags the badges show. Setting it replaces the badges, and leaves an open menu or Custom as it is.
    public IReadOnlyList<string> Tags { get; set; }

    /// Raised after the user adds a tag, spelled as Tasks' WithTag spells it, once Tags holds it. Adding a tag
    /// already in Tags, ignoring case, changes nothing and raises nothing.
    public event EventHandler<string>? TagAdded;

    /// Raised after the user removes a tag, once Tags no longer holds it.
    public event EventHandler<string>? TagRemoved;
}

/// Tags as read-only badges in their colors, wrapping onto more lines.
public sealed class TagBadges : UserControl
{
    public TagBadges(ITagColors tagColors, ITheme theme);

    /// The tags the badges show, in order.
    public IReadOnlyList<string> Tags { get; set; }
}

/// The system clipboard.
public interface IClipboard
{
    /// The clipboard's text, or null when it holds none.
    string? Text();

    /// The clipboard's image as PNG, or null when it holds none.
    byte[]? ImagePng();
}

/// The dialog that chooses an image file.
public interface IImageFileChooser
{
    /// The chosen file's full path, or null when the user cancels.
    string? Choose();
}
```

## Failure modes

- **No clone folder is set**: Add stays open with the text, says to set the repository folder in Configure, and keeps the draft.
- **Saving fails**, for example because the task file can't be written: Add stays open with the text, shows the error, and keeps the draft.
- **Saving fails and Tasks can't undo it** (it throws an `AggregateException`): Add stays open with the text and the draft, and the editor says the save was incomplete, so the user checks the repository folder before saving again. The Task list's edit gets the same message. (Decision to confirm: the wording.)
- **The edited task was deleted**, on this device or another (Tasks throws `TaskNotFoundException`): the edit stays open with the text, and the editor says "This task no longer exists. Copy your changes before leaving the editor." (Decision to confirm: the wording.)
- **An image can't be read or saved**: nothing is inserted, and Add says why.
- **A clicked color can't be kept**, because there's no clone folder, `tags.json` can't be read or is newer, or the file can't be written: the tag isn't added, and Custom keeps the text and says why, as [above](#custom-adds-a-new-tag-in-the-color-clicked).
- **`tags.json` can't be read**: every badge shows its tag's default color, and the editor still opens and saves tasks.
- **The draft can't be read**: Add opens empty, says the draft couldn't be loaded, and leaves the draft file as it is.

## Ruled out

The options below were considered and rejected, grouped by decision.

- The Add window:
  - **A full editor window**: Add stays compact, for a quick task.
  - **Opening on the primary screen**: on a PC with two screens, Add could open away from where the user works.
  - **Always on top**: Add would cover a window the user wants to copy from.
- The look of Title and Description:
  - **The Task editor's own colors for each theme**: they would copy the theme's colors and drift from the tag box.
  - **The theme's text box style itself on Title and Description**: WPF applies that style only to its own text boxes, and AvalonEdit's editor isn't one.
- Add's buttons:
  - **Clear beside Save at the bottom**: a slip of the mouse would clear the task instead of saving it.
  - **Save as the default button, pressed by Enter**: Enter already moves to the Description, breaks lines, and adds tags.
  - **A button for Ctrl+Shift+Enter too**: one more button in a compact window, for a key the user can learn.
  - **Clear inside the editor the Task list reuses**: the Task list's edit has [its own design](task-list.md#edit-description-turns-the-right-side-into-the-editor), with no Clear.
  - **A Save button class the Task editor publishes, which each window places**: a new public class, when the Task editor itself places both Saves.
  - **Save inside Add's whole editor, which Task View reused**: Task View now edits the description alone, through the [Description editor](#public-interface).
  - **Renaming Tasks' `DiscardDraft` to match Clear**: it would change a public interface only for the name on a button.
- Which part of saving the editor owns:
  - **The editor publishes its save steps, and each screen runs them and shows its own messages**: Add and the Task list would repeat the same steps and messages, which the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) forbids.
  - **A ready-made edit panel from the Task editor, which saves through Tasks itself**: the Task editor would take on editing existing tasks, which the [Task list](task-list.md#edit-description-turns-the-right-side-into-the-editor) owns.
- How the Task list tells whether a discarded edit changed anything:
  - **Always show the Undo bar when an edit is left without saving**: it would say an edit was discarded when nothing changed.
  - **Publishing the tag text typed and not yet added**: the Task list would decide what counts as a change in the editor, splitting that rule across two modules.
  - **Counting typed tag text in `Value`**: Add's draft would then keep it as a tag, which the [draft rule](#closing-without-saving-keeps-a-draft-on-this-device) excludes.
- Closing without saving:
  - **Esc discards the text, after a confirmation**: a draft keeps the text without asking. Task View's [prompt](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) is a separate case: an edit in place has no draft.
  - **Closing saves the task automatically**: an accidental close would create a half-written task on every device.
- Tags:
  - **Inline #tags in the Title, plus a Tags line**, or **inline #tags only**: tags would mix into the title's text.
  - **Keeping the five chips under the Tags line beside the + menu**: two lists of the same tags in one window.
  - **Keeping a picked color only when the task is saved**: the draft would need a new format to carry it.
  - **Adding the tag when its color can't be kept, in its default color**: the user would see a color other than the one picked, with no sign until another device shows it.
  - **One tag control with a flag that turns its + off for the rows**: rows need only colored badges, so a separate read-only part is smaller than a flag no other user needs.
  - **Picking a color, which a second click unpicks, then adding the tag with Enter**: the user asked that a click on a color add the tag at once.
  - **Colors that show while Custom's box is empty**: the user asked that they show only once a tag is typed.
  - **Seven tags in the + menu**: the user asked for eight.
  - **Suggestions under Custom's box, as it is typed**: Search finds the same tags in use, so the window would hold two lists of them.
  - **The + button on its own row, under the badges**: it would sit away from the tags it adds to, and take a row of its own.
  - **A WPF ContextMenu for the + menu**, with Search's matches as child items: the window's keys, such as the Close window and Save shortcuts, wouldn't reach it, and items take the focus as the pointer passes, pulling the cursor out of the Search box.
  - **Today's inline panel for the + menu**: it isn't a drop-down, and opening it grows Add and pushes the Task list's description down.
  - **Search finds only tags that start with the typed text**: the user asked that a tag containing the text anywhere match.
  - **Custom inside the drop-down, in place of the menu's items**: a stray click outside would close it and drop the typed tag, and Save couldn't add text from a closed menu.
- Where a new task goes:
  - **The bottom**: it can scroll out of sight.
  - **A setting**: one more option to test.
- Preview:
  - **A preview key**: the first preview waits about 0.2–0.85 s for WebView2 to start ([WebView2 start-up](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1909)).
  - **A live preview, always on**: about 40–50 MB more memory all day ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)).
- The clipboard and the file dialog:
  - **WPF's Clipboard and OpenFileDialog, used directly in the Task editor**: its tests would touch the real clipboard and open real dialogs.
- Choosing the task to reference:
  - **A search dialog**: a second window for what a list at the cursor does, as the snippet picker already does.
- What Ctrl+Tab does in the editor:
  - **Moving the focus between Task View's list and its editor**: a new key, and Add has nothing to switch to.
  - **Ignoring it**: the key would do nothing.
- How the Task list gets the Title or the Description alone:
  - **A mode on the whole editor**, such as "title only" or "description only": a flag that every key, save step, and change check must respect, which the [code design](../../../AGENTS.md#code-design) rules advise against.
  - **The Task list hides the fields it doesn't want**: hidden fields would still take part in saving and in whether anything changed, so a hidden empty Title could block a save.
- Who owns Task View's Preview of an edited description:
  - **The Description editor, with an Edit and a Preview tab of its own**: the Task editor would need the Formatted view, a new dependency in the [module map](../modules.md#module-map), and either a second web view in Task View or the Task list's one handed into the editor.
- How a resized image's width reaches the draft:
  - **The Task list sets `Value` to the new text**: setting `Value` starts `HasChanges` over, so Esc would show no Undo bar and closing wouldn't ask.
  - **A general member that replaces the text and keeps the changes check**: a wider part than the one need, which a caller could use to lose the user's typing.
