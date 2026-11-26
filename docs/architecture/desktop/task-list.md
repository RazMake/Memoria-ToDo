# Task list: how the desktop app shows and finds tasks

Status: Decided

The Task list is the desktop app's Task View window. Its left side lists the tasks grouped by tag, where a click on a title selects the task, its checkbox checks it off, drag and drop reorders a group's tasks and the groups themselves, with a line showing where they land, and a click on a group's tag collapses or expands it; its right side shows the selected task's formatted description and tags, and turns into the Description editor on Edit Description, with a Preview tab where images resize. Edit Title edits a title in its row. A status bar at the bottom holds the Sync button, the app's version, and any new version from [Updates](updates.md).

## Behavior

### Tasks show grouped by tag, beside the selected task's description

The user asked for Task View in two sides, and for these rules:

- **The left side groups the tasks by tag.** Each tag is a group. A task shows in one group only: its first tag's, its [group tag](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), as the user asked, or "No tag" when it has none. Its other tags don't place it anywhere else. Each row shows a checkbox and the task's title, and takes the [title's name](formatted-view.md#a-titles-name-is-the-text-it-shows) as its own.
- **A click on the title selects the task, and only the checkbox checks or unchecks it.** Only the checkbox shows the hand pointer, the user decided; the title keeps the normal pointer. The user asked first for a title click that checks the task, then chose this so the right side has a clear way to pick its task.
- **Each row's checkbox lines up with the first line of its title**, however many lines the title wraps to.
- **Drag and drop sorts the tasks within a group.**
- **The right side shows the selected task's description**, formatted.
- **There's no Edit button.** Right-clicking a task opens a menu whose Edit Title edits the title [in its row](#edit-title-edits-the-title-in-its-row), and whose Edit Description turns the right side into the [Description editor](#edit-description-turns-the-right-side-into-the-editor).

The user decided that titles and descriptions show formatted, as a reader sees them, never as Markdown: no Markdown characters, such as `**` or `:red[`, show. The [Formatted view](formatted-view.md) shows both, so Add can later show a preview the same way:

- **Titles show as native Windows Presentation Foundation (WPF) text**, so they need no web view.
- **The selected task's description shows in the one web view**, which the Task list makes when Task View opens and releases when it closes. It stays on the right side, so selecting another task only gives it another page.
- **Links**: a web link opens in the default browser, as the [Formatted view](formatted-view.md#web-links-open-in-the-default-browser-and-other-links-are-reported) does. A link to another task selects that task, which the Task list tells by asking [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it). Every other link does nothing, as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.
- **Mentions**: a [mention of a contact](../contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) shows the contact's [current details](#mentions-show-each-contacts-current-details) on [Rendering's mention background](../rendering.md#a-mention-shows-on-a-colored-background), in titles and descriptions alike, through the Formatted view; clicking it does nothing. Resting the pointer on it shows the contact's [card](#resting-the-pointer-on-a-mention-shows-the-contacts-card).

The user decided:

- **Groups come in the tags' order from [Tasks](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips), which the user sets by [dragging a group](#drag-and-drop-moves-tasks-and-groups-and-a-line-shows-where-they-land), with a "No tag" group last** for tasks without a tag, so every open task shows. A tag never moved shows at the top, as a new task does, so before any move, groups come in the tags' alphabetical order. Adding, resolving, or retagging tasks never changes the order of the groups that show; a new group tag's group appears at the top. A tag that's never first in any open task has no group. Every device, and the Android app's tag chips, show the same order.
- **Within a group, tasks keep their rank order.** Rank stays one order for all tasks, as [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) keeps it, and the task file format doesn't change.

Proposed, each a decision to confirm:

- A group's heading is its tag's [colored badge](#tags-show-as-colored-badges-and-change-without-edit), and its rows sit [indented under it](#a-click-on-a-groups-tag-collapses-or-expands-it). A group with no task to show is hidden.
- **Rows show no tag badges**, since the group already names the tag, and the selected task's tags show on the right side.
- **A click anywhere on a row except the checkbox selects the task**, and so does a right-click. Selecting a task shows its description, with its [tag control](#tags-show-as-colored-badges-and-change-without-edit) under it.
- **A link in a title opens as a link**, and doesn't select the task.
- **The checkbox's middle sits at the middle of the title's first line**, at any text size.
- **Nothing is selected when Task View opens**, and the right side says "Select a task to see its description." It says that again when the selected task is deleted, hidden by search, or gone after a sync. A selected task with no description shows "No description." above its tags.
- **Task View opens wide enough for both sides**, and can't be made so narrow that either side becomes unreadable: it opens at 1100 by 720 pixels with the left side taking 68% of the width and the right side 32%, as the user decided, and is at least 680 pixels wide, with the left side at least 300 and the right side at least 340. A bar between the sides moves the split, by mouse only: the user decided it's never a keyboard stop, so it never shows a focus rectangle. The split isn't remembered; each open starts at 68% and 32%.
- **Task View remembers its size and place on this device**, across closing it and restarting the app, as the user asked. It reopens where it last closed, through the platform's [saved place](README.md#a-saved-place-reopens-a-window-where-it-was), which owns the file, the checks, and what happens when the place is no longer on a screen. The user decided:
  - It's kept for this Windows user on this PC, in `task-view-placement.json` in the device folder, `%LocalAppData%\Memoria-ToDo`, and never synced, since each PC has its own screens. Another PC, or another Windows user on this PC, keeps its own.
  - It's saved each time Task View closes, for any reason, including Exit and an update's restart.
  - A maximized Task View reopens maximized, and keeps the size it had before it was maximized for when it's restored. One closed while minimized reopens as it was before it was minimized.
  - The first open on a device, or after the saved place can't be read or is no longer on a screen, opens at 1100 by 720 on the pointer's screen, as before.
  - The split between the sides isn't kept, as above.
  - A copy run from a build folder uses the same device folder as the installed copy, so the two share Task View's place, as they share Auto update.
- **Each side scrolls on its own.** The description shows at its full height inside the right side, so the right side scrolls, not the web view.
- The web view starts in the background when Task View opens, and is released when it closes, so the tray app holds no browser while Task View is closed. Microsoft recommends creating WebView2 only for real web content, and no more instances than needed ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)).
- A link to a deleted task shows "This task no longer exists." in the message line. A link to a resolved task shows the resolved tasks and selects it. A link to a hidden task clears the search if the search hides it, and [reveals its row](#a-click-on-a-groups-tag-collapses-or-expands-it) if its group is collapsed, then selects it. The left side scrolls to the task's row.
- When the browser can't open a web link, Task View shows the error in its message line, the same line where a failed move, resolve, or delete says why.
- The [Close window shortcut](configure.md#esc-and-the-close-window-shortcut-close-the-window), Shift+Esc at first, closes Task View from any control, even while a task is being edited, exactly as its close button does.

### Mentions show each contact's current details

The user decided that a mention's text is rebuilt from the contact's current fields each time it shows, as [Contacts](../contacts.md#a-mentions-text-is-rebuilt-from-the-contact-each-time-it-shows) says. Proposed, each a decision to confirm:

- **Task View swaps each mention's text before it shows a title or a description**, through [Rendering's mentions](../rendering.md#public-interface), with Contacts' live text for the mention's address and template. A mention Contacts has no text for keeps its stored text. The Formatted view then shows the result, and needs no contacts.
- **When contacts change, such as after a sync or a save in the Contacts window, Task View shows the rows and the selected description again** with the new text. An open edit keeps its text; its chips show the new text, as the [Task editor](task-editor.md#typing-an-at-sign-mentions-a-contact) says.
- **An edit's [Preview](#edit-description-has-an-edit-tab-and-a-preview-tab) swaps the draft's mentions too**, so it looks like the saved description will, and like the editor's chips. It shows again when contacts change, as the description does.
- **Search matches the task as saved**, so a plain word matches a mention by its stored text, not its live text, as [Contacts](../contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) says. A word that names a contact with "@" matches the [mentions of that contact](#search-narrows-the-list) instead, whatever their text.

### Resting the pointer on a mention shows the contact's card

The user asked that resting the mouse pointer on any mention show a card with the contact's details: the common ones by default, and every one while Ctrl is held. The [Contact list](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details) makes the card and decides what it shows, the [Formatted view](formatted-view.md#a-mention-shows-a-card-its-user-makes) finds the mention under the pointer, and the platform's [hover card](README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part) decides when it opens and closes. The user decided that a mention whose contact Contacts can't find shows no card, as the [Contact list](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details) says. Proposed, each a decision to confirm:

- **Every mention Task View shows has a card**: in each row's title, in the selected task's description, in an edit's Preview, and on the chips of an open edit, which the [Task editor](task-editor.md#resting-the-pointer-on-a-chip-shows-the-contacts-card) shows.
- **Task View passes the Formatted view a function that makes the card** from the mention's address, so the Formatted view still needs no contacts.
- **A title inside a task reference shows no card**: its mentions became [plain text](task-editor.md#every-formatting-command-and-snippet-has-a-key) when the reference was inserted.
- **The card doesn't stop a click**: the card shows only while the pointer rests, so a click on the mention still selects the row as before, and closes the card.

### A click on a group's tag collapses or expands it

The user asked for these rules:

- **A group's rows sit indented under its heading**, so the heading reads as the group's title.
- **A click on a group's tag badge collapses the group**, hiding its rows, and another click expands it. The badge shows the hand pointer. The "No tag" heading works the same way.
- **A collapsed heading's count is discreet**: "( 3 )", spaced as the user decided, sits centered vertically on the badge, in the theme's secondary text color, with only the number bold. So it reads as a note beside the tag, not a second title.

Proposed, each a decision to confirm:

- **A collapsed heading shows how many of its tasks it hides**, such as "( 3 )", so a collapsed group isn't mistaken for an empty one.
- **Every group starts expanded when Task View opens.** The user decided that a collapsed group stays collapsed only until Task View closes: nothing stores it, so no file, format, or other device changes.
- **Collapsing a group keeps the selection**, and the right side still shows the selected task, even when its row is hidden.
- **Up and Down skip a collapsed group's rows**, and Alt+Up and Alt+Down move a task only among rows that show.
- **A link to a task in a collapsed group reveals its row**, as the link rule [above](#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) says, and a search keeps each group as the user left it.
- **Only the mouse collapses a group.** Rows stay the keyboard's only stops in the list, so headings get no key.

The user decided how Task View reveals a task whose group is collapsed, such as for a link to it: it expands the task's group, so its row shows. Now that a task shows in [one group only](#tasks-show-grouped-by-tag-beside-the-selected-tasks-description), there's no other group to choose.

The user decided that collapsing the group whose row holds an open [title edit](#edit-title-edits-the-title-in-its-row) keeps the edit and its text: the edit stays open, hidden with its row, and shows again, with its text, when the group expands. Proposed, each a decision to confirm:

- **Every open title edit stays open when its group collapses**, changed or not, so one rule covers both.
- **A hidden title edit ends only as a shown one does**: Esc, starting another edit, or selecting another task leaves it, so Esc with nothing else open leaves it, without an Undo bar, as for any title edit.
- **Putting the cursor back in a hidden title edit expands its group first**, such as the close prompt's Cancel, or its Save when the title is empty or the save fails.

### Edit Description turns the right side into the editor

Edit Description, in a task's right-click menu, selects the task and switches the right side to the Task editor's Description editor, the same Description field as Add's, from the [Task editor](task-editor.md#public-interface). The user decided that only the description is editable: the title stays as it shows in the row. The user decided that the [tag control](#tags-show-as-colored-badges-and-change-without-edit) stays live beside the edit and [joins it](#tags-changed-during-an-edit-save-with-the-edit): a tag change during the edit saves or drops with the edit's text. Saving works as in Add, through the [editor's save](task-editor.md#add-and-task-view-save-the-same-way-through-the-editor), which passes Tasks' [save of an edit](../tasks.md#public-interface) with the task as the editor got it, the edited description, the title unchanged, and the tag control's tags.

The user decided that the [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window), Ctrl+Enter at first, saves the edit and leaves it; the user can change it in Configure. A saved edit shows the task's description again, formatted. Ctrl+Shift+Enter, which in Add saves and starts the next task, does nothing here. The user decided that when Ctrl+Enter isn't the Save shortcut, for example when Save is Ctrl+S, Ctrl+Enter in the Description types a line break like Enter: it doesn't save or leave the edit.

The user decided that Esc leaves the edit and abandons every change, after it closes any open list, and that leaving shows the same Undo bar as Delete when the edit had changes, as [Esc's steps](#esc-goes-back-one-step-then-closes-task-view) say. Closing Task View with a changed edit [asks first](#closing-task-view-asks-before-it-drops-a-changed-edit).

The user decided that the cursor starts at the end of the Description, which the menu item names, through the Description editor.

Proposed, each a decision to confirm:

- Selecting another task also leaves the edit, as Esc does.
- **Checking the task being edited saves the edit first**, as the Save shortcut does, then resolves the task, which hides at once. The user decided this. Proposed: when the save fails, the task isn't resolved, its checkbox clears, and the editor stays open with its message. This is a decision to confirm.

The user asked for a Save button in the edit, at the bottom right, with the Description reaching down to it. Proposed, each a decision to confirm:

- **A Save button sits at the bottom right of the right side while the Edit tab shows.** It saves as the Save shortcut does: a saved edit shows the formatted description again, and a failed save keeps the editor open with its message. The user decided it's the [Description editor's](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) own Save button, under its message line.
- **The editor fills the right side's height.** The Description stretches down to its message line and Save, below the tabs and the tag control, and the right side doesn't scroll as a whole while an edit is open; the Description scrolls inside itself. When the window is too short for the editor's smallest height, which the editor reports as its `MinHeight`, the right side scrolls.
- **The right side draws no focus outline**, though Ctrl+Tab and [Preview](#edit-description-has-an-edit-tab-and-a-preview-tab) give it the keyboard focus, so it's never framed in a light outline.
- Changes from other devices show in the list at once. An open edit keeps the user's text, and saving it merges with those changes as [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) decides.

Which discarded edits show that Undo bar, and what Undo keeps of the editor, follow the user's decision on the [editor's report of changes](task-editor.md#public-interface), and a [changed tag list](#tags-changed-during-an-edit-save-with-the-edit) counts too. Since Undo puts the edit back with its images, as the user decided below, the edit's images are deleted only when its Undo bar goes away or Task View closes.

The user decided what happens when an edit's images can't be deleted. Saving an edit moves the images waiting for the task into the clone, on any save that changes something, as [Tasks](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) decides, where [an image is deleted only with its task](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), so images left by a discarded edit must not wait for the next one:

- **The action still happens.** Leaving the edit, the Undo bar going away, or Task View closing goes ahead, and the message line says "The edit's images couldn't be removed: " followed by the reason. A failure while Task View closes shows the next time it opens, as a failed delete does. The images that couldn't be deleted stay where they wait, and nothing retries.
- **A new edit cleans up first.** Starting an edit of a task with Edit Description or F2 first finishes that task's discarded edit, if its Undo bar shows, then deletes any images an earlier edit of the task left. If that fails, the edit doesn't open, and the message line says why. An Undo bar for another task stays: editing never finishes another task's delete or discarded edit.
- **Undo deletes nothing.** It puts the discarded edit back with its images.

### Edit Description has an Edit tab and a Preview tab

The user decided that during Edit Description, the right side shows two small, discreet tab headers in the theme's colors, "Edit" and "Preview", above the edit:

- **Edit shows the Description editor**, as [above](#edit-description-turns-the-right-side-into-the-editor). An edit opens on Edit.
- **Preview shows the draft formatted**, as the description shows when no edit is open, through the same [Formatted view](formatted-view.md#it-shows-what-rendering-decides-never-markdown). It shows the draft as it is each time the user switches to Preview; it doesn't follow typing, since the user can't type while it shows.
- **Preview shows images added during the edit**, pasted, dropped, or picked, before Save. The user decided that the web view also serves those [waiting images](formatted-view.md#the-clones-address-also-serves-an-open-edits-waiting-images), so they show and resize like saved ones. The Task list does nothing for it: their links are the same before and after Save.
- **Images resize in Preview**, as [below](#an-image-resizes-by-dragging-its-corner-in-the-edits-preview), and nowhere else.
- **The Save shortcut saves the edit, and Esc abandons every change, in both tabs**, exactly as in Edit.

The Task list builds the tabs around the Task editor's Description editor, and shows Preview in the one web view it already has: Task View's description doesn't show during an edit, so the web view is free. The Task editor needs no Formatted view and no web view of its own, and the [module map](../modules.md#module-map) doesn't change.

Proposed, each a decision to confirm:

- **A click on a tab header switches to it, and so do Ctrl+Tab and Ctrl+Shift+Tab**, from anywhere in the edit, the preview's page included. The headers aren't keyboard stops, so they show no focus rectangle.
- **Switching to Preview moves the keyboard focus to the right side**, so the arrow keys and Page Up and Page Down scroll the preview. Switching back to Edit puts the cursor where it was.
- **Links in Preview do nothing**, as the [Formatted view](formatted-view.md#web-links-open-in-the-default-browser-and-other-links-are-reported) proposes: following a task link would leave the edit.
- **An empty draft's Preview says "No description."**, as a task with none does.
- **The tag control shows under both tabs**, and works in both.
- **Everything that puts the cursor back in the edit switches to Edit first**: Undo of a discarded edit, the close prompt's Cancel, and a save that fails.

### Edit Title edits the title in its row

The user asked for Edit Title, a new item in a task's right-click menu, which edits the title in place with the same abilities as Add's Title. The user decided that it edits in the row the user right-clicked, not on the right side, so the title is edited where it shows and the description stays in view. The row's title turns into the Task editor's Title editor, the same one-line field as Add's, with the cursor at the end of the title:

- **It works as Add's Title does**: Markdown styled in place, the [formatting keys](task-editor.md#every-formatting-command-and-snippet-has-a-key) that work in a Title, and pasted text with line breaks joined into one line.
- **The Save shortcut saves** the new title through Tasks' [save of an edit](../tasks.md#public-interface), with the description unchanged and the tag control's tags, and the row shows the title formatted again. The tag control on the right side [joins the title edit](#tags-changed-during-an-edit-save-with-the-edit), as it joins a description edit.
- **Esc abandons the change**, after it closes any open list.
- **An empty title isn't saved**: the field is marked, as Add marks it, and stays open.

Proposed, each a decision to confirm:

- **Enter saves too**, since a title is one line and Enter has nothing else to do in it, as renaming a file in Windows works.
- **Shift+F2 starts it** from the keyboard, and F2 keeps starting Edit Description.
- **Esc leaves with no Undo bar**: a title is one line, quickly typed again.
- **One edit at a time.** Starting Edit Title or Edit Description leaves an open edit as Esc does, and so does selecting another task or clicking another row.
- **The checkbox and the row's drag stay off while the title is edited**, so a click inside the field moves the cursor, and the task's other rows show the old title until it's saved.
- **A failed save keeps the field open with its text**, and the Title editor says why under the field, as the [editor's save](task-editor.md#add-and-task-view-save-the-same-way-through-the-editor) words each failure.
- **Closing Task View with a changed title** [asks first](#closing-task-view-asks-before-it-drops-a-changed-edit), as for a changed description.
- **Edit Title on another row of the task being edited moves the open edit to that row**, with its text, and the cursor goes there. A resolved task's title is edited in its own resolved row, which stays among the resolved tasks while it is edited.
- **Starting Edit Title leaves a description edit's Undo bar and its images alone**: a title edit leaves no images, so it has nothing to clean first, and Undo still brings the description edit back.

### Keys and the mouse reorder, resolve, and delete

The table lists each action on the selected task, by mouse and by key. Proposed: these actions and keys. This is a decision to confirm.

| Action | Mouse | Key |
| --- | --- | --- |
| Select a task | Click its title or row, or right-click it | Up, Down, with the focus in the left side |
| Edit Title | The right-click menu | Shift+F2 |
| Edit Description | The right-click menu | F2 |
| Save an edit | None | The Save shortcut, Ctrl+Enter at first; Enter also saves a title |
| Switch between an edit's Edit and Preview | Click the tab header | Ctrl+Tab, Ctrl+Shift+Tab |
| Collapse or expand a group | Click its tag | None |
| Move up or down within its group | Drag and drop | Alt+Up, Alt+Down |
| Move a group among the groups | Drag its heading | None |
| Resolve, or reopen a resolved task | Click the checkbox | Ctrl+D |
| Delete | The right-click menu | Delete |
| Search | Type in the search box | Ctrl+F, from anywhere in the window, even a row's open right-click menu, moves to the search box and selects its text. There, Down selects the top row and Up the bottom one. Up on the top row and Down on the bottom row go back to the search box |
| Sync | The Sync button in the status bar | F5 |

Also proposed, each a decision to confirm:

- The right-click menu holds Edit Title, Edit Description, and Delete, each with its key beside it, so rows hold no buttons.
- **Rows are the keyboard's stops, not their checkboxes.** Up and Down select the next row across group headings, skipping rows of collapsed groups, and Enter selects the focused row. The user decided that Up on the top row and Down on the bottom row move the cursor to the search box, with its text selected, and keep the selection.
- **Ctrl+F always searches in Task View**, the user decided, even when the user made it the [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window): it moves to the search box and leaves an open edit open. In a row's right-click menu, it closes the menu first. When an edit ends with the focus inside the editor, the focus goes back to the selected task's row.
- **The row keys work from the right side too**, the user decided. A click on the right side, such as on the description, gives it the focus, and then each row key in the table acts on the selected task as on its row. Proposed, each a decision to confirm: Alt+Up and Alt+Down move the task in its group; the keys act only while the right side itself has the focus and shows the selected task, so keys in the tag control and in an open description edit, its Preview included, keep their own meaning. When a collapsed group hides the selected task's row, its description still shows, and the user decided the keys still act on it: Shift+F2 expands the group to edit the title in its row, Alt+Up and Alt+Down do nothing, and the other keys act as usual.
- **The list itself is never a keyboard stop**, the user decided, so it never shows a focus rectangle around the whole list. Tab goes straight to a row. Proposed: a focused row shows only its selection, with no dashed focus rectangle of its own. This is a decision to confirm.
- Delete asks for no confirmation. The task disappears at once, and an Undo bar shows for about 10 seconds.
- The task's file is deleted when the Undo bar goes away or Task View closes. Deleting another task or discarding an edit while the bar shows finishes the earlier action at once, so one bar shows at a time. A delete that fails as Task View closes shows its message the next time Task View opens.
- Alt+Up and Alt+Down move the task one place among the tasks shown in the group whose row has the focus, and do nothing at the group's ends.
- Dropping a task on the upper half of a row puts it before that row's task, and on the lower half after it, between the tasks that group shows there.
- A task moves only within one group. A drop on another tag's group moves nothing and adds no tag, and a drop on the [resolved tasks](#resolved-tasks-wait-behind-a-count-at-the-bottom) moves nothing, so moving never resolves or reopens a task. Resolved tasks move among themselves.
- Dropping a task that was deleted meanwhile, such as by a sync, moves nothing and shows the same message as a link to a deleted task.

A moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors), between the two tasks it lands between in its group. Rank is one order for all tasks, and a task shows in one group, its first tag's, so the task file needs no order per tag. A task whose first tag changes keeps its rank, and shows in its new group among that group's tasks in rank order.

### Drag and drop moves tasks and groups, and a line shows where they land

The user decided that dragging a group's heading moves the whole group among the groups, and that every drag, of a task or a group, shows a line where the item will land. Only the mouse moves a group: headings stay off the keyboard's stops, as only the mouse [collapses a group](#a-click-on-a-groups-tag-collapses-or-expands-it).

A group's drag works this way:

- **A press on a heading that moves past Windows' drag distance starts a group drag.** It's the same distance a row's drag uses ([`SystemParameters.MinimumHorizontalDragDistance`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.minimumhorizontaldragdistance)). A release before that distance is a click, which collapses or expands the group as [above](#a-click-on-a-groups-tag-collapses-or-expands-it); a drag never collapses or expands it. The press may start on the badge or on a collapsed group's count.
- **A group's heading and its shown rows count as one item, split into halves, to pick the gap.** Over the item's upper half, the dragged group lands before that group; over its lower half, after it, as a task does over a row. A collapsed group is its heading alone. A long expanded group puts its two gaps far apart, so the line may sit well above or below the pointer.
- **"No tag" stays last and can't be dragged.** A press on its heading only collapses or expands it. A group dropped anywhere over "No tag" lands just before it, after the last tag group; no group lands after it.
- **The resolved tasks never move with the groups**: "N resolved" and its list stay at the bottom, and a group dropped there moves nothing.
- **A group moved while search hides some groups lands between the two shown groups it was dropped between**, wherever the hidden groups fall, as a task moved among a group's shown tasks does.
- **The move saves at once, in [Tasks' tag order](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips)**, with no Undo bar, as a task's move has none; dragging back undoes it. Other devices, and the Android app's tag chips, get the new order with the next sync.
- **Moving a group keeps everything else as it was**: the selection, which groups are collapsed, and an open title edit with its text.
- **The order changes only Task View's groups on the desktop.** The tag menu keeps its [most-used order](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case).

While a drag is over the list, a drop line shows where the item will land:

- **The line is thin, in the theme's accent color, `AccentFillColorDefaultBrush`, with a small circle at its start**, so it reads as a place between items, not a border. The Edit and Preview [tab headers](#edit-description-has-an-edit-tab-and-a-preview-tab) use the same accent, and it follows the theme.
- **The line sits in the gap the item would land in.** For a task, it's the gap between two rows of the task's own group, from the rows' indent to the list's right edge. For a group, it's the gap between two groups, just above the heading of the group it would land before, from the headings' indent. After the last tag group, it sits above "No tag", or under the last group when there's none.
- **The line and the drop come from one rule**, so a drop always lands where the line showed. A row's upper half still puts a task before it, and its lower half after it, as [above](#keys-and-the-mouse-reorder-resolve-and-delete).
- **Where a drop would move nothing, no line shows, and the pointer shows Windows' no-drop cursor.** A drop handler tells Windows which drops it accepts ([Drag and drop](https://learn.microsoft.com/en-us/windows/apps/develop/data/drag-and-drop)). For a task, that's over another group's rows, over a heading, over the resolved tasks from an open task or the open tasks from a resolved one, and over a gap next to the task itself. For a group, that's a gap next to the group itself, and the resolved tasks. Outside the list, no line shows.
- **The line takes no room.** Rows and headings don't move while it shows, so the place under the pointer never jumps. WPF draws such marks in an adorner layer, above the list and outside its layout ([Adorners overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/adorners-overview)).
- **The line goes away when the drop lands, the pointer leaves the list, or Esc cancels the drag.** WPF's drag cancels on Esc by itself ([Drag-and-drop overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop-overview)), so that Esc never closes Task View or leaves an edit.

The list scrolls during a drag, so a task or a group can go to a place off screen:

- **While the pointer is near the list's top or bottom edge during a drag, of a task or a group, the list scrolls that way.** The size of that edge band and the scrolling speed live in code.
- **The scrolling stops when the pointer leaves the edge band, the list reaches its end, the drop lands, or the drag is canceled.** The line follows the place under the pointer as the list scrolls.

### Esc goes back one step, then closes Task View

The user decided that Esc closes every window, one step per press, so one key press never loses the user's text, and Esc means "back one step" in Task View as it does in Add. Each press does the first of these that applies:

1. **It closes an open list or menu**, such as the editor's snippet picker, the search box's ["@" list](#search-narrows-the-list), the tag control's menu, or its Custom box and colors, which drop what was typed there, as in [Add](task-editor.md#a-click-on-a-pill-opens-the-tag-menu). The tag control's `CloseEdit` closes its menu first, then Custom, one per press.
2. **It leaves an edit**, dropping any tag change made in it, with its Undo bar when a description edit had changes, as [Edit Description](#edit-description-turns-the-right-side-into-the-editor) and [Edit Title](#edit-title-edits-the-title-in-its-row) say.
3. **It empties the search when the search box holds text**, from anywhere in the window, so every task shows again and Task View stays open. The user asked for this.
4. **It closes Task View**, as its close button does, so a pending Undo finishes, as above.

Closing from an edit takes two presses, and so does closing while the search holds text. The window closes through the desktop platform's [Esc part](README.md#window-modules-read-keys-through-the-platform), so every step before it marks the key handled. Esc never closes the window while an edit is open, so it never shows the [close prompt](#closing-task-view-asks-before-it-drops-a-changed-edit).

Proposed, each a decision to confirm:

- **Esc empties the search from anywhere in the window**, not only from the search box, since the search hides tasks wherever the focus is. An open edit ends first, so Esc during an edit keeps the search.
- **Esc doesn't clear the selection**: with no list, edit, or search text, it closes Task View, as the user's steps say.
- **Esc works while the description has focus.** The user decided that every key reaches the window, so Esc takes the same steps there as anywhere in Task View, and so do the function keys, such as F5 for Sync, and the Save shortcut and Ctrl+Tab in an edit's [Preview](#edit-description-has-an-edit-tab-and-a-preview-tab). Keys reach the window because the description's page [never holds the keyboard focus](formatted-view.md#the-page-never-holds-the-keyboard-focus): a click on it leaves the focus on the right side's pane. Its arrow keys and Page Up and Page Down scroll the pane.

### Closing Task View asks before it drops a changed edit

The user decided that when the Add new task shortcut [switches to Add](shell.md#a-shortcut-switches-between-add-and-task-view) while an [edit](#edit-description-turns-the-right-side-into-the-editor) has changes, Task View asks what to do with them. A [changed tag list](#tags-changed-during-an-edit-save-with-the-edit) is a change. Closing would otherwise lose the edit with no Undo, since the Undo bar closes with the window, and an edit in Task View has no [draft](task-editor.md#closing-without-saving-keeps-a-draft-on-this-device) as Add does. An edit with no changes, the common case, closes at once without asking. The prompt offers three buttons:

- **Save** saves the edit as the Save shortcut does. When it's saved, Task View closes, and Shell opens Add. When it isn't, because the save fails or the Title is empty, Task View stays open with the editor's message or the marked Title, and Add doesn't open.
- **Discard** leaves the edit, deletes its images as closing does, and closes Task View.
- **Cancel** keeps Task View open, with the edit as it was and the cursor back in the editor, and Add doesn't open.

Save is the default button, so Enter picks it. Esc and the prompt's own close button pick Cancel. Pressing the Add new task shortcut again while the prompt shows does nothing.

Proposed, each a decision to confirm:

- **The close button and Alt+F4 ask too**, with the same prompt, so every way the user closes Task View treats a changed edit alike. So do the window menu's Close and the taskbar's Close window. Esc never gets that far, as [above](#esc-goes-back-one-step-then-closes-task-view).
- **Exit in the tray menu and Windows signing out don't ask**: they discard the edit, as closing did before, so the prompt never blocks the app's exit or the end of a session. An open prompt closes as Cancel first, and Task View then closes with no error.
- **Task View asks only when Windows passes on the user's close command.** Windows sends a window that command ([`SC_CLOSE`](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand)) for its close button, Alt+F4, and its window menu's Close, and Windows Forms tells a user's close apart the same way ([CloseReason](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.closereason)). When the app exits, for Exit or for signing out alike, WPF closes each window with a plain close message instead ([Application.cs](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs#L1586-L1605), [Window.cs](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs#L2245)), so the exit never asks. Any other close without the command doesn't ask either. Shell's [Close Task View](#public-interface) asks as before, so Shell, the platform, and Task View's port stay as they are.
- **A UX test sends Task View the plain close message the exit sends**, with a changed edit open, and checks that the window closes without asking and the edit's images are deleted. The real Exit and signing out are still checked by hand at final validation: the user chose that rather than let the platform test host reference Task list. So is the taskbar's Close window, since no Microsoft page says which message it sends.
- **The prompt is a small modal dialog owned by Task View**, centered on it and following the theme, titled "Unsaved changes", with the text "Save your changes to this task?". A pending Undo bar finishes only once Task View really closes.

### Tags show as colored badges, and change without Edit

The user decided that Task View shows tags as colored badges wherever it shows them, in the colors the [Task editor](task-editor.md#a-tasks-tags-are-pills-in-order-and-a-drag-reorders-them) draws, and that a task's tags change without Edit. Proposed: where they show, now that rows sit in tag groups. This is a decision to confirm.

- **Each group's heading** shows its tag as a badge, at today's size, and rows show none. The "No tag" heading stays bold text, with the words the [Task editor publishes](task-editor.md#public-interface) for its add pill, so they have one home, as the user decided.
- **The selected task** shows the tag control under its description: the same pills, add pill, menu, and drag as Add, so the user adds, removes, replaces, and reorders tags without Edit. The first pill shows larger, so the user sees which tag places the task in its group. The user decided that during an [edit](#edit-description-turns-the-right-side-into-the-editor) the control still shows, a task with no tag shows the "No tag" pill, and a click opens the tag menu, but every change [joins the edit](#tags-changed-during-an-edit-save-with-the-edit) instead of saving.

The Task editor publishes both parts, so a tag looks the same in both windows, and the Task list owns no colors.

The user decided that outside an edit, an Undo bar follows each change: the same bar as Delete, for the same time. Undo saves the whole list the control showed before, in its order, the same way. The user decided that the bar's words name the change, found by comparing the list before and after:

| Change | The bar says |
| --- | --- |
| One tag added | "Tag added" |
| One tag removed | "Tag removed" |
| One tag replaced by another | "Tag changed" |
| The same tags in another order | "Tags reordered" |

The user's earlier words, "Tag changed" after a choice and "Tag cleared" after a clear, came from one tag per task: "Tag changed" keeps its meaning for a replace, and "Tag removed" takes over from "Tag cleared".

Proposed, each a decision to confirm:

- **The tag control shows for any selected task**, open or resolved. One task is selected at a time, so one control shows at a time. Selecting another task closes the control's menu, cancels a drag, and drops text typed in Custom.
- **Outside an edit, each change saves at once**, through Tasks' [save of an edit](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), with the task as the list shows it, and the same title, description, and the control's whole new list. Only the tags differ, so Tasks replaces only the tags, and a title or description another device changed meanwhile stays. A drag saves once, on the drop, never while the pill moves. Tasks needs no new part, and the save starts a sync as every local save does.
- **The pills change at once, and the groups follow when Tasks reports the change**: when the first tag changed, the task moves to its new first tag's group, or to "No tag", and stays selected, with the list scrolled to it, as the user decided. A change that keeps the first tag leaves the task where it is. A sync that changes the task's tags meanwhile, outside an edit, updates the pills, cancels a drag, and leaves an open menu or Custom as it is.
- **One bar shows at a time**, so a new tag change, a delete, or a discarded edit finishes the earlier bar and keeps its change. A tag change finishes it before it saves.
- **Starting an edit of the task finishes its tag-change bar** and keeps the change, so the editor starts from the saved tags. A bar for another task stays, as for a discarded edit.
- **A tag change outside an edit never cleans up the task's edit images** itself. Finishing a discarded edit's bar first deletes that edit's images, as any finished bar does, so Tasks' save, which moves [every waiting image](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), finds none of them.
- **A clicked color is kept by the tag control when its tag is chosen**, as in Add, so it stays with the tag even when saving the task then fails, or the edit it joined is discarded.

### Tags changed during an edit save with the edit

The user decided that while a [description edit](#edit-description-turns-the-right-side-into-the-editor) or a [title edit](#edit-title-edits-the-title-in-its-row) is open, the tag control joins that edit, so the text and the tags save or fail together:

- **Adding, removing, replacing, or reordering a tag only changes the pills.** Nothing saves, and no tag-change bar shows.
- **The edit's save sends the edited text and the control's tags together**, in one call to Tasks' [save of an edit](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), with the task as the edit started. Tasks replaces the tags only when the control's list differs from the one the edit started with, in order, once normalized, so tags another device set meanwhile stay when the user left the pills alone. When the save fails, nothing is saved: the edit stays open with its text and the control's tags.
- **Text still in Custom when the edit saves is added as a tag first**, through the tag control's `ChooseTypedText`, as Enter in Custom does and as Add's Save does. Nothing saves for it on its own; the edit's one save carries it. This holds for every way the edit saves: the Save shortcut, the Save button, the close prompt's Save, and checking the task.
- **Leaving the edit without saving drops the tag changes with the text**: Esc, the close prompt's Discard, selecting another task, or starting another edit. Undo of a discarded description edit puts back both its text and the control's tags.
- **A changed tag list is a change of the edit.** An edit has changes when its editor reports one, or when the control's list differs from the list the edit started with, in order, once normalized, so a reorder alone counts. So a description edit whose only change is its tags shows the Undo bar when discarded, and closing Task View asks first. The user decided that text typed in Custom and not chosen isn't a change of the edit, unlike in [Add](task-editor.md#custom-adds-a-new-tag-in-the-color-clicked): leaving an edit with no other change drops it without asking. Ruled out: nonblank Custom text counts as a change.
- **Esc closes the tag control's menu, then its Custom, before it leaves the edit**, through the control's `CloseEdit`, as [Esc's steps](#esc-goes-back-one-step-then-closes-task-view) say. Esc during a drag cancels the drag first, inside the control.

Proposed, each a decision to confirm:

- **A title edit left with Esc still shows no Undo bar**, even when its tags changed: tags, like a title, are quickly chosen again.
- **A sync during the edit doesn't change the pills**, as it doesn't change the edit's text. The save then keeps the synced tags unless the user changed the list, by [Tasks' tag rule](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it).

### Search narrows the list

[Tasks](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) decides how search matches tasks. The user decided that Task View has no tag filter, that the search box sits after a "Search:" label with no button, and that the list narrows as the user types.

The user also decided:

- **The search box has the focus when Task View opens.** Down there selects the top row, and Up the bottom row; the list scrolls to it, and the row takes the focus.
- **Task View's shortcut, while the window is open, brings it forward**, empties the search, and puts the cursor in the box, as [Shell](shell.md#a-shortcut-switches-between-add-and-task-view) says.
- **Ctrl+F works anywhere in the window**, an open editor too, and selects the box's text.

Proposed, each a decision to confirm:

- Search looks in titles, descriptions, and every tag of a task, first or not, so a word that matches only a task's second tag still finds it. Each group shows only its tasks that match, so a match shows in its first tag's group, and a group with none is hidden. An empty box shows every task.
- Opening Task View from the tray while it is open also empties the search, since the tray and the shortcut share the window's Show.
- The resolved count, below, counts only the resolved tasks that match the search.

The user asked that typing "@" in the search box insert the chosen contact's alias, and that the search then find every task that refers to that contact in its title or description. Proposed, each a decision to confirm:

- **"@" in the search box opens the Task editor's [alias list](task-editor.md#a-plain-text-box-can-offer-contacts-by-alias)**, where Editing's rule opens the "@" list: at the box's start, or after a character that isn't a letter or a digit. It looks and works like the editor's "@" list, without the template header. The search box's own Down and Up, to the top or bottom row, work only while the list is closed, and then exactly as before the list existed.
- **Choosing a contact puts "@" and its alias in the box**, or its ID when it has no alias, in place of the "@" and what was typed after it, and the list narrows at once.
- **A search word that starts with "@" names a contact when the rest of it finds one**, through Contacts' [Find](../contacts.md#public-interface), which looks up an alias first, then an ID, ignoring case. Typing `@anap` by hand works the same as choosing it.
- **Every word must still match**, as today: `@anap budget` finds the tasks that mention Ana and also hold "budget". Words without "@" match through [Tasks' search](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case), unchanged.
- **The search follows contact changes**: when contacts change, Task View matches the box's text again, so a word that now names a contact, or no longer does, takes effect without typing.

The user decided:

- **A word that names a contact matches only a task whose title or description holds a mention of that contact**: a mention whose address holds the contact's ID, ignoring case, whatever its stored text and template.
- **A word that starts with "@" but finds no contact matches as a plain word**, through Tasks' search, so nothing typed is ever ignored. So does "@" alone, and a name two contacts share, which [finds nobody](../contacts.md#a-name-two-contacts-share-points-to-nobody).
- **The Task list does this matching itself**: it splits off the words that name a contact, finds each task's mentions with [Rendering's mention list](../rendering.md#public-interface), reads each mention's ID with the platform's [contact address](../modules.md#a-contact-address-has-one-home), and passes the other words to Tasks' search. Tasks still knows no contacts. When the Android Task list needs the same matching, the second use moves it to a shared home, as the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) says.

### The search bar spans the window in its own shade

The user asked for a shorter search bar that stretches across the whole window, in a color slightly different from the window's, and decided the search bar is the whole band below. It's the platform's [search band](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), which the Contacts window shares, and the split between the sides is the platform's split panes. Proposed, each a decision to confirm:

- **The search bar is the band at Task View's top that holds the "Search:" label and the search box.** It runs from the window's left edge to its right edge.
- **Its background is the same theme resource as the [status bar's](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar)**, so the two bars match and follow the theme.
- **The user decided the search box is 180 pixels wide**, whatever the window's width.
- **The band is one row of controls tall**, with small padding.
- **The message line and the Undo bar stay under the band**, outside it. The Undo bar keeps its own card look. The message line is the platform's [message line](README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them), which Contacts shares.
- Proposed, a decision to confirm: the band's height is the search box's plus at most 12 pixels of padding.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden by default, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed, each a decision to confirm:

- "N resolved" at the bottom of the left side shows the resolved tasks, in one list in rank order, not in tag groups, and the count hides while it's 0. A resolved task's checkbox is checked, and a click on its checkbox reopens it.

The user decided that a checked task moves behind "N resolved" at once, with no Undo bar, as before the split; a click on its checkbox there reopens it.

### Task View syncs when it opens and on its Sync button

Opening Task View starts a sync without delaying the window, and its Sync button starts one at once, as
[Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.
Both use the shared service's manual-sync operation, which joins an active pass rather than starting another.
The opener owns and observes the opening request; window button/event handlers await their requests and handle
errors. Closing the window cancels its waits and detaches its status subscription, not the shared pass.
Background notices dispatch to the UI and never update a closed window. Existing search, pending undo, editor,
and web-view lifetime behavior stays unchanged.

**Decided by the user:** unexpected failures from Task View's opening, button, or F5 sync requests are observed
and reported in its existing failure line. The window remains usable; the failure never becomes an unobserved
Task fault or a success-shaped fallback. This adds no automatic retry.

Window-owned sync requests handle the service's
[`SyncNowAsync` disposal contract](../../../src/Shared/Sync/ISyncService.cs) quietly during shutdown, including
queued requests: service disposal is not a user-facing sync failure.

Proposed, each a decision to confirm:

- F5 also starts a sync.
- When a sync started from Task View fails, Task View shows the error next to the button. Other failures show only as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) decides.
- **The Sync button and its error sit at the start of [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar)**, which runs along Task View's bottom edge and always shows, so the bar Add shows is the same one. The bar's end shows a new version while one is available or updating; a click on "Update available" opens [What is new](updates.md#what-is-new-shows-each-newer-releases-notes) over Task View. An update closes Task View as its close button does, asking first about a changed edit. [Below](#a-status-bar-holds-the-sync-button-and-the-version) says what the bar holds and in what order.

### A status bar holds the Sync button and the version

The user decided that Task View has a status bar along its bottom edge. It's [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar), the one Add shows too, and holds, from left to right:

- **On its left, the Sync button is an icon button**: a sync symbol in blue, with no text, and a tooltip. The blue is the [palette's](../rendering.md#six-colors-read-well-in-light-and-dark-themes) blue for the theme, so it reads on both backgrounds and follows the theme.
- **A failed sync's error shows in the status bar, right of the button**, in the theme's error color, as [Configure](configure.md#the-window-shows-its-settings-in-labeled-sections) shows its messages, the user decided. A sync that succeeds clears it. While a sync runs, the button's tooltip says "Syncing…".
- **On its right, the app's version**, such as "Version 1.4.2". The user decided that it comes from the platform's [app version](README.md#the-apps-version-has-one-home), so `Create` takes no new argument for it.
- **Just left of the version, an icon that shows whether Prevent screen saver is on**, with a tooltip, as the user asked. [Keep awake](keep-awake.md#the-status-bar-shows-whether-its-on) publishes the state, and its design says what each state's tooltip says; the Task list draws the icon and its tooltip from the state. The user decided that only Task View's bar shows it, that it's a sun, and that it's not a button:
  - It's a sun, U+E706 from the icon font the Sync button uses, just left of the version: in the normal text color while on, muted while off or paused, and in the error color while it failed. The tooltip says which.
  - It's not a button and never a keyboard stop; Configure changes the setting. Screen readers read its tooltip as its name.
  - It follows the state as it changes, while Task View is open.
- **At its end, after the version, Updates' update text**, such as "Update available: 2.2.0", only while a new version is available or updating, as [Updates](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) decides. Otherwise the version ends the bar.

Proposed, each a decision to confirm:

- **The Task list makes one part for the bar's start**: the Sync button and its error on the left, and the version on the right, filling the width the update text leaves. It passes that part to Updates' status bar as the bar's start, so Updates' public interface doesn't change and the update text stays at the bar's end.
- The tooltip says "Sync (F5)" while no sync runs. The button's name for screen readers is "Sync".
- The status bar itself is never a keyboard stop; the Sync button is, after the list, and then the update text while it's a button.

The user asked how the button looks while a sync runs, and decided it combines the blue icon with a green working look. Proposed, each a decision to confirm:

- **The Sync button is compact**, with little padding around its icon, so it fits the one-line status bar. The icon comes from the icon font Windows ships, Segoe Fluent Icons on Windows 11 and Segoe MDL2 Assets on Windows 10, which share its code point ([Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font)), so it needs no new dependency. The icon is U+E895, and the button pads it by at most 8 pixels at each side and 3 above and below.
- **While any sync runs, the button turns green and is turned off.** That includes the sync Task View starts when it opens, the button's and F5's, and one Sync starts on its own, such as after a save. The green is the theme's success color, `SystemFillColorSuccessBrush`, so it suits the light and dark themes. The button keeps that bright green while it's off, instead of the theme's greyed-out look, and still tells assistive tools it's unavailable. Pressing it, or F5, starts no second sync.
- **"Syncing…" shows right after the button while a sync runs**, in the place of the error. When the sync ends, the icon turns blue again, and the place shows a failed sync's error, or nothing.

### An image resizes by dragging its corner in the edit's Preview

The user decided that an image in a description can be resized, and that its size stays with the task, as [Rendering](../rendering.md#an-image-keeps-the-width-the-user-gives-it) keeps it. The user decided that the user drags the image's corner, and that the [web view](formatted-view.md#a-resized-image-is-reported-not-saved) reports the new width.

The user decided that images resize only while the description is edited, in the edit's [Preview](#edit-description-has-an-edit-tab-and-a-preview-tab):

- **Only Preview shows resize corners.** The description Task View shows when no edit is open has none, and neither does a row, so Edit Title never meets one.
- **In Preview, each image has a resize corner**, at its bottom right, and dragging it changes the image's width. The height follows the image's proportions. That includes an image added during the edit and not saved yet.
- **A finished drag changes the draft, not the task.** The Task list writes the width into the edit's text through the Description editor, which uses Rendering's [width function](../rendering.md#public-interface). Nothing is saved: the Save shortcut saves it with the rest of the edit, and Esc abandons it with every other change.
- **The page stays script-free.** The corner is the browser's own CSS resize handle, and the Task list never reads the page.

Proposed: Preview doesn't show again after a drag, since its page already shows the new width; the next switch to Preview shows the draft with it. This is a decision to confirm.

## Public interface

- **Create Task View**: the composition root calls it when the app starts, with the parts Task View needs, and gives the result to Shell. The window opens only on Show Task View. It takes the Task editor's parts, since it builds the editor and the tag parts, and the [Formatted view's](formatted-view.md#public-interface) two ports: a function that makes a new description web view each time Task View opens, and the default browser.
- **Show Task View**: opens the window, or brings it forward. Shell calls it from the tray menu and when the app is started a second time. Proposed: it uses the desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show), which says where the window opens and what happens after the user closes it. This is a decision to confirm.
- **Close Task View**: closes the window as its close button does, and says whether it closed. A changed edit [asks first](#closing-task-view-asks-before-it-drops-a-changed-edit), and the window stays open when the user cancels, the save fails, or the prompt already shows. Shell calls it when the Add new task shortcut [switches to Add](shell.md#a-shortcut-switches-between-add-and-task-view), and opens Add only when it closed.

The split window, the tag groups, and Edit Description change none of these parts.

[`ViewTasksWindow.Create`](../../../src/Desktop/TaskList/ViewTasksWindow.cs) retains its existing arguments and
adds the shared `ISyncService` as its last argument. Row 7 of the [desktop delivery backlog](README.md#delivery-backlog) adds `Close` to its port. Row 8 adds Tasks' [tag colors](../tasks.md#tag-colors-are-a-capability-of-their-own) to `Create`, right after `tasks`, as the Task editor's `Create` takes them. There is no sync-less overload, test-only switch, or second service created by this module.

```csharp
namespace Memoria.ToDo.Desktop.TaskList;

public static class ViewTasksWindow
{
    /// Creates Task View. The window, and its description web view, are made only on IViewTasksWindow.Show.
    public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette,
        ITheme theme, TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync);
}

/// The Task View window, as Shell uses it.
public interface IViewTasksWindow
{
    /// Opens the window, or brings it forward.
    void Show();

    /// Closes the window as its close button does, first asking Save, Discard, or Cancel when an edit
    /// has changes, and returns once it's answered. True when the window is closed, or wasn't
    /// open; false when the user cancels, Save doesn't save, or the prompt already shows.
    bool Close();

    /// Whether the window is open now.
    bool IsOpen { get; }
}
```

Proposed: `Close` waits for the prompt's answer and returns it, rather than returning a task. The prompt is a modal dialog, which runs its own message loop while it waits, so Shell's shortcut action stays a plain call. This is a decision to confirm.

It also takes the platform's
[Close window keys](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows) as a new last argument, after
`ISyncService sync`, and attaches each window it makes to them.

The user decided that the Task list may use [Updates](updates.md), for its [status bar](#a-status-bar-holds-the-sync-button-and-the-version). Proposed: `Create` also takes Updates' [update status](updates.md#public-interface), `IUpdateStatus updates`, as its new last argument, after `closeWindowKeys`, and puts the status bar in each window it makes. This is a decision to confirm.

Row 11 of the [delivery backlog](README.md#delivery-backlog) adds the platform's [Save keys](README.md#the-save-shortcut-saves-in-any-window-that-edits) as `Create`'s new last argument, after the update status, as the user decided; Edit Description and Edit Title save on them. The version comes from a platform part, not an argument, and resizing an image adds an event to the Formatted view's [web view port](formatted-view.md#public-interface), so `Create`'s `createWebView` argument keeps its type:

```csharp
public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette,
    ITheme theme, TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
    Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync,
    CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys);
```

Row 13 of the [delivery backlog](README.md#delivery-backlog) adds Tasks' [tag order](../tasks.md#tag-order-is-a-capability-of-its-own), `ITagOrder tagOrder`, right after `tagColors`, as tag colors came in; Task View sorts its groups and saves a group's move through it. The drop line, a group's drag, and the scrolling during a drag change no other part. This is not implemented:

```csharp
public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, ITagOrder tagOrder,
    IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time, IClipboard clipboard,
    IImageFileChooser imageFiles, IFileSystem files, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser,
    ISyncService sync, CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer);
```

The user decided that Task View uses [Contacts](../contacts.md#contacts-is-a-shared-module-of-its-own), for [live mention text](#mentions-show-each-contacts-current-details) and the editors' "@" list. Proposed, a decision to confirm: Contacts' `IContacts contacts` joins `Create` right after `tagOrder`, and Task View passes it to the Title and Description editors, as the [Task editor](task-editor.md#public-interface) takes them. The user also decided that Task View's split bar and search band come from the [desktop platform](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), shared with the Contacts window, with no change in behavior; its drag parts moved there too, and the composition root passes the mouse as `Create`'s last argument, `IDragPointer pointer`, shown above. Contacts' argument is not implemented.

For the [search box's "@" list](#search-narrows-the-list) and the [contact cards](#resting-the-pointer-on-a-mention-shows-the-contacts-card), the user decided:

- **The Task list may use the [Contact list](contact-list.md#public-interface)**, for its contact card, as the [module map](../modules.md#module-map) says.
- **It attaches the Task editor's `ContactAliasList` to the search band's box**, which the platform's [search band](README.md#the-search-band-publishes-its-box-for-the-alias-list) publishes.

Proposed, each a decision to confirm:

- **It gives the Formatted view's title and description a card function** that calls the Contact list's `ContactCard.For` with Contacts, the theme, and the mention's address.
- **`Create` takes nothing new**: it already takes Contacts and the theme.

For the [Prevent screen saver icon](#a-status-bar-holds-the-sync-button-and-the-version) and the [saved size and place](#tasks-show-grouped-by-tag-beside-the-selected-tasks-description), proposed, each a decision to confirm:

- **The Task list may use [Keep awake](keep-awake.md#public-interface)**, as the [module map](../modules.md#module-map) proposes, and `Create` takes its status, `IKeepAwakeStatus keepAwake`, after `pointer`.
- **`Create` takes the device folder, `string deviceFolder`, after `keepAwake`**, and makes its [single window](README.md#a-closed-window-is-made-again-on-the-next-show) with the platform's [saved place](README.md#a-saved-place-reopens-a-window-where-it-was) for `task-view-placement.json` in it. Task View's file name lives in the Task list, which owns the window. `Create` fails when the folder isn't a full path, as Updates' does.
- **The logger stays last**, as [Logging](../logging.md#public-interface) says:

```csharp
public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, ITagOrder tagOrder, IContacts contacts,
    IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time, IClipboard clipboard,
    IImageFileChooser imageFiles, IFileSystem files, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser,
    ISyncService sync, CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer,
    IKeepAwakeStatus keepAwake, string deviceFolder, ILogger logger);
```

## Failure modes

- **The web view fails**, for example when the WebView2 Runtime is missing: the description shows as Markdown source, with a message saying why, as the [Formatted view](formatted-view.md#failure-modes) does. So does an edit's Preview, where no image can then resize. The rest of the list works.
- **A move, resolve, or delete can't be saved**: the task returns to how it was, and a message says why.
- **A group's move can't be saved**, for example because no clone folder is set, or `tags.json` can't be read, has a newer version, or can't be written: the group goes back to its place, and the message line says why, as Tasks' [failure modes](../tasks.md#failure-modes) give it. While `tags.json` can't be read, groups show in the tags' alphabetical order, and the list still loads.
- **A deleted task's images can't be removed**: the task's file is already deleted, as [Tasks](../tasks.md#public-interface) says, so the task stays gone. Proposed: the message line says "The task was deleted, but some of its images couldn't be removed: " followed by the reason. This is a decision to confirm.
- **Saving an edit fails**: nothing is saved, neither the text nor the control's tags. The editor stays open with the text, the control keeps its tags, and the editor shows the error, as the [editor's save](task-editor.md#add-and-task-view-save-the-same-way-through-the-editor) does.
- **An edit's images can't be deleted**: the action goes ahead and the message line says why, and a new edit of that task doesn't open until they're deleted, as the [user decided](#edit-description-turns-the-right-side-into-the-editor). A tag change outside an edit doesn't clean them up first, so its save moves them into the clone, where they sync with no link until the task is deleted. This is a known limitation, not fixed.
- **A tag change outside an edit can't be saved**, for example because the task's file can't be written or no clone folder is set: the pills go back to the task's saved tags, in their saved order, no Undo bar shows, and the message line says why, as the editor words each of Tasks' failures. When the task was deleted meanwhile, such as by a sync, it says "This task no longer exists." (Decision to confirm: the wording.)
- **Undoing a tag change fails**: the change stays, the bar goes away, and the message line says why.
- **A tag's color can't be kept**: the tag isn't chosen and nothing is saved; Custom says why, as in [Add](task-editor.md#failure-modes).
- **A new title can't be saved**: the title edit stays open with its text, and the Title editor says why.
- **Save in the close prompt doesn't save**: Task View stays open with the editor's message, and Add doesn't open.
- **A Tasks call from the list throws a failure the call doesn't document**: the list doesn't report it, since [each module reports only its expected failures](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), so the platform shows it and the app ends. That includes an `AggregateException`, which only adding a task and a sync's update throw, and no call from the list makes either.
- **Task View's saved place is missing, can't be read, has a newer version, or isn't on any screen now**, such as after a monitor was unplugged: Task View opens at its first size on the pointer's screen, as the [saved place](README.md#a-saved-place-reopens-a-window-where-it-was) decides, and nothing shows to the user. The next close saves over it.
- **Task View's place can't be saved when it closes**: Task View still closes, nothing shows, and its next open uses the previous saved place, or the first size. A Warning in the log says why.
- **Prevent screen saver failed**: the icon shows the error color, and its tooltip says why, as [Keep awake](keep-awake.md#failure-modes) gives it.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Where a task's description shows:
  - **One list in rank order, where the selected task expands in place**: the user wants the tasks grouped by tag, beside the description.
  - **A list only, with tasks opening in an editor window**: no rendered view.
- How a task is edited:
  - **An Edit button on the selected task**: the user wants Edit Description in a right-click menu instead.
- How many tasks are selected:
  - **Several at once**: a web view for each, and a right side that's harder to read.
- What happens when an edit's images can't be deleted:
  - **Say why and go on, with no cleanup before a new edit**: saving a later edit of the task would put the leftover images in the clone, where they sync.
  - **Stop until deleting works**, keeping the Undo bar and refusing to close: a lasting disk problem would block editing, deleting, and closing Task View.
  - **Retry on a timer, or delete every waiting edit image when the app starts**: either needs a new rule for how long an edit's images live, and Tasks shares that rule with the Android app.
- Unexpected sync-request failures:
  - **Terminate the app through its central fail-fast boundary**: the user chose an inline failure so an
    unsuccessful sync request does not end local task work.
- How Task View uses the tag control:
  - **Tags change only in an edit, and show as colored badges in the rows and the filter**: the user chose to tag a task without Edit.
  - **Only the edit uses the control, and the rows and the filter stay as they are**: a tag would look different in the list than in the editor, and colors would help least where the user scans most.
  - **Saving a tag change when the task collapses**: closing Task View or a sync meanwhile could lose it, and the change would need a pending state of its own.
  - **A new Tasks part that sets the tags**: Tasks' save of an edit already applies only tags that changed.
  - **No Undo for a tag change, since another click reverses it**: a slip on the x would be the only change in Task View with no Undo, and a tag no other task uses would be gone from the tag menu.
- What Esc does while a list or an edit is open:
  - **Close at once, after closing an open list**: an open edit would be lost with no Undo, since the bar closes with the window.
  - **Close at once, and ask about a changed edit as the shortcut does**: a key that means "back one step" would show a dialog, and leaving an edit would take the mouse or another key.
- When closing with a changed edit asks:
  - **Only for the Add new task shortcut**: the close button would still lose the edit that the shortcut protects.
  - **Also for Exit and Windows signing out**: the prompt could block the app's exit or the end of the session.
- How Task View tells the user's close from the app's exit:
  - **A flag the platform sets before the app exits, which Task View reads**: a new public platform part, for what Windows already tells each window.
  - **A Shell call that closes Task View without asking before the app exits**: it changes Task View's port, and it must run before Shell's first wait, or signing out races WPF closing the window.
  - **WPF's own exit state**: `Application`'s flag is internal, and `Dispatcher.HasShutdownStarted` turns true only after the windows have closed.
  - **Asking on every plain close message**: WPF's exit sends one, so Exit would show the prompt.
- How Close reports the prompt's answer:
  - **An asynchronous close that returns a task**: Shell's shortcut action, and every caller, would become asynchronous for one dialog, and a second press would still need its own guard.

- Order within a tag group:
  - **An order per tag, in each task's file**: the task file gains a field, and Tasks' merge and the Android app change.
  - **An order per tag, in a separate synced file**: a new format, merge rules, and Sync support for one feature.
- How the user selects a task:
  - **A click beside the title selects, and a click on the title checks**: a long title leaves only the row's edges to select with.
  - **Hovering selects**: the right side would change as the mouse crosses the list.
- What a checked task does next:
  - **It stays in place, struck through, until Task View closes**: the user kept today's behavior.
  - **It hides at once, with an Undo bar**: the user kept today's behavior.
- Where Edit Description puts the cursor:
  - **In the Title, as in Add**: the user would have to move to the Description first.
- The order of the tag groups:
  - **Most-used tag first**: groups would move as tasks are added and resolved.
  - **Always alphabetical**: the user asked to set the groups' order by dragging. Where the order is stored is ruled out in [Tasks](../tasks.md#ruled-out).
- Whether "No tag" moves:
  - **Like any group**: the stored order would need a reserved name no real tag may take, which both apps must know.
- Which part of the list picks the gap a dragged group lands in:
  - **Only headings, by their halves**: rows show no line during a group's drag, so there's less to aim at.
  - **Every group shows collapsed during a group's drag**: the gaps sit close together, but the list changes shape under the pointer when the drag starts.
- Moving a group by key:
  - **Ctrl+Alt+Up and Ctrl+Alt+Down move the focused row's group**: two more keys to learn and test; the user chose the mouse only.
- How the drop line looks:
  - **The target row's or heading's edge turns the accent color**: an edge drawn on one row can't show the gap after the last row as clearly.
  - **The rows part, leaving a gap the size of the dragged item**: rows move under the pointer during the drag, and it needs animation.
- Reaching a place off screen during a drag:
  - **No scrolling; the user collapses groups first**: a task couldn't reach a place off screen within a group taller than the list.
- What the Sync button shows:
  - **The word "Sync", alone or beside the icon**: a wider button in a one-line bar.
- What the search bar covers:
  - **The search box alone, on its own full-width row**: it adds a row, against a shorter bar.
  - **Only the band's width and shade, with the search box keeping its size**: the box stays small.
- Filtering by tag:
  - **Keeping the tag filter beside collapsing groups**: the user removed it; collapsing the groups the user doesn't want does its job.
- What Edit Description edits:
  - **The whole task, Title, Description, and tags, as in Add**: the user wants only the description editable there, and Edit Title for the title.
- The split bar and the list:
  - **Keeping the split bar a keyboard stop, to move the split with the arrow keys**: it showed a focus rectangle; the user wants none, so only the mouse moves the split.
- The Sync button:
  - **A text button at the top of the window**: the user wants an icon button in a status bar.
- How the status bar shows a sync:
  - **The error in Task View's message line**, the one move, resolve, and delete use: one place for every message, but it mixes a sync's lasting state with one-time action messages.
  - **A red icon on a failure, with the error in the tooltip**: the quietest bar, but the error needs a hover to read.
- Where the version comes from:
  - **The composition root passes it to `Create`**: every caller and test of `Create` would change for one string.
  - **Velopack's installed version**: it knows nothing when the app runs from a build folder, so the bar would show no version during development and in tests.
- When the search runs:
  - **Typing searches at once, as before**: the magnifier button would have nothing to start.
  - **Typing searches after a short pause**, and Enter or the button at once: fewer jumps, but a timer for little gain.
- How long a group stays collapsed:
  - **Kept on this device**: a new file in the app's local folder, for a view the user sets up again in one click.
  - **Kept in the repository, shared by every device**: a new field in `settings.json`, kept and merged by Tasks, that Android would also have to respect.
- Where Edit Title edits:
  - **On the right side, above the description**: the title would show twice while it's edited, and the right side would need a second editor layout.
- How the user resizes an image:
  - **A right-click menu on the image**, offering sizes such as Small, Medium, and Large: fewer choices than a drag.
  - **Typing the width in the description's source only**: the user must know the form, and the request asked for resizable images.
  - **Dragging the corner in the description shown outside an edit, saved at once with an "Image resized" Undo bar**: the user chose to resize images only while editing, where the width joins the draft.
- How the edit's Preview shows the draft:
  - **Live, following each typed character**: a new page at every key, for a tab the user can't type in.
- Who builds the Edit and Preview tabs:
  - **The Task editor's Description editor**: it would need the Formatted view and a web view, as the [Task editor](task-editor.md#ruled-out) says.
- What collapsing a group does to a title edit in it:
  - **The edit ends**: the user chose to keep the draft, so a click on a tag never loses typing.
- Which group a task with several tags shows in:
  - **Every tag's group, as before one tag per task**: the user asked that only the first tag place the task, so a task shows once.
- What shows of the tags during an edit:
  - **The tags as read-only badges, or a plain read-only "No tag" badge, with no x and no menu**: the user wants the tags changed during an edit, as part of it.
  - **The tags as badges, and nothing when there are none**: the same reason, and the row under the description would jump.
- How a tag change during an edit saves:
  - **At once, in its own save with its own Undo bar, while the edit's later save keeps it**: the text and the tags wouldn't save or fail together, and the two saves need rules so they can't undo each other.
  - **The edit's Save leaves Custom's text out, so it becomes a tag only by Enter or a color click**: typing in Custom would be lost on Save, unlike in Add.
- The tag-change Undo bar's text:
  - **"Tag changed" for every change**: the bar wouldn't say whether a tag is gone, new, or only moved.
  - **"Tags changed" for every change**: one rule and no comparison, but a slip of a drag and a slip of the x would read the same.
  - **A save on every step of a drag, each with its own bar**: one drag would leave many saves and bars, where the user made one change.
- What happens to a task's row when its first tag changes:
  - **The task moves, and the selection clears**: the tag control would disappear right after the user changed it, so a second change would need the task found again.
- What an edit's Preview shows of a mention:
  - **The stored text**: the Preview would show a mention's old text while the chip in the editor shows the new.
- What "@" in the search box inserts (proposed, a decision to confirm):
  - **A whole mention, as in the editor**: the box would hold Markdown the user can't read or edit.
  - **The full name**: it holds spaces, so it would split into words that match anyone sharing a first name.
- What a search word starting with "@" does when it finds no contact:
  - **It matches nothing**: the list would always empty while the user types.
  - **It's ignored**: a wrong name would silently search for less than the user typed.
- What a word that names a contact matches:
  - **Mentions, plus the contact's alias or full name written as plain text**: a full name such as "Ana Pop" can match text that isn't a mention, and the rule grows.
- Where the matching of "@" words lives:
  - **In Tasks' search, with a function for "@" words that the caller passes**: a Tasks interface change for one caller.
  - **In Rendering, as a function that says whether Markdown mentions an ID**: a search rule in the module that draws text, with no second caller yet.
- How Task View remembers its size and place:
  - **Saved also at the end of each move or resize**: a crash would keep the last place, but each drag would write a file while the user works.
  - **Keeping the split between the sides too**: the file and Task View's code would grow for a part the user didn't ask for.
- How the status bar shows Prevent screen saver:
  - **An icon that turns the setting on or off when clicked**: a click by mistake would change a setting that stays, where Configure shows it with its help line.
  - **The icon in Add's status bar too**: Add is a quick entry window, and the setting doesn't change while it's open.
  - **A moon, U+E708**: it reads as sleep, the opposite of what the feature does.
  - **Text, such as "Screen stays on"**: it takes room the update text and a sync error need.

Who shows formatted Markdown, and who opens links, is decided in the [Formatted view](formatted-view.md#ruled-out).

## Open questions

None.
