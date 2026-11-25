# Task editor: how the desktop app adds a task in seconds

Status: Decided

The Task editor is the desktop app's Add window, built so one key press puts the cursor in a new task's title with no wait. Task View edits a task with the same editor, on its right side.

## Behavior

### Add opens at once, with the cursor in the Title

The Add window is created when the app starts and stays hidden until needed, so showing it loads nothing. It opens centered on the active screen, on top of other windows, with three fields:

1. **Title**: one line, where the cursor starts.
2. **Description**: grows with its content, then scrolls. Proposed: it is at least 140 and at most 400 device-independent pixels tall, so the window stays on screen. This is a decision to confirm. Proposed, a decision to confirm: where a window gives the [Description editor](#public-interface) a set height, as Task View's [right side](task-list.md#edit-description-turns-the-right-side-into-the-editor) does, the Description fills that height instead, with no upper limit, and scrolls inside itself. The Description editor then sets its `MinHeight` to its height with the Description at its smallest, so the container can tell when it must scroll.
3. **Tags**: the task's tags as colored pills in the task's order, where the first one groups the task and shows larger, followed by a pill that [adds a tag](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them).

Above the fields, a small **Clear** button empties Add and deletes its [draft](#closing-without-saving-keeps-a-draft-on-this-device). Below them, a **Save** button saves the task, exactly as the [Save shortcut](#keys-save-close-and-move-between-fields) does. Clear is smaller than Save and sits apart from it, so it isn't clicked by mistake.

Title and Description use the editor that [How the apps fit together](../overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) picks: [AvalonEdit](https://www.nuget.org/packages/AvalonEdit/6.3.1.120) 6.3.1.120, a dependency of the Task editor's project. It restores and builds on .NET 11, but its first run there is not confirmed until the UX tests run. It references the whole desktop framework, so the project turns off the WinForms analyzer warning WFO2000, which is meant for WinForms controls. The box where a new tag is typed stays a plain WPF text box.

Title and Description look like the tag box in both the light and the dark [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it): the same background, text color, border, and rounded corners. The theme styles only WPF's own text box, not AvalonEdit's editor, so one place in the Task editor, used by both fields, gives them the look from the theme's own text box resources. The Task editor copies no color, so the fields switch with the theme as the tag box does. A Title marked as missing keeps its red border.

Proposed: Add accepts typing within 100 ms of the Add new task shortcut, measured from the key press to the first typed character showing. This is a decision to confirm.

The user decided that a click on another window leaves Add open behind it, as Task View and the app's other windows stay, so Alt+Tab, the taskbar, or the Add new task shortcut brings it back with its text as it was.

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
- **While a new version is available or updating, Add shows [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) along its bottom edge**, below Save, and grows taller by the bar's height, as it grows with what it holds. Otherwise the bar is hidden. A click on the bar's update text opens [What is new](updates.md#what-is-new-shows-each-newer-releases-notes) over Add. An update closes Add and keeps its draft.

### Keys save, close, and move between fields

The table lists the keys that work across the window, and which are still proposals:

| Key | Where | What it does | Status |
| --- | --- | --- | --- |
| Enter or Tab | Title | Moves to the Description | Decided |
| Tab | Description, outside a list item | Moves to the first tag pill, or to the add pill when the task has no tag | Proposed |
| Enter | Tag box | Adds the typed tag to the task, keeping its color | Proposed |
| Alt+Left, Alt+Right | A tag's pill | Moves that tag one place earlier or later | Proposed |
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

- The color list opens under the Description, not at the cursor, and shows only while open. Click a color, or press Down to move through them and Enter to choose, as with Search's matches in the [tag menu](#a-click-on-a-pill-opens-the-tag-menu). Esc closes it first. The cursor stays in the field, and the chosen color wraps the field's selection.
- Each formatting and heading key runs on the field with the cursor. Each key's result replaces only the text that changed, as one undo step, and keeps the selection Editing returns.
- Editing sees every line break as "\n". If the text holds any "\r\n" or "\r", the key converts them first.
- In the Description, Enter, Tab, and Shift+Tab run Editing's list commands in a list item. Tab on a first item that can't move is swallowed: the cursor stays in the Description. Outside a list item, Enter breaks the line as usual, Tab moves to the tags, and Shift+Tab moves back to the Title.
- The snippet list and the task list open under the Description, like the color list, not at the cursor. The color, snippet, and task lists work alike, and at most one is open: opening one closes the others. Down moves through the items and Enter chooses the one moved to. Enter with a list open and nothing moved to closes the list and then does what Enter does with no list.

The user decided that Up works the same in every popup list, the color, snippet, task, and "@" lists alike: while a list is open, Up moves to the item before, stopping at the first, and never moves the cursor in the field. With nothing moved to yet, Up does nothing. With no list open, Up moves the cursor as usual.

Proposed, each a decision to confirm:

- Items are chosen by position, so tasks with equal titles stay apart.
- The snippet list closes when nothing matches, when the cursor moves before the "/", or when the "/" no longer opens Editing's picker, such as after Backspace removes it. Choosing a snippet replaces the "/" and what was typed after it, even for the link, whose text is then empty.
- Snippet keys do nothing in the Title unless Editing says the snippet works in a Title.
- Empty or missing clipboard text counts as no web address for the link, so the link gets an empty address and no error shows.
- The task list shows every open task, newest first, until the user types; what is typed after it opens narrows it by Tasks' search. With no open task, or none matching, no list shows. Choosing a snippet that needs a task from the snippet list opens the task list, and choosing a task replaces the "/" and what was typed.

The reference to another task opens a list, as above. It lists open tasks only, and typing filters them with [Tasks' search](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case). The link's text is the chosen task's title with its Markdown kept, escaped as [Editing's reference snippet](../editing.md#snippets-insert-common-blocks) says; the list itself shows the title as a reader sees it, without its Markdown, which [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) mark.

A title's [mentions](#typing-an-at-sign-mentions-a-contact) show in the task list with their live text, or their stored text when Contacts has none, as in Task View's rows. Markdown can't put a link inside another link's text, so when the reference is inserted, each mention in the title becomes its live text, or its stored text, as its Markdown without the link around it. Later changes to the contact don't reach the reference, as later changes to the title don't. Proposed, each a decision to confirm. The rest of the title's text keeps its Markdown and its escapes. A mention's text is [Markdown](../contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) whose values are escaped, so in the reference a contact's name shows as written, such as `Ana *QA*` with its stars, and its template's styling shows, such as the alias in italic for `{fullName} (_{alias}_)`. A color shows as written, not as a color, since [Editing escapes its brackets](../editing.md#snippets-insert-common-blocks). The user decided how values and the reference are escaped; the reference keeping the mention's Markdown is proposed, a decision to confirm.

### Typing an at sign mentions a contact

The user decided that the "@" list shows each contact through a [mention template](../contacts.md#mention-templates-come-from-a-synced-file), that Tab moves to the next template, and that a mention in the editor shows as one chip with its [live text](../contacts.md#a-mentions-text-is-rebuilt-from-the-contact-each-time-it-shows), which Backspace and Delete remove whole and the cursor can't enter. Typing "@" where [Editing's mention list](../editing.md#typing-an-at-sign-opens-the-mention-list) opens, in the Title or the Description, shows contacts found by [Contacts' search](../contacts.md#search-matches-every-typed-word-in-any-field) with the text typed after the "@".

The user then asked for three changes: the list opens at the cursor, in a popup as menus do, not at the bottom of the field; the template shows once, in a header at the top, which a click moves to the next template as Tab does; and each row reads "**Full Name** (_alias_)". The user decided:

- **The header shows the contact moved to, in the current template**, such as "Ana Pop (EM)", so the user sees exactly what Enter inserts.
- **Each row shows the contact's full name in bold, then its alias in italics in parentheses**: "**Ana Pop** (_anap_)". A contact with no alias shows its ID in the alias's place, so every row names something the user can type.
- **Only the "@" list moves to a popup.** The color, snippet, and task lists stay under the Description, as [above](#every-formatting-command-and-snippet-has-a-key).

The user then decided how the list uses the theme, so it looks like the text it inserts:

- **The header shows its text as a mention chip**: on the theme's [mention background](../rendering.md#a-mention-shows-on-a-colored-background), in the normal text color, as the editor's chips show a mention, below. The user then asked that its Markdown show as styling, as a chip's does: for `{fullName} (_{alias}_)`, the alias shows in italic and no `_` shows.
- **Each row's alias shows in the theme's italic color**, [Rendering's text look](../rendering.md#public-interface) for italic text, in this list and in the [alias list](#a-plain-text-box-can-offer-contacts-by-alias) alike. The full name keeps the normal text color.
- **Both follow a switch between light and dark while the list is open**, through the platform's [theme follower](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).
- **The popup's surface keeps the Windows theme's colors**: its background, border, row highlight, and help text, as today.
- **The header and every chip are the Formatted view's [mention chip](formatted-view.md#public-interface)**, which a title's mentions use too, so a mention looks the same everywhere. The user decided this. The header's name, which screen readers read, is the text it shows, without Markdown characters.

The theme comes from the editor that owns the list, which already takes it, and from the theme the [alias list](#public-interface) is attached with. No public part changes.

Proposed, each a decision to confirm:

- **The list opens in a popup at the "@"**, as the [tag menu](#a-click-on-a-pill-opens-the-tag-menu) does, not inside the editor. Its top-left corner sits just under the line that holds the "@", lined up with the "@"'s left edge, so it stays put while the user types after the "@". In a tall Description, it opens beside the text being typed, wherever that is.
- **Near a screen's edge, the popup moves to stay whole on that screen's work area**: where it doesn't fit below the line, it opens above it, and where it doesn't fit to the right, it moves left until it fits. WPF's popup placement does both for a popup placed under a rectangle ([popup placement](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup-placement-behavior)).
- **The popup never takes the keyboard focus**: the cursor stays in the field, and typing still goes there and narrows the list.
- **It follows the "@" when the field scrolls**, and closes when the "@" scrolls out of the field's view.
- **It closes, leaving the text as it is**, when [Editing's query](../editing.md#typing-an-at-sign-opens-the-mention-list) answers nothing (the cursor moved to or before the "@", a line break lies between them, or the "@" is gone), when nothing matches, on Esc, when the field loses the keyboard focus, when another list opens, and when the window is deactivated, moved, resized, minimized, or hidden. A popup is a window of its own and doesn't move with its owner, so it closes instead of floating away.
- **Keys work as in the other lists**: Down moves to the next contact and Up to the one before, stopping at the first and the last; Enter chooses the contact moved to; Esc closes; a click on a row chooses it. Enter with nothing moved to closes the list and then does what Enter does with no list, as the [other lists](#every-formatting-command-and-snippet-has-a-key) do.
- **It shows up to seven contacts.** With nothing typed yet, the search matches everyone, so it shows the first seven by full name.
- **While no contact is moved to, the header shows the first row's contact.** It changes as Down and Up move, and when the template changes. Under it, in the help style, "Tab or click: next template" shows while there are two or more templates.
- **A click on the header moves to the next template**, exactly as Tab does. The cursor stays in the field, and the row moved to stays moved to.
- **The help style is the theme's tertiary text color**, `TextFillColorTertiaryBrush`, which the [tag menu](#a-click-on-a-pill-opens-the-tag-menu) already uses for its dimmed text. The Task editor sets it on the list's help parts itself. The user asked for a more discreet, more transparent color than the secondary one used before; the tertiary color is the Windows theme's next, fainter step. Proposed, a decision to confirm.
- **The "Tab or click: next template" line sits 5 pixels in from the list's left edge**, the user's choice, so it reads as a note under the header.
- **Tab, while the list is open, moves to the next template**, in the file's order, back to the first after the last, and the header shows its new text at once. Each opening starts at the first template, which is the one [used last](#the-list-starts-on-the-template-used-last). With one template, Tab and a click on the header do nothing, and Tab never leaves the field while the list is open.
- **Choosing a contact inserts the mention** Contacts makes from the contact and the current template, through Editing, as one undo step.
- **With no contacts, or no repository, no list shows**, and the "@" stays as typed.

The user asked that erasing back into a partial mention work as if the user had typed it: with `@ale` typed, erasing the "e" shows the list for `@al`, and so on, even when the list was closed. Proposed, each a decision to confirm:

- **An edit that only removes text, and leaves the cursor where a typed query would end, opens the list** for the "@" [Editing finds](../editing.md#typing-an-at-sign-opens-the-mention-list) before the cursor, with that query, at that "@". Backspace, Delete, Ctrl+Backspace, Cut, and an undo that only removes text all count. Typing over a selection is typing, not erasing, and works as typing does today.
- **Erasing back to a bare "@" opens the list with everyone**, as typing "@" does. Erasing the "@" itself closes it, as today.
- **It works after Esc, after the field lost the focus, and in a field that opened with text in it**, since typing the same text there would open the list. Esc closes it again.
- **Moving the cursor, by keys or a click, opens nothing**, as today.
- **It opens nothing while the color, snippet, or task list is open**, since one list is open at a time, and an "@" is then filter text for that list.
- **It opens nothing when a chip sits between the "@" and the cursor**, since the chip ended that mention, nor for an "@" inside a mention's Markdown shown as text.
- **Erasing while the list is open keeps updating it**, as today.

The chips work the same in the Title and the Description:

- **A mention shows as one chip**: its live text on Rendering's [mention background](../rendering.md#a-mention-shows-on-a-colored-background), with none of its link's Markdown showing. Its text's own Markdown shows as styling, as in a title: for `{fullName} (_{alias}_)`, the alias shows in italic and no `_` shows. When Contacts has no live text, the chip shows the stored text, formatted the same way. The chips show the new text when contacts change, without changing the task's Markdown.
- **The cursor steps over a chip as over one character**, and a click on a chip puts the cursor before or after it, whichever side is nearer. A selection takes a chip whole or not at all.
- **Backspace after a chip and Delete before it remove the whole mention**, as one undo step.
- **Saving keeps each mention's Markdown as it is**, stored text included, so the editor changes no mention the user didn't touch.
- **Copying a chip copies its Markdown**, so pasting it makes the same mention again.
- **A mention whose Markdown spans a line break shows as its Markdown, not a chip**, since a chip is one piece of one line. Contacts never writes one; only a hand-typed mention can. This is a decision to confirm.

#### The list starts on the template used last

The user asked that the "@" list start on the template used last, on every device that syncs, and decided that choosing a contact moves its template to the top of the [templates file](../contacts.md#mention-templates-come-from-a-synced-file), keeping the others in their order. Proposed, each a decision to confirm:

- **Enter, or a click on a row, moves the template first right after it inserts the mention**, through Contacts' `MoveTemplateFirst`, in the Title and the Description, in Add and in Task View alike. So the next opening starts on that template, and Tab goes through the others in their new order.
- **The move is kept when the task isn't saved**, as a [clicked tag color](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them) is, and Undo removes the mention but not the move.
- **Nothing else moves a template**: not Tab, a click on the header, or closing the list.

### A plain text box can offer contacts by alias

The user asked that "@" in Task View's [search box](task-list.md#search-narrows-the-list) insert the contact's alias. The Task editor publishes its "@" list for any plain WPF text box, so a contact is found the same way in both, and the popup, its rows, and its keys have one home. Proposed, each a decision to confirm:

- **It opens and closes as in the Title**: by Editing's rule for where "@" opens and what it searches with, at the "@", in a popup, with the same rows, keys, and closing rules. It opens when "@" is typed, and when an erase leaves the cursor at the end of a partial word such as `@ana`, [as in the Title](#typing-an-at-sign-mentions-a-contact). Moving the cursor back into a word such as `@anap` opens nothing. The erase rule is proposed, a decision to confirm.
- **It has no header and no templates**, since what it inserts isn't a mention. Tab, while it's open, closes it and then moves the focus as it would with no list.
- **Choosing a contact replaces the "@" and the text typed after it with "@" and the contact's alias**, or its ID when it has none, through Editing's [mention picker](../editing.md#public-interface), with the cursor right after it and no space added. An alias and an ID hold no spaces, so the result is one search word.
- **While it's open, it takes Down, Up, Enter, and Esc first**, so the box's owner sees them only while it's closed: in the search box, Down and Up then move through contacts, not to the [top or bottom row](task-list.md#search-narrows-the-list), and Esc closes the list before it [empties the search](task-list.md#esc-goes-back-one-step-then-closes-task-view). While it's closed, it takes none of them, so the box and its owner work exactly as with no list.

### Resting the pointer on a chip shows the contact's card

The user asked that resting the mouse pointer on any mention show a card with the contact's details, with the common details by default and every detail while Ctrl is held. The card itself, and what it shows, is the [Contact list's](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details), and the platform's [hover card](README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part) decides when it opens, where, and when it closes. The user decided that a chip whose contact Contacts can't find shows no card, since the chip already shows the stored text, as the [Contact list](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details) says. Proposed, each a decision to confirm:

- **Each chip in the Title and the Description shows the card** of the contact its address names, in Add and in Task View's edits alike.
- **A mention shown as Markdown, across a line break, shows no card**, as it's no chip.

### Closing without saving keeps a draft on this device

Closing Add without saving, with Esc or its close button, keeps its text as a draft. The next Add opens with the draft, and the Clear button clears it. Drafts stay on this device and never sync. Add never asks before it closes, since the draft keeps the text; Task View [asks](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) only before it drops a changed edit, which has no draft.

[Tasks keeps the draft](../tasks.md#a-draft-waits-on-this-device-until-its-saved), with its images, in `%LocalAppData%\Memoria-ToDo\draft\`, a folder the composition root gives it. Add keeps the draft when it hides and when the app exits, and saving the task clears it.

Add keeps the draft each time it hides, with every field empty meaning no draft: the kept draft is removed. Show restores the kept draft into all three fields with the cursor in the Title, and a draft whose task ID is already a task is deleted and Add opens empty. Clear empties the fields, deletes the kept draft and its images with [Tasks' `DiscardDraft`](../tasks.md#public-interface), starts a new task ID, and leaves Add open with the cursor in the Title. Clear is only the button's name: Tasks calls that step discard, so no public interface changes. A save that fails leaves the text, so Esc keeps it as the draft.

Proposed, each a decision to confirm:

- Add keeps the draft itself when its window closes as the app exits, so the composition root needs no extra call.
- After Clear, Add shows no message and no mark on the Title, as for a new task.
- Text typed in the tag box and not yet added as a tag isn't part of the draft, but the hidden window keeps it, so the next Show of the same window finds it. Clear and saving clear it.
- The draft keeps every tag in the order the pills show, so Show restores the same order and the same first tag.
- A save that fails with an unreadable-data error keeps Add open and shows the message, as for other failed saves. A save with no title marks the Title with a red border until it is edited.
- While a draft that can't be read is on disk and the fields are still empty, hiding leaves that file alone, so a window opened and closed by mistake doesn't destroy it. Typing something or saving replaces it, and Clear deletes it.
- If keeping the draft fails when Add hides, the error is not caught, so the failure is loud rather than a draft silently lost.

### A task's tags are pills in order, and a drag reorders them

The user decided that a task has several tags in an order they set, and that the first one is the one Task View [groups the task by](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description), as [Tasks](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) keeps it. The tag control shows them as a row of pills:

- **Each tag is a pill in its color, with an x at its end**, in the task's order, wrapping onto more lines when the row is full. A click on the x removes that tag; the next tag, if any, becomes the first.
- **The first pill shows 5% larger than a tag badge shows today, and every other pill 5% smaller**, so the user sees which tag groups the task. The user set both sizes, and decided that only the tag control shows the first tag larger: Task View's group headings keep today's badge size. Proposed, a decision to confirm: the text, the padding, and the x scale together.
- **After the tags, an add pill opens the tag menu**, and the tag chosen there is added at the end. The user decided that the add pill says "No tag" while the task has none, as today's pill does, and shows "+" once it has one, at today's badge size. Decided by the session, a decision to confirm: once the task has 15 tags, Tasks' [limit](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), the add pill is turned off, dimmed and skipped by Tab, so the user never meets the error.
- **A click on a tag's pill, outside its x, opens the tag menu to replace that tag** in its place, so changing the group tag keeps the others, as the user decided.
- **A tag the task already has is never offered and never added twice**, wherever it's chosen: from the menu, from Search, or in Custom. Tags compare once [normalized](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case).

The user asked that the user pick up a tag and drag it, with the list reordering as the pointer moves. Proposed, each a decision to confirm:

- **A press on a tag's pill that then moves farther than Windows' drag distance picks the pill up**, as the [desktop platform's](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) press-and-drag part tells a click from a drag. A press that doesn't move that far is a click. A press on the x, or on the add pill, never starts a drag.
- **While the pill is held, the other pills move aside as the pointer passes them**, so the row always shows the order a release would save, and the held pill shows where it would land. The sizes follow that order: whichever pill sits first shows larger.
- **Releasing the button drops the pill there.** The control raises one change only when the order differs from before the drag. A pointer outside the row, while the button is still down, keeps the place nearest it, so a drag never loses the pill.
- **Esc, or losing the mouse, such as when another window takes it, puts the pills back** in the order they had before the drag, and nothing changes.
- **The drag stays inside the control.** It uses the mouse held by the control, not Windows' drag and drop between windows, so no other window or field can take a tag.
- **Alt+Left and Alt+Right move the focused pill one place**, so the order can change without a mouse, as Alt+Up and Alt+Down [move a task](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete) in Task View. The user decided these keys.

The menu has three parts, as the + menu had:

1. **The eight most-used tags**, which a click chooses.
2. **Search**: a text box. The tags that match what is typed show in a submenu beside it, and a click on one chooses it.
3. **Custom**: a text box labeled **New tag:**, whose text follows [Tasks' tag rule](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) as it's typed, and Rendering's 30 [tag colors](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes), in the current theme's set. The colors show only while the box holds text, and a click on a color chooses the typed tag in that color.

[Tasks](../tasks.md#each-tags-color-is-kept-in-one-synced-file) keeps each tag's color, so every device shows it. [The menu](#a-click-on-a-pill-opens-the-tag-menu) and [Custom](#custom-adds-a-new-tag-in-the-color-clicked) are described below.

The user decided each of these rules, kept from the one-tag pill:

- **"No tag" has no x**, since there's nothing to clear.
- **A pill is one Tab stop, and its x is the next.** Enter, Space, or Down on a pill opens the menu. Enter or Space on the x removes that tag.

Proposed, each a decision to confirm:

- **The Tab order follows the pills' order**: each tag's pill, then its x, then the add pill. After a reorder, the moved pill keeps the focus.
- **A click on the x doesn't open the menu.** The focus moves to the pill now in the removed one's place, or to the add pill when none is.
- **"No tag" and "+" show in the theme's plain look**, not a tag color, so neither reads as a tag.

The pills and their menu make up the tag control. It's part of the editor, so Add gets it, and the Task list shows it under the [selected task's](task-list.md#tags-show-as-colored-badges-and-change-without-edit) description. Tags follow [Tasks' tag rules](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case): every tag is lowercase, with a hyphen where a space was.

The user decided that every tag is normalized, so a tag already in use shows normalized too: a task file that holds `Work` shows `work`. In the tag box, Custom's **New tag:** box, the text follows Tasks' rule as it's typed:

- **Typed and pasted text is rewritten by Tasks' [`TagName.Normalize`](../tasks.md#public-interface)** on every change, so typed letters show lowercase, whatever the Shift or Caps Lock state, and pasted `My  Work` shows `my-work`.

Decided by the session, each a decision to confirm:

- **A space typed at the end shows as a space until the next character is typed, which turns it into a hyphen**, so typing `to do` shows `to do` for a moment, then `to-do`. A space typed in an empty box, or right after another space, does nothing, since normalizing trims it or joins the run. Choosing the tag normalizes it once more, so a space left at the end is dropped.
- **The Search box keeps what is typed as it is.** Tasks normalizes the typed text before it [matches tags](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case), so `My w` finds `my-work`, and the box needs no rule of its own.

Proposed, each a decision to confirm:

- **A badge's text sits in its middle, across and down**, the user decided, and so does the pill's, with its x when it has one.
- **A color belongs to the tag, not to one task.** Choosing a color for a tag already in use changes it on every task, and on every device after a sync.
- **A tag with no color kept shows its default color**, which [Rendering](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) picks from the tag's normalized text, so a tag looks the same in every window and on every device. A color name this app doesn't know, such as one a newer app wrote, shows the default and is kept as it is.
- **A badge, and each pill with a tag, take the color's value and text color for the current theme**, from Rendering, and change when the theme or a tag's color does.
- **A clicked color is kept when its tag is chosen**, not when the task is saved, so the [draft](#closing-without-saving-keeps-a-draft-on-this-device) keeps its format. The color stays with the tag even if the task is never saved.

### A click on a pill opens the tag menu

Each pill is a drop-down button: a click on it outside its x, or Enter, Space, or Down while it has the focus, opens the menu under it, over the window, and a second click closes it. Opened from the add pill, the menu adds a tag at the end; opened from a tag's pill, it replaces that tag in its place. The pill's tag is remembered by name, not by place, so a sync that adds or moves tags while the menu is open still replaces that tag wherever it now sits. The user decided that when a sync removes the tag the menu was opened from, the menu closes and nothing is replaced. The menu is drawn in the theme's menu colors, copying none. The user decided it's a WPF [Popup](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup) under the pill, holding the Task editor's own items, with a second Popup beside Search for the matches. Keys pressed inside a popup still travel up to the window, since a popup's root passes routed events to the popup's parent ([PopupRoot.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/PopupRoot.cs)). So Esc, the [Close window shortcut](#keys-save-close-and-move-between-fields), and the Save shortcut keep working with the menu open.

The user decided that the menu's top tags and Search's matches leave out the task's current tag, since choosing it would change nothing. Proposed, a decision to confirm: now that a task has several, they leave out every tag the task has, so the menu never offers a tag the task can't take twice.

Proposed, each a decision to confirm:

- **The top of the menu lists up to eight tags**: the tags in use other than the task's tags, in Tasks' most-used order, each as a badge in its color. A line separates them from Search. With no such tag, the menu starts with Search. Eight has one home in the Task editor's code.
- **Search is a text box labeled "Search:"**. Typing in it shows the tags that match in a submenu to the right of the Search item, or to its left when the screen has no room. The user decided which tags match: the tags in use that contain the typed text anywhere, other than the task's tags, so `ork` finds `work`. They're [Tasks' tag suggestions](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case). Matches show as badges in their colors, most-used first, and may include the top eight.
- **The submenu lists every match**, and scrolls when it's taller than the screen allows. With an empty Search box, no submenu shows. When nothing matches, it shows one dimmed line, "No matching tags", that can't be clicked.
- **Custom… closes the menu and opens [Custom](#custom-adds-a-new-tag-in-the-color-clicked)**, which then adds or replaces as the menu would have.
- **Choosing a tag from the menu or its submenu adds it, or replaces the pill's tag, and closes both**, and the focus goes to the chosen tag's pill. Search's text is never part of the task, the draft, or the editor's changes, and closing the menu drops it.
- **A click outside the menu closes it**, as Esc does.
- **Choosing a tag from the menu while Custom shows leaves Custom as it is**, with its typed text, so no typing is lost.

Keys in the menu, proposed, each a decision to confirm:

| Key | Where | What it does |
| --- | --- | --- |
| Up, Down | The menu | Move through the tags, Search, and Custom… Moving onto Search puts the cursor in its box. |
| Enter | A tag, or Custom… | Chooses the tag, or opens Custom |
| A letter or digit | The menu, outside Search | Goes into the Search box, so typing starts a search at once |
| Down, Up | The Search box, with matches | Move a highlight through the matches, while the cursor stays in the box; Up from the first match leaves none highlighted |
| Down, Up | The Search box, with no matches | Move to Custom…, or to the last tag |
| Enter | The Search box | Chooses the highlighted match, or the first match when none is; with no match, does nothing |
| Esc | Anywhere in the menu | Closes the menu and drops Search's text |

### Custom adds a new tag in the color clicked

Custom shows below the tag control, with the cursor in its text box, labeled **New tag:**. The user decided that the box's text follows Tasks' tag rule as [above](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them), that the colors show only while the box holds text, and that a click on a color chooses the typed tag with that color. The user also decided that Custom opens below the tag control, as today, and stays until a tag is chosen or Esc. So a click elsewhere can't lose the typed tag, and the Save button and shortcuts can choose its text.

Proposed, each a decision to confirm:

- **The box suggests nothing.** Search finds the tags in use, so two lists would offer the same tags.
- **The colors show as a grid, one column per hue and one row per shade**, below the box. They hide again when the box is emptied.
- **The grid marks the color the typed tag would get with Enter**: its kept color, or its default. So typing `work` shows what `work` looks like now.
- **A click on a color keeps that color for the tag, then adds the tag to the task**, at the end, or in place of the pill's tag when Custom came from a tag's pill, normalized. A tag already in use is recolored on every task, as [a color belongs to the tag](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them). Proposed, a decision to confirm: a typed tag the task already has is only recolored, and the task's tags stay as they are. Custom then hides, the box empties, and the focus goes to the tag's pill.
- **Enter in the box adds the typed tag with no new color**, as a click on a color does, so it keeps its kept color, or its default. That keeps choosing a tag fast from the keyboard and never writes `tags.json`.
- **Keys**: Tab moves from the box to the colors. In the colors, the arrow keys move, and Space or Enter chooses the tag in the color moved to. Esc hides Custom and drops what was typed there, before it closes Add. The user decided that Backspace in the empty box does nothing, since each x removes its tag, so one Backspace too many never removes one.
- **The Save button, the Save shortcut, and Ctrl+Shift+Enter add any text still in the box**, as Enter does, before they save, so no typing is lost. That text counts as a change, as typed tag text does today.
- **When a clicked color can't be kept, the tag isn't chosen.** Custom stays open with the typed text, and says why under the colors. With no clone folder, it says to choose the repository folder in Configure, then choose the tag again. Otherwise it says the tag wasn't chosen because its color couldn't be kept, followed by the reason, such as the file error. (Decision to confirm: the wording.) The message clears when the text changes or Custom hides.

### The editor styles Markdown in place, with no preview

Add shows Markdown source styled in place: bold looks bold, and headings, links, and code are styled. The styling comes from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no rendered preview and uses no web view, so it never waits for one. Task View's Edit Description has a [Preview](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab), which the Task list builds around the Description editor with its own web view; the editor itself still shows no preview.

Proposed, each a decision to confirm:

- Colored text shows in its palette color for the current [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), such as `:red[urgent]` in red.
- Markdown's own characters, such as `**` around bold text, show dimmed.
- Headings, links, code, and italic text take Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme) for the current theme: every heading in its color and at its size, a link underlined in the link color, code in its font and color on the faded background, and italic text in the italic color.
- A mention shows as one [chip](#typing-an-at-sign-mentions-a-contact), with its live text on Rendering's [mention background](../rendering.md#a-mention-shows-on-a-colored-background), in the normal text color; its link's Markdown doesn't show. The chips come from [Rendering's mention list](../rendering.md#public-interface), so the editor finds no links itself.
- Styling never delays typing: typed text shows at once, and its styling follows.

### A new task goes to the top

A saved new task goes to the top of the order, where the user sees it first in Task View. Tasks computes its rank, as [the desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists.

### Add and Task View save the same way, through the editor

The editors run every save themselves, so Add and the Task list's [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor) and [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row) save the same way. Their user passes the function that saves: Add passes [Tasks'](../tasks.md#public-interface) add, and the Task list passes Tasks' save of an edit. Each save takes three steps, each where the editor has the field it needs:

1. Tag text typed and not yet chosen is added to the task's tags, so no typing is lost. In Task View, the tag control sits outside the field editors, so the Task list takes this step through the control's `ChooseTypedText`, then passes the control's tags in the save it gives the editor.
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
- **The tag control**: a task's tags as pills in order, each with an x, and the add pill, with the menu a pill opens and the drag that reorders them, as [above](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them). It reports the task's whole list each time the user adds, removes, replaces, or reorders a tag, and keeps a clicked color through Tasks, but never saves the task. Its user can also close its open menu or Custom, and choose the text typed in Custom, so Task View's Esc and Save act on it as Add's do. The editor holds one, and the Task list shows one under the selected task's description, during an edit too. It also publishes the words "No tag", which the Task list's "No tag" group heading shows, so they have one home. Proposed: this home, as a constant on the tag control. This is a decision to confirm.
- **Tag badges**: tags as read-only badges in their colors, at today's size. The Task list shows one as each tag group's heading.
- **Two ports**, the clipboard and the image file dialog, which the desktop app implements beside its composition root, so the Task editor's tests run on [fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test).

Proposed: the editor and the two tag parts follow Tasks' changes, the tag colors' changes, and the theme only while they're on screen, so a closed window or a left edit holds no subscription, as the [Formatted view](formatted-view.md#it-shows-what-rendering-decides-never-markdown) does. Each shows the current tags, colors, and theme when it's made, and again when it's back on screen, such as an edit reopened by Undo. This is a decision to confirm.

[Rendering](../rendering.md#public-interface) and [Editing](../editing.md#public-interface) are plain functions, so the Task editor calls them directly. The Task editor reads a dropped or chosen image file through Shared's `IFileSystem`, which `Create` and the editor's constructor take last (decision to confirm), and keeps each image through [Tasks' `SaveImage`](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), for a draft or an open edit alike.

Step 4 of the [desktop delivery backlog](README.md#delivery-backlog) adds the editor's `Save`, `FocusTitle`, and `HasChanges`, which the Task list needs. Row 7 adds `IAddWindow.Close`. Row 8 adds Tasks' [tag colors](../tasks.md#tag-colors-are-a-capability-of-their-own) to `Create` and the editor's constructor, right after `tasks`, and replaces `TagChips` with `TagEditor`, `TagBadges`, and `TopTags`. Row 11 removes `TopTags`, since the Task list dropped the tag filter that used it; the tag menu keeps the eight most-used tags inside the Task editor, so the count still has one home. Row 11 also adds `TitleEditor` and `DescriptionEditor`, with `DescriptionEditor.SetImageWidth`, makes `TaskEditorControl` internal, and adds the platform's Save keys to `AddWindow.Create` as its new last argument, after the update status. Proposed: the two editors' members below. This is a decision to confirm.

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
    CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys, IPointerPosition pointer);
```

With the [mention list and chips](#typing-an-at-sign-mentions-a-contact), the user decided that the Task editor uses [Contacts](../contacts.md#contacts-is-a-shared-module-of-its-own). Proposed, a decision to confirm: Contacts' [`IContacts contacts`](../contacts.md#public-interface) joins `AddWindow.Create` right after `tagColors`, and the Title and Description editors' constructors right after `tasks`, so the "@" list searches them and the chips show live text. No other member changes. This is not implemented.

For the [chips' cards](#resting-the-pointer-on-a-chip-shows-the-contacts-card), the user decided that the Task editor may use the [Contact list](contact-list.md#public-interface), for its contact card, as the [module map](../modules.md#module-map) says. The editors already take Contacts and the theme, which the card needs. The user also decided that `AddWindow.Create`, `TitleEditor`, and `DescriptionEditor` take the platform's [pointer position](README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part), immediately before their logger, and pass it to each hover card. They reject a null pointer before wiring events.

Proposed, a decision to confirm, for the [popup "@" list](#typing-an-at-sign-mentions-a-contact) and the [alias list](#a-plain-text-box-can-offer-contacts-by-alias): the Task editor publishes the alias list, which the Task list attaches to its search box:

```csharp
namespace Memoria.ToDo.Desktop.TaskEditor;

/// The "@" list for a plain text box: typing "@" where Editing's mention list opens shows the contacts Contacts'
/// search finds, in a popup at the "@", and choosing one replaces the "@" and the text typed after it with "@" and
/// the contact's alias, or its ID when it has none. It has no header and no templates. Each row's alias shows in
/// theme's italic color, following theme switches.
public static class ContactAliasList
{
    /// From now on, box offers the list, and while it's open, Down, Up, Enter, and Esc reach the list before any other
    /// handler of the box or its ancestors. Throws ArgumentNullException for a null argument. Use it on the UI thread.
    public static void Attach(System.Windows.Controls.TextBox box, IContacts contacts, ITheme theme, ILogger logger);
}
```

The user decided that `ContactAliasList.Attach` takes a required `ILogger logger` last, so the alias list logs why it opened after an erase, as the editors do; Task View passes its own logger.

The user decided that `TitleEditor`, `DescriptionEditor`, and `TagEditor` each take a required `ILogger logger` as their last constructor argument, as every module's create method does, and null is refused with an `ArgumentNullException`. The editor that catches a failure it shows logs it, so a host such as the Task list doesn't log the same failure again.

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
        IClipboard clipboard, SaveKeys saveKeys, IPointerPosition pointer, ILogger logger);

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
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys,
        IPointerPosition pointer, ILogger logger);

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

/// A task's tags as pills in their colors, in order, each with an x that removes it, then an add pill that says
/// "No tag" while there are none and "+" otherwise, turned off at 15 tags. The first pill shows 5% larger than a badge,
/// the others 5% smaller. A click on a pill opens the menu, which adds a tag from the add pill or replaces the pill's
/// tag: the top tags other than the task's, a tag in use found by Search, or Custom: the New tag box, whose text
/// TagName.Normalize rewrites as it's typed, and the 30 tag colors, where a click on a color keeps that color through
/// tagColors and chooses the tag. Dragging a pill, or Alt+Left and Alt+Right on it, reorders the tags.
/// It never saves the task.
public sealed class TagEditor : UserControl
{
    /// The words the add pill shows when the task has no tag, and the Task list's "No tag" group heading.
    public const string NoTagText = "No tag";

    /// pointer gives the mouse's place during a drag, as the field editors' pointer does.
    public TagEditor(ITasks tasks, ITagColors tagColors, ITheme theme, IPointerPosition pointer, ILogger logger);

    /// The tags the pills show, in order; empty for "No tag". Setting it changes the pills, raises nothing, cancels
    /// a drag, and leaves an open menu or Custom as it is. Throws ArgumentNullException for null.
    public IReadOnlyList<string> TaskTags { get; set; }

    /// Raised once after the user adds, removes, replaces, or reorders a tag, once TaskTags holds the new list:
    /// every tag in order, an added one normalized as Tasks' WithTag returns it. Raises nothing when the list didn't
    /// change, such as a drag dropped where it started or a tag chosen that the task already has.
    public event EventHandler<IReadOnlyList<string>>? TagsChanged;

    /// Closes the innermost open part, a pill's drag first, which puts the pill back, then the menu, then Custom, dropping what was typed there, as Esc does in it.
    /// Returns false when none is open.
    public bool CloseEdit();

    /// Adds the text typed in Custom, as Enter there does, raising TagsChanged. Returns true when
    /// the text was added or Custom's box is empty, and false when the task already holds
    /// TagName.MaxPerTask tags: the text then stays in Custom and the message line says why.
    public bool ChooseTypedText();
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

Row 14 of the [delivery backlog](README.md#delivery-backlog) gave the tag control one tag, `TaskTag`, and one event, `TagChanged`, so one choice outside an edit makes one save and one Undo bar. The user named it `TaskTag`, not `Tag`, so it doesn't hide WPF's `FrameworkElement.Tag`. Row 14 also added `NoTagText`, the one home of the "No tag" words, and two members the Task list needs now that the tags [join an open edit](task-list.md#tags-changed-during-an-edit-save-with-the-edit): `CloseEdit`, which the user named, so Task View's Esc closes the menu or Custom before it leaves an edit, and `ChooseTypedText`, so the edit's save chooses Custom's text first, as Add's does.

Row 21 brings [several ordered tags](#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them). The user decided these declaration changes:

- **`TaskTags` and `TagsChanged` replace `TaskTag` and `TagChanged`**, holding and reporting the whole list, so every change, a reorder included, still makes one save and one Undo bar. The names keep row 14's reason for `TaskTag`.
- **The constructor takes `IPointerPosition`**, before the logger, as the Title and Description editors do, so UX tests drive a drag with the [fake pointer](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them). Add's `Create` and the Task list already hold one, so no other declaration and no wiring in the composition root changes.
- **`NoTagText`, `CloseEdit`, and `TagBadges` keep their declarations.** The user decided that `ChooseTypedText` returns whether it took the typed text, so Task View's save stops, keeping the edit and the typed tag, when the task already holds 15 tags.

Until row 21 is built, the code still has row 14's members.

## Failure modes

- **No clone folder is set**: Add stays open with the text, says to set the repository folder in Configure, and keeps the draft.
- **Saving fails**, for example because the task file can't be written: Add stays open with the text, shows the error, and keeps the draft.
- **Saving fails and Tasks can't undo it** (it throws an `AggregateException`): Add stays open with the text and the draft, and the editor says the save was incomplete, so the user checks the repository folder before saving again. The Task list's edit gets the same message. (Decision to confirm: the wording.)
- **The edited task was deleted**, on this device or another (Tasks throws `TaskNotFoundException`): the edit stays open with the text, and the editor says "This task no longer exists. Copy your changes before leaving the editor." (Decision to confirm: the wording.)
- **An image can't be read or saved**: nothing is inserted, and Add says why.
- **A clicked color can't be kept**, because there's no clone folder, `tags.json` can't be read or is newer, or the file can't be written: the tag isn't chosen, and Custom keeps the text and says why, as [above](#custom-adds-a-new-tag-in-the-color-clicked).
- **`tags.json` can't be read**: every badge shows its tag's default color, and the editor still opens and saves tasks.
- **The draft can't be read**: Add opens empty, says the draft couldn't be loaded, and leaves the draft file as it is.
- **A chosen contact's template can't be [moved first](#the-list-starts-on-the-template-used-last)**, because the templates file can't be read now, needs a newer app, or can't be written: the mention stays inserted, the editor says why where it shows a failed save, the failure is logged at Error, and the list keeps its order. Proposed, a decision to confirm.

## Ruled out

The options below were considered and rejected, grouped by decision.

- How a mention shows in the editor:
  - **Its Markdown, styled in place, with the brackets and address dimmed**: the user chose one chip that the cursor can't enter.
  - **Shift+Tab for the previous template too**: the templates are few, and Shift+Tab already means moving back a field.
  - **Refreshing each mention's stored text on Save**: every save would change mentions the user never touched, and the merge would then see edits on both sides.
  - **A dimmed "No title" in the "@" row of a contact with no title**: every row lines up, but the words add noise to a short list.
  - **A shared help style in the desktop platform**: one home for which theme color means help, but it changes the platform's public interface and the screens that use the color today.
- A task reference whose title holds a mention:
  - **Each mention becomes its stored text**: the reference may show details older than the title shows.
  - **Keeping the title's Markdown as is**: the reference would show the mention's raw Markdown inside the link.
- How the task list shows a title's mentions:
  - **The stored text**: the list and Task View could show one title two ways.

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
  - **Several tags per task, as badges with remove buttons and a + button**, with no order: the user asked that the order count, the first tag grouping the task, and that a drag reorder them.
  - **One tag control with a flag that turns its + off for the rows**: rows need only colored badges, so a separate read-only part is smaller than a flag no other user needs.
  - **Picking a color, which a second click unpicks, then adding the tag with Enter**: the user asked that a click on a color add the tag at once.
  - **Colors that show while Custom's box is empty**: the user asked that they show only once a tag is typed.
  - **Seven tags in the tag menu**: the user asked for eight.
  - **Suggestions under Custom's box, as it is typed**: Search finds the same tags in use, so the window would hold two lists of them.
  - **The + button on its own row, under the badges**: it would sit away from the tags it adds to, and take a row of its own.
  - **A WPF ContextMenu for the tag menu**, with Search's matches as child items: the window's keys, such as the Close window and Save shortcuts, wouldn't reach it, and items take the focus as the pointer passes, pulling the cursor out of the Search box.
  - **Today's inline panel for the tag menu**: it isn't a drop-down, and opening it grows Add and pushes the Task list's description down.
  - **Search finds only tags that start with the typed text**: the user asked that a tag containing the text anywhere match.
  - **Custom inside the drop-down, in place of the menu's items**: a stray click outside would close it and drop the typed tag, and Save couldn't add text from a closed menu.
- The tag control's interface for several tags:
  - **Separate events for an added, a removed, and a moved tag**: the Task list would rebuild the list from each event, and a replace would raise two, with two saves and two Undo bars.
  - **Keeping `TaskTag` and `TagChanged` beside a new list**: two members for one value, which must stay in step.
- How a drag reorders the pills:
  - **Windows' drag and drop through the platform's `IDragPointer`, as task rows use**: it lets a pill leave the control and land in another window or field, and the row can't reorder while the drag runs, since Windows' drag loop holds the mouse.
  - **A drop line between pills, with the row reordered only on release**: the user asked that the list reorder as the pointer moves.
- How the first tag stands out:
  - **A star or a "group" label on the first pill**: the user asked for the size difference.
  - **The group headings at the first tag's larger size too**: a heading shows one tag, so a size there tells nothing, and the size would no longer mark the first of several.
- What the add pill says:
  - **Always "+"**: an untagged task would show only a "+", with nothing saying it has no tag.
  - **Always "Add tag"**: wide, and it pushes the tags onto more lines.
- What a click on a tag's pill does:
  - **Nothing, with only the add pill opening the menu**: changing a tag would take a remove, an add, and a drag.
- How the keyboard reorders tags:
  - **A "Make first" item in the tag menu**: it only moves a tag to the front, and adds an item to every menu.
  - **No keyboard reorder**: a keyboard user couldn't change which tag groups a task without removing tags.
- What the tag box does with a space:
  - **A typed space does nothing, and pasted spaces are removed**, the earlier rule: `to do` would become `todo`, while Tasks turns the same text into `to-do`. The user chose hyphens.
  - **A typed space becomes a hyphen at once**: typing two spaces would show two hyphens, which Tasks' rule would never make, so the box would need its own copy of the rule.
  - **The box keeps the text as typed until the tag is chosen**: the user decided that typed letters show lowercase.
- What the add pill does at 15 tags:
  - **It stays on, and choosing a tag says it's one too many**: the user would pick a tag only to be told it can't be added.- The "No tag" pill:
  - **A dimmed x that can't be clicked**: a control that never works.
- The current tag in the menu:
  - **Shown with a check mark**: it would take one of the eight places, and a click on it would do nothing.
- How the keyboard reaches each pill and its x:
  - **One Tab stop per pill, where Delete or Backspace removes the tag**: a key the user must discover, and the x would be mouse-only.
- Backspace in Custom's empty box:
  - **It removes the last tag**: a tag the user meant to keep could vanish by accident.
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
- Where the "@" list shows (proposed, a decision to confirm):
  - **Under the field, as before**: the user asked for the list at the cursor, since a tall Description puts the list far from the "@".
  - **Following the cursor as the user types after the "@"**: the popup would jump sideways with each letter.
- How a plain text box gets the "@" list (proposed, a decision to confirm):
  - **A mode flag on one list, templates on or off**: a flag every key and row must check, against the [code design](../../../AGENTS.md#code-design) rules; the two lists share their parts instead.
  - **The list in the desktop platform**: the platform [never names a module](../modules.md#the-platform-serves-every-module-and-knows-none), and the list needs Contacts and Editing.
- What the "@" list's header shows:
  - **The template itself, such as `{fullName} ({careerPath.short})`**: users would read field names instead of the result.
  - **Both, the result with the template under it**: a taller header for little gain.
- What a row shows for a contact with no alias:
  - **The full name alone**: two contacts with one name and no alias would look alike.
  - **The title in the alias's place**: mixes two kinds of value in one place.
- Which lists move to a popup at the cursor:
  - **All four lists**: more work and more UX tests now, for a change the user asked only of the "@" list.
- What Up does while the color, snippet, or task list is open:
  - **It moves the cursor in the field, as before**: Up would move through the "@" list but not the others, so the lists would work differently.
- When a template moves first (proposed, a decision to confirm):
  - **When Tab or a click on the header reaches it**: browsing the templates would reorder the list.
  - **When the task is saved**: the editor would track the templates each task used, across drafts, and an unsaved task would forget them.
  - **Undoing the move with the mention, or taking the mention back when the move fails**: the user's edit would depend on a side feature.
- How the "@" lists use the theme:
  - **Only the Windows theme's colors, with a plain header and an alias in the normal text color**: the header wouldn't look like the chip Enter inserts.
- How the "Tab or click: next template" line is dimmed (proposed, a decision to confirm):
  - **The secondary text color at a lower opacity**: a color the Windows theme doesn't define, whose contrast nobody checked, against a fainter step the theme already has.
- Which edits open the "@" list again (proposed, a decision to confirm):
  - **Only Backspace and Delete**: Ctrl+Backspace, Cut, and undo would leave the same text but act differently.
  - **Moving the cursor too**: the list would pop up while the user only moves through text that holds a mention-like word.
  - **Not after Esc, until "@" is typed again**: the user asked that erasing work as typing the same text would, and typing `@al` opens the list.
- Where the one part that draws a mention as a chip lives:
  - **The Task editor formats the mention's Markdown itself**: no new dependency, but a second copy of how a styled stretch of text becomes WPF text, which the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) forbids: a change to how italic or code looks would need two edits.
  - **The desktop platform holds the chip**: the platform [never names a module](../modules.md#the-platform-serves-every-module-and-knows-none), and the chip needs Rendering's spans and text look.

## Open questions

None.
