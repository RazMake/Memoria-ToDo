# Task list: how the desktop app shows and finds tasks

Status: Drafting

The Task list is the desktop app's Task View window. Its left side lists the tasks grouped by tag, where a click on a title selects the task, its checkbox checks it off, drag and drop reorders a group, and a click on a group's tag collapses or expands it; its right side shows the selected task's formatted description and tags, and turns into the Description editor on Edit Description, with a Preview tab where images resize. Edit Title edits a title in its row. A status bar at the bottom holds the Sync button, the app's version, and any new version from [Updates](updates.md).

## Behavior

### Tasks show grouped by tag, beside the selected task's description

The user asked for Task View in two sides, and for these rules:

- **The left side groups the tasks by tag.** Each tag is a group, and a task with two tags shows in both groups. Each row shows a checkbox and the task's title, and takes the [title's name](formatted-view.md#a-titles-name-is-the-text-it-shows) as its own.
- **A click on the title selects the task, and only the checkbox checks or unchecks it.** The title shows the hand pointer. The user asked first for a title click that checks the task, then chose this so the right side has a clear way to pick its task.
- **Each row's checkbox lines up with the first line of its title**, however many lines the title wraps to.
- **Drag and drop sorts the tasks within a group.**
- **The right side shows the selected task's description**, formatted.
- **There's no Edit button.** Right-clicking a task opens a menu whose Edit Title edits the title [in its row](#edit-title-edits-the-title-in-its-row), and whose Edit Description turns the right side into the [Description editor](#edit-description-turns-the-right-side-into-the-editor).

The user decided that titles and descriptions show formatted, as a reader sees them, never as Markdown: no Markdown characters, such as `**` or `:red[`, show. The [Formatted view](formatted-view.md) shows both, so Add can later show a preview the same way:

- **Titles show as native Windows Presentation Foundation (WPF) text**, so they need no web view.
- **The selected task's description shows in the one web view**, which the Task list makes when Task View opens and releases when it closes. It stays on the right side, so selecting another task only gives it another page.
- **Links**: a web link opens in the default browser, as the [Formatted view](formatted-view.md#web-links-open-in-the-default-browser-and-other-links-are-reported) does. A link to another task selects that task, which the Task list tells by asking [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it). Every other link does nothing, as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

The user decided:

- **Groups come in the tags' alphabetical order, ignoring case, with a "No tag" group last** for tasks without a tag, so every open task shows. A group stays where the user found it.
- **Within a group, tasks keep their rank order.** Rank stays one order for all tasks, as [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) keeps it, so two groups show their shared tasks in the same order, and the task file format doesn't change.

Proposed, each a decision to confirm:

- A group's heading is its tag's [colored badge](#tags-show-as-colored-badges-and-change-without-edit), and its rows sit [indented under it](#a-click-on-a-groups-tag-collapses-or-expands-it). A group with no task to show is hidden.
- **Rows show no tag badges**, since the group already names the tag, and the selected task's tags show on the right side.
- **A click anywhere on a row except the checkbox selects the task**, and so does a right-click. Selecting a task shows its description, with its [tag control](#tags-show-as-colored-badges-and-change-without-edit) under it. A task in two groups is selected in both, and both rows show it.
- **A link in a title opens as a link**, and doesn't select the task.
- **The checkbox's middle sits at the middle of the title's first line**, at any text size.
- **Nothing is selected when Task View opens**, and the right side says "Select a task to see its description." It says that again when the selected task is deleted, hidden by search, or gone after a sync. A selected task with no description shows "No description." above its tags.
- **Task View opens wide enough for both sides**, and can't be made so narrow that either side becomes unreadable: it opens at 1100 by 720 pixels with the sides split evenly, and is at least 680 pixels wide, with the left side at least 300 and the right side at least 340. A bar between the sides moves the split, by mouse only: the user decided it's never a keyboard stop, so it never shows a focus rectangle. Task View opens where [Single window](README.md#a-closed-window-is-made-again-on-the-next-show) places it and remembers neither its size, its split, nor its place, since remembering needs a saved setting that no design has yet.
- **Each side scrolls on its own.** The description shows at its full height inside the right side, so the right side scrolls, not the web view.
- The web view starts in the background when Task View opens, and is released when it closes, so the tray app holds no browser while Task View is closed. Microsoft recommends creating WebView2 only for real web content, and no more instances than needed ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)).
- A link to a deleted task shows "This task no longer exists." in the message line. A link to a resolved task shows the resolved tasks and selects it. A link to a hidden task clears the search if the search hides it, and [reveals its row](#a-click-on-a-groups-tag-collapses-or-expands-it) if every group it's in is collapsed, then selects it. The left side scrolls to the task's first row.
- When the browser can't open a web link, Task View shows the error in its message line, the same line where a failed move, resolve, or delete says why.
- The [Close window shortcut](configure.md#esc-and-the-close-window-shortcut-close-the-window), Shift+Esc at first, closes Task View from any control, even while a task is being edited, exactly as its close button does.

### A click on a group's tag collapses or expands it

The user asked for these rules:

- **A group's rows sit indented under its heading**, so the heading reads as the group's title.
- **A click on a group's tag badge collapses the group**, hiding its rows, and another click expands it. The badge shows the hand pointer. The "No tag" heading works the same way.

Proposed, each a decision to confirm:

- **A collapsed heading shows how many of its tasks it hides**, such as "(3)", so a collapsed group isn't mistaken for an empty one.
- **Every group starts expanded when Task View opens.** The user decided that a collapsed group stays collapsed only until Task View closes: nothing stores it, so no file, format, or other device changes.
- **Collapsing a group keeps the selection**, and the right side still shows the selected task, even when its row is hidden. A task in two groups still shows in its other, expanded group.
- **Up and Down skip a collapsed group's rows**, and Alt+Up and Alt+Down move a task only among rows that show.
- **A link to a task in a collapsed group reveals its row**, as the link rule [above](#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) says, and a search keeps each group as the user left it.
- **Only the mouse collapses a group.** Rows stay the keyboard's only stops in the list, so headings get no key.

The user decided how Task View reveals a task whose groups are all collapsed, such as for a link to it: it expands only the group of the row it reveals, the task's first row in group order, so one row shows and the task's other groups stay collapsed. When one of the task's groups is already expanded, nothing expands.

The user decided that collapsing the group whose row holds an open [title edit](#edit-title-edits-the-title-in-its-row) keeps the edit and its text: the edit stays open, hidden with its row, and shows again, with its text, when the group expands. Proposed, each a decision to confirm:

- **Every open title edit stays open when its group collapses**, changed or not, so one rule covers both.
- **A hidden title edit ends only as a shown one does**: Esc, starting another edit, or selecting another task leaves it, so Esc with nothing else open leaves it, without an Undo bar, as for any title edit.
- **Putting the cursor back in a hidden title edit expands its group first**, such as the close prompt's Cancel, or its Save when the title is empty or the save fails.

### Edit Description turns the right side into the editor

Edit Description, in a task's right-click menu, selects the task and switches the right side to the Task editor's Description editor, the same Description field as Add's, from the [Task editor](task-editor.md#public-interface). The user decided that only the description is editable: the title stays as it shows in the row, and the tags can't change until the edit ends. Saving works as in Add, through the [editor's save](task-editor.md#add-and-task-view-save-the-same-way-through-the-editor), which passes Tasks' [save of an edit](../tasks.md#public-interface) with the task as the editor got it, its title and tags unchanged.

The user decided that the [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window), Ctrl+Enter at first, saves the edit and leaves it; the user can change it in Configure. A saved edit shows the task's description again, formatted. Ctrl+Shift+Enter, which in Add saves and starts the next task, does nothing here. The user decided that when Ctrl+Enter isn't the Save shortcut, for example when Save is Ctrl+S, Ctrl+Enter in the Description types a line break like Enter: it doesn't save or leave the edit.

The user decided that Esc leaves the edit and abandons every change, after it closes any open list, and that leaving shows the same Undo bar as Delete when the edit had changes, as [Esc's steps](#esc-goes-back-one-step-then-closes-task-view) say. Closing Task View with a changed edit [asks first](#closing-task-view-asks-before-it-drops-a-changed-edit).

The user decided that the cursor starts at the end of the Description, which the menu item names, through the Description editor.

Proposed, each a decision to confirm:

- **The tags show as read-only badges during the edit**, with no remove buttons and no + button, so a tag change, which saves at once, never races the open edit's save.
- Selecting another task also leaves the edit, as Esc does.
- **Checking the task being edited saves the edit first**, as the Save shortcut does, then resolves the task, which hides at once. The user decided this. Proposed: when the save fails, the task isn't resolved, its checkbox clears, and the editor stays open with its message. This is a decision to confirm.
- Changes from other devices show in the list at once. An open edit keeps the user's text, and saving it merges with those changes as [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) decides.

Which discarded edits show that Undo bar, and what Undo keeps of the editor, follow the user's decision on the [editor's report of changes](task-editor.md#public-interface). Since Undo puts the edit back with its images, as the user decided below, the edit's images are deleted only when its Undo bar goes away or Task View closes.

The user decided what happens when an edit's images can't be deleted. Saving an edit moves every image waiting for the task into the clone, where [an image is deleted only with its task](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), so images left by a discarded edit must not wait for the next one:

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
- **The tags' read-only badges show under both tabs.**
- **Everything that puts the cursor back in the edit switches to Edit first**: Undo of a discarded edit, the close prompt's Cancel, and a save that fails.

### Edit Title edits the title in its row

The user asked for Edit Title, a new item in a task's right-click menu, which edits the title in place with the same abilities as Add's Title. The user decided that it edits in the row the user right-clicked, not on the right side, so the title is edited where it shows and the description stays in view. The row's title turns into the Task editor's Title editor, the same one-line field as Add's, with the cursor at the end of the title:

- **It works as Add's Title does**: Markdown styled in place, the [formatting keys](task-editor.md#every-formatting-command-and-snippet-has-a-key) that work in a Title, and pasted text with line breaks joined into one line.
- **The Save shortcut saves** the new title through Tasks' [save of an edit](../tasks.md#public-interface), with the description and tags unchanged, and the row shows the title formatted again.
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
| Resolve, or reopen a resolved task | Click the checkbox | Ctrl+D |
| Delete | The right-click menu | Delete |
| Search | The magnifier button | Ctrl+F moves to the search box, and Enter there searches |
| Sync | The Sync button in the status bar | F5 |

Also proposed, each a decision to confirm:

- The right-click menu holds Edit Title, Edit Description, and Delete, each with its key beside it, so rows hold no buttons.
- **Rows are the keyboard's stops, not their checkboxes.** Up and Down select the next row across group headings, skipping rows of collapsed groups, and Enter selects the focused row. When an edit ends with the focus inside the editor, the focus goes back to the selected task's row.
- **The list itself is never a keyboard stop**, the user decided, so it never shows a focus rectangle around the whole list. Tab goes straight to a row. Proposed: a focused row shows only its selection, with no dashed focus rectangle of its own. This is a decision to confirm.
- Delete asks for no confirmation. The task disappears at once, and an Undo bar shows for about 10 seconds.
- The task's file is deleted when the Undo bar goes away or Task View closes. Deleting another task or discarding an edit while the bar shows finishes the earlier action at once, so one bar shows at a time. A delete that fails as Task View closes shows its message the next time Task View opens.
- Alt+Up and Alt+Down move the task one place among the tasks shown in the group whose row has the focus, and do nothing at the group's ends.
- Dropping a task on the upper half of a row puts it before that row's task, and on the lower half after it, between the tasks that group shows there.
- A task moves only within one group. A drop on another tag's group moves nothing and adds no tag, and a drop on the [resolved tasks](#resolved-tasks-wait-behind-a-count-at-the-bottom) moves nothing, so moving never resolves or reopens a task. Resolved tasks move among themselves.
- Dropping a task that was deleted meanwhile, such as by a sync, moves nothing and shows the same message as a link to a deleted task.

A moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors), between the two tasks it lands between in its group. Rank is one order for all tasks, so a task in two groups may also change place in its other group: dropping it between A and B puts it between A and B everywhere both show, and wherever that rank falls among the tasks of its other group. The user accepted this, so the task file needs no order per tag.

### Esc goes back one step, then closes Task View

The user decided that Esc closes every window, one step per press, so one key press never loses the user's text, and Esc means "back one step" in Task View as it does in Add. Each press does the first of these that applies:

1. **It closes an open list or menu**, such as the editor's snippet picker, the tag control's + menu, or its Custom box and colors, which drop what was typed there, as in [Add](task-editor.md#tags-show-as-colored-badges-and-the-plus-button-adds-one).
2. **It leaves an edit**, with its Undo bar when a description edit had changes, as [Edit Description](#edit-description-turns-the-right-side-into-the-editor) and [Edit Title](#edit-title-edits-the-title-in-its-row) say.
3. **It closes Task View**, as its close button does, so a pending Undo finishes, as above.

Closing from an edit takes two presses. The window closes through the desktop platform's [Esc part](README.md#window-modules-read-keys-through-the-platform), so every step before it marks the key handled. Esc never closes the window while an edit is open, so it never shows the [close prompt](#closing-task-view-asks-before-it-drops-a-changed-edit).

Proposed, each a decision to confirm:

- **Esc in the search box closes the window**, rather than clearing the search first. Task View is [made again](README.md#a-closed-window-is-made-again-on-the-next-show) each time it opens, so the search starts empty and hides nothing next time.
- **Esc doesn't clear the selection**: with no list or edit open, it closes Task View, as the user's three steps say.
- **Esc works while the description has focus.** The user decided that every key reaches the window, so Esc takes the same steps there as anywhere in Task View, and so do the function keys, such as F5 for Sync, and the Save shortcut and Ctrl+Tab in an edit's [Preview](#edit-description-has-an-edit-tab-and-a-preview-tab). Keys reach the window because the description's page [never holds the keyboard focus](formatted-view.md#the-page-never-holds-the-keyboard-focus): a click on it leaves the focus on the right side's pane. Its arrow keys and Page Up and Page Down scroll the pane.

### Closing Task View asks before it drops a changed edit

The user decided that when the Add new task shortcut [switches to Add](shell.md#a-shortcut-switches-between-add-and-task-view) while an [edit](#edit-description-turns-the-right-side-into-the-editor) has changes, Task View asks what to do with them. Closing would otherwise lose the edit with no Undo, since the Undo bar closes with the window, and an edit in Task View has no [draft](task-editor.md#closing-without-saving-keeps-a-draft-on-this-device) as Add does. An edit with no changes, the common case, closes at once without asking. The prompt offers three buttons:

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

The user decided that Task View shows tags as colored badges wherever it shows them, in the colors the [Task editor](task-editor.md#tags-show-as-colored-badges-and-the-plus-button-adds-one) draws, and that a task's tags change without Edit. Proposed: where they show, now that rows sit in tag groups. This is a decision to confirm.

- **Each group's heading** shows its tag as a badge, and rows show none.
- **The selected task** shows the tag control under its description: the same badges, remove buttons, and + menu as Add, so the user adds or removes a tag without Edit. During an [edit](#edit-description-turns-the-right-side-into-the-editor), the tags show as read-only badges instead.

The Task editor publishes both parts, so a tag looks the same in both windows, and the Task list owns no colors.

Proposed, each a decision to confirm:

- **The tag control shows for any selected task**, open or resolved. One task is selected at a time, so one control shows at a time. Selecting another task closes the control's menu and drops text typed in Custom.
- **Each add or remove saves at once**, through Tasks' [save of an edit](../tasks.md#two-versions-of-a-task-merge-field-by-field), with the task as the list shows it, and the same title, description, and new tags. Only the tags differ, so Tasks applies only that tag's add or remove, and a change another device made meanwhile stays. Tasks needs no new part, and the save starts a sync as every local save does.
- **The badges change at once**, and the groups follow when Tasks reports the change: an added tag puts the task in that tag's group too, and a removed tag takes it out. A sync that changes the task's tags meanwhile updates the badges and leaves an open menu or Custom as it is.
- **An Undo bar follows each change**, saying "Tag added" or "Tag removed": the same bar as Delete, for the same time. Undo saves the opposite change the same way, so a removed tag comes back at the end of the task's tags. One bar shows at a time, so a new tag change, a delete, or a discarded edit finishes the earlier bar and keeps its change.
- **Starting an edit of the task finishes its tag-change bar** and keeps the change, so the editor starts from the saved tags. A bar for another task stays, as for a discarded edit.
- **A tag change first cleans up the task's earlier edit**, as starting an edit does: it finishes that task's discarded-edit bar, then deletes images an earlier edit left, since saving would otherwise move them into the clone. If deleting fails, the tag doesn't change, and the message line says why.
- **A picked color is kept by the tag control when its tag is added**, as in Add, so it stays with the tag even when saving the task then fails.

### Search narrows the list

[Tasks](../tasks.md#tags-and-search-ignore-case) decides how search matches tasks. The user decided that Task View has no tag filter, and that the search box sits after a "Search:" label, a bit shorter than before, with a magnifier button after it that runs the search. Enter in the box runs it too.

Proposed, each a decision to confirm:

- Search looks in titles, descriptions, and tags. Each group shows only its tasks that match, and a group with none is hidden.
- The magnifier button's tooltip says "Search (Enter)".
- The user decided that only Enter or the magnifier button searches: typing alone doesn't narrow the list, so the list never changes under the user mid-word. Searching with an empty box shows every task again.
- The resolved count, below, counts only the resolved tasks that match the search.

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
- While a sync runs, the button shows it's working, and pressing it again starts no second sync.
- When a sync started from Task View fails, Task View shows the error next to the button. Other failures show only as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) decides.
- **The Sync button and its error sit at the start of [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar)**, which runs along Task View's bottom edge and always shows, so the bar Add shows is the same one. The bar's end shows a new version while one is available or updating; a click on it starts the update, which closes Task View as its close button does, asking first about a changed edit. [Below](#a-status-bar-holds-the-sync-button-and-the-version) says what the bar holds and in what order.

### A status bar holds the Sync button and the version

The user decided that Task View has a status bar along its bottom edge. It's [Updates' status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar), the one Add shows too, and holds, from left to right:

- **On its left, the Sync button is an icon button**: a sync symbol in blue, with no text, and a tooltip. The blue is the [palette's](../rendering.md#six-colors-read-well-in-light-and-dark-themes) blue for the theme, so it reads on both backgrounds and follows the theme.
- **A failed sync's error shows in the status bar, right of the button**, in the theme's error color, as [Configure](configure.md#the-window-shows-its-settings-in-labeled-sections) shows its messages, the user decided. A sync that succeeds clears it. While a sync runs, the button's tooltip says "Syncing…".
- **On its right, the app's version**, such as "Version 1.4.2". The user decided that it comes from the platform's [app version](README.md#the-apps-version-has-one-home), so `Create` takes no new argument for it.
- **At its end, after the version, Updates' update text**, such as "Update available: 2.2.0", only while a new version is available or updating, as [Updates](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) decides. Otherwise the version ends the bar.

Proposed, each a decision to confirm:

- **The Task list makes one part for the bar's start**: the Sync button and its error on the left, and the version on the right, filling the width the update text leaves. It passes that part to Updates' status bar as the bar's start, so Updates' public interface doesn't change and the update text stays at the bar's end.
- The tooltip says "Sync (F5)" while no sync runs. The button's name for screen readers is "Sync".
- The status bar itself is never a keyboard stop; the Sync button is, after the list, and then the update text while it's a button.

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

## Failure modes

- **The web view fails**, for example when the WebView2 Runtime is missing: the description shows as Markdown source, with a message saying why, as the [Formatted view](formatted-view.md#failure-modes) does. So does an edit's Preview, where no image can then resize. The rest of the list works.
- **A move, resolve, or delete can't be saved**: the task returns to how it was, and a message says why.
- **A deleted task's images can't be removed**: the task's file is already deleted, as [Tasks](../tasks.md#public-interface) says, so the task stays gone. Proposed: the message line says "The task was deleted, but some of its images couldn't be removed: " followed by the reason. This is a decision to confirm.
- **Saving an edit fails**: the editor stays open with the text and shows the error, as the [editor's save](task-editor.md#add-and-task-view-save-the-same-way-through-the-editor) does.
- **An edit's images can't be deleted**: the action goes ahead and the message line says why, and a new edit of that task doesn't open until they're deleted, as the [user decided](#edit-description-turns-the-right-side-into-the-editor).
- **A tag added or removed without Edit can't be saved**, for example because the task's file can't be written or no clone folder is set: the badges go back to the task's saved tags, no Undo bar shows, and the message line says why, as the editor words each of Tasks' failures. When the task was deleted meanwhile, such as by a sync, it says "This task no longer exists." (Decision to confirm: the wording.)
- **Undoing a tag change fails**: the change stays, the bar goes away, and the message line says why.
- **A tag's color can't be kept**: the tag isn't added and nothing is saved; Custom says why, as in [Add](task-editor.md#failure-modes).
- **A new title can't be saved**: the title edit stays open with its text, and the Title editor says why.
- **Save in the close prompt doesn't save**: Task View stays open with the editor's message, and Add doesn't open.
- **A Tasks call from the list throws a failure the call doesn't document**: the list doesn't report it, since [each module reports only its expected failures](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), so the platform shows it and the app ends. That includes an `AggregateException`, which only adding a task and a sync's update throw, and no call from the list makes either.

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
  - **A new Tasks part that adds or removes one tag**: Tasks' save of an edit already applies only the tags that changed.
  - **No Undo for a tag change, since another click reverses it**: a slip on a remove button would be the only change in Task View with no Undo, and a tag no other task uses would be gone from the + menu.
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
- Which groups a reveal expands when all of a task's groups are collapsed:
  - **Every group of the task**: the user wants one row to show, not every group the task is in.

Who shows formatted Markdown, and who opens links, is decided in the [Formatted view](formatted-view.md#ruled-out).
