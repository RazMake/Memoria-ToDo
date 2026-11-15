# Task list: how the desktop app shows and finds tasks

Status: Decided

The Task list is the desktop app's View Tasks window: one list of open tasks in rank order, where a task expands in place to show its formatted description, its tags in color, and turns into the editor in place. Its Sync button syncs on demand.

## Behavior

### One list in rank order, with one task open at a time

View Tasks shows the open tasks in one list, in rank order. Each row shows a checkbox, the title, and the task's tags as [colored badges](#tags-show-as-colored-badges-and-change-without-edit), and takes the [title's name](formatted-view.md#a-titles-name-is-the-text-it-shows) as its own. Selecting a task expands it in place to show its formatted description; expanding another collapses the open one, so one web view serves every task.

The user decided that titles and descriptions show formatted, as a reader sees them, never as Markdown: no Markdown characters, such as `**` or `:red[`, show. The [Formatted view](formatted-view.md) shows both, so Add can later show a preview the same way:

- **Titles show as native Windows Presentation Foundation (WPF) text**, so they need no web view.
- **The open task's description shows in the one web view**, which the Task list makes when View Tasks opens and releases when it closes.
- **Links**: a web link opens in the default browser, as the [Formatted view](formatted-view.md#web-links-open-in-the-default-browser-and-other-links-are-reported) does. A link to another task expands that task, which the Task list tells by asking [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it). Every other link does nothing, as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

Proposed, each a decision to confirm:

- View Tasks opens 680 wide and 720 tall, in device-independent pixels, and can't be made narrower than 400. It opens where [Single window](README.md#a-closed-window-is-made-again-on-the-next-show) places it and remembers neither its size nor its place, so each time it's made again it starts at this size. Sizing the window to its list would make it jump as tasks expand, and remembering its size needs a saved setting that no design has yet.
- The web view starts in the background when View Tasks opens, and is released when it closes, so the tray app holds no browser while View Tasks is closed. Microsoft recommends creating WebView2 only for real web content, and no more instances than needed ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)).
- An open description shows at its full height: the list scrolls, not the description.
- A link to a deleted task shows "This task no longer exists." in the message line. A link to a resolved task shows the resolved tasks and expands it. A link to a hidden task clears the search if the search hides it, and the tag filter if the filter hides it, keeping whichever doesn't, then expands it.
- When the browser can't open a web link, View Tasks shows the error in its message line, the same line where a failed move, resolve, or delete says why.

### Edit turns the open task into the editor, in place

Edit switches the open task to the same editor as Add, from the [Task editor](task-editor.md#public-interface), in the same place in the list. Saving works as in Add, through the [editor's save](task-editor.md#add-and-view-tasks-save-the-same-way-through-the-editor), which passes Tasks' [save of an edit](../tasks.md#public-interface) with the task as the editor got it.

The user decided that Esc leaves the edit, after it closes any open list, and that leaving discards the change with the same Undo bar as Delete, as [Esc's steps](#esc-goes-back-one-step-then-closes-view-tasks) say. Closing View Tasks with a changed edit [asks first](#closing-view-tasks-asks-before-it-drops-a-changed-edit).

Proposed, each a decision to confirm:

- Collapsing the task, or opening another one, also leaves the edit, as Esc does.
- Ctrl+Enter and Ctrl+Shift+Enter both save and leave the edit, since there's no next task to start. A saved edit shows the task expanded, formatted.
- Changes from other devices show in the list at once. An open edit keeps the user's text, and saving it merges with those changes as [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) decides.

Which discarded edits show that Undo bar, and what Undo keeps of the editor, follow the user's decision on the [editor's report of changes](task-editor.md#public-interface). Since Undo puts the edit back with its images, as the user decided below, the edit's images are deleted only when its Undo bar goes away or View Tasks closes.

The user decided what happens when an edit's images can't be deleted. Saving an edit moves every image waiting for the task into the clone, where [an image is deleted only with its task](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), so images left by a discarded edit must not wait for the next one:

- **The action still happens.** Leaving the edit, the Undo bar going away, or View Tasks closing goes ahead, and the message line says "The edit's images couldn't be removed: " followed by the reason. A failure while View Tasks closes shows the next time it opens, as a failed delete does. The images that couldn't be deleted stay where they wait, and nothing retries.
- **A new edit cleans up first.** Starting an edit of a task with Edit or F2 first finishes that task's discarded edit, if its Undo bar shows, then deletes any images an earlier edit of the task left. If that fails, the edit doesn't open, and the message line says why. An Undo bar for another task stays: editing never finishes another task's delete or discarded edit.
- **Undo deletes nothing.** It puts the discarded edit back with its images.

### Keys and the mouse reorder, resolve, and delete

The table lists each action on the selected task, by mouse and by key. Proposed: these actions and keys. This is a decision to confirm.

| Action | Mouse | Key |
| --- | --- | --- |
| Expand or collapse | Click the row | Enter |
| Edit in place | Edit button | F2 |
| Move up or down | Drag and drop | Alt+Up, Alt+Down |
| Resolve, or reopen a resolved task | The row's checkbox | Ctrl+D |
| Delete | Delete button | Delete |
| Search | None | Ctrl+F |

Also proposed, each a decision to confirm:

- Delete asks for no confirmation. The task disappears at once, and an Undo bar shows for about 10 seconds.
- The task's file is deleted when the Undo bar goes away or View Tasks closes. Deleting another task or discarding an edit while the bar shows finishes the earlier action at once, so one bar shows at a time. A delete that fails as View Tasks closes shows its message the next time View Tasks opens.
- Alt+Up and Alt+Down move the task one place among the tasks shown in its group, and do nothing at the group's ends.
- Dropping a task on the upper half of a row puts it before that row's task, and on the lower half after it, between the tasks shown there.
- A task moves only within its group: an open task among the open tasks, and a resolved task among the [resolved tasks](#resolved-tasks-wait-behind-a-count-at-the-bottom). Moving never resolves or reopens a task, so a drop on the other group moves nothing.
- Dropping a task that was deleted meanwhile, such as by a sync, moves nothing and shows the same message as a link to a deleted task.

A moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors).

### Esc goes back one step, then closes View Tasks

The user decided that Esc closes every window, one step per press, so one key press never loses the user's text, and Esc means "back one step" in View Tasks as it does in Add. Each press does the first of these that applies:

1. **It closes an open list or menu**, such as the editor's snippet picker, the tag control's + menu, or its Custom box and colors, which drop what was typed there, as in [Add](task-editor.md#tags-show-as-colored-badges-and-the-plus-button-adds-one).
2. **It leaves an edit in place**, with its Undo bar when the edit had changes, as [Edit](#edit-turns-the-open-task-into-the-editor-in-place) says.
3. **It closes View Tasks**, as its close button does, so a pending Undo finishes, as above.

Closing from an edit takes two presses. The window closes through the desktop platform's [Esc part](README.md#window-modules-read-keys-through-the-platform), so every step before it marks the key handled. Esc never closes the window while an edit is open, so it never shows the [close prompt](#closing-view-tasks-asks-before-it-drops-a-changed-edit).

Proposed, each a decision to confirm:

- **Esc in the search box closes the window**, rather than clearing the search first. View Tasks is [made again](README.md#a-closed-window-is-made-again-on-the-next-show) each time it opens, so the search starts empty and hides nothing next time.
- **Esc doesn't collapse the expanded task**: with no list or edit open, it closes View Tasks, as the user's three steps say.
- **Esc works while the description has focus.** The description is a WebView2, which passes Esc on to the window as an accelerator key ([AcceleratorKeyPressed](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.acceleratorkeypressed), [WebView2.OnKeyDown](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.wpf.webview2.onkeydown)). No run has confirmed this yet.

### Closing View Tasks asks before it drops a changed edit

The user decided that when the Add new task shortcut [switches to Add](shell.md#a-shortcut-switches-between-add-and-view-tasks) while an [edit in place](#edit-turns-the-open-task-into-the-editor-in-place) has changes, View Tasks asks what to do with them. Closing would otherwise lose the edit with no Undo, since the Undo bar closes with the window, and an edit in place has no [draft](task-editor.md#closing-without-saving-keeps-a-draft-on-this-device) as Add does. An edit with no changes, the common case, closes at once without asking. The prompt offers three buttons:

- **Save** saves the edit as Ctrl+Enter does. When it's saved, View Tasks closes, and Shell opens Add. When it isn't, because the save fails or the Title is empty, View Tasks stays open with the editor's message or the marked Title, and Add doesn't open.
- **Discard** leaves the edit, deletes its images as closing does, and closes View Tasks.
- **Cancel** keeps View Tasks open, with the edit as it was and the cursor back in the editor, and Add doesn't open.

Save is the default button, so Enter picks it. Esc and the prompt's own close button pick Cancel. Pressing the Add new task shortcut again while the prompt shows does nothing.

Proposed, each a decision to confirm:

- **The close button and Alt+F4 ask too**, with the same prompt, so every way the user closes View Tasks treats a changed edit alike. So do the window menu's Close and the taskbar's Close window. Esc never gets that far, as [above](#esc-goes-back-one-step-then-closes-view-tasks).
- **Exit in the tray menu and Windows signing out don't ask**: they discard the edit, as closing did before, so the prompt never blocks the app's exit or the end of a session. An open prompt closes as Cancel first, and View Tasks then closes with no error.
- **View Tasks asks only when Windows passes on the user's close command.** Windows sends a window that command ([`SC_CLOSE`](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand)) for its close button, Alt+F4, and its window menu's Close, and Windows Forms tells a user's close apart the same way ([CloseReason](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.closereason)). When the app exits, for Exit or for signing out alike, WPF closes each window with a plain close message instead ([Application.cs](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs#L1586-L1605), [Window.cs](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs#L2245)), so the exit never asks. Any other close without the command doesn't ask either. Shell's [Close View Tasks](#public-interface) asks as before, so Shell, the platform, and View Tasks' port stay as they are.
- **A UX test sends View Tasks the plain close message the exit sends**, with a changed edit open, and checks that the window closes without asking and the edit's images are deleted. The real Exit and signing out are still checked by hand at final validation: the user chose that rather than let the platform test host reference Task list. So is the taskbar's Close window, since no Microsoft page says which message it sends.
- **The prompt is a small modal dialog owned by View Tasks**, centered on it and following the theme, titled "Unsaved changes", with the text "Save your changes to this task?". A pending Undo bar finishes only once View Tasks really closes.

### Tags show as colored badges, and change without Edit

The user decided that View Tasks shows tags as colored badges wherever it shows them, in the colors the [Task editor](task-editor.md#tags-show-as-colored-badges-and-the-plus-button-adds-one) draws, and that a task's tags change without Edit:

- **Each row** shows its task's tags as badges.
- **The tag filter** shows the seven most-used tags as badges, the same seven Add's + menu offers, and a list of all tags offers the rest.
- **The expanded task** shows the tag control: the same badges, remove buttons, and + menu as Add, so the user adds or removes a tag without Edit. During an [edit](#edit-turns-the-open-task-into-the-editor-in-place), the editor's own tag control takes its place.

The Task editor publishes all three parts, so a tag looks the same in both windows, and the Task list owns no colors.

Proposed, each a decision to confirm:

- **The tag control shows on any expanded task**, open or resolved. One task is expanded at a time, so one control shows at a time. Collapsing the task, or expanding another, closes the control's menu and drops text typed in Custom.
- **Each add or remove saves at once**, through Tasks' [save of an edit](../tasks.md#two-versions-of-a-task-merge-field-by-field), with the task as the list shows it, and the same title, description, and new tags. Only the tags differ, so Tasks applies only that tag's add or remove, and a change another device made meanwhile stays. Tasks needs no new part, and the save starts a sync as every local save does.
- **The badges change at once**, and the row and the filter follow when Tasks reports the change. A sync that changes the task's tags meanwhile updates the badges and leaves an open menu or Custom as it is.
- **An Undo bar follows each change**, saying "Tag added" or "Tag removed": the same bar as Delete, for the same time. Undo saves the opposite change the same way, so a removed tag comes back at the end of the task's tags. One bar shows at a time, so a new tag change, a delete, or a discarded edit finishes the earlier bar and keeps its change.
- **Starting an edit of the task finishes its tag-change bar** and keeps the change, so the editor starts from the saved tags. A bar for another task stays, as for a discarded edit.
- **A tag change first cleans up the task's earlier edit**, as starting an edit does: it finishes that task's discarded-edit bar, then deletes images an earlier edit left, since saving would otherwise move them into the clone. If deleting fails, the tag doesn't change, and the message line says why.
- **A picked color is kept by the tag control when its tag is added**, as in Add, so it stays with the tag even when saving the task then fails.

### Tags and search narrow the list

[Tasks](../tasks.md#tags-and-search-ignore-case) decides how search and the tag filter match tasks. Proposed, each a decision to confirm:

- One tag filters at a time, chosen from the [top tags](#tags-show-as-colored-badges-and-change-without-edit) or the list of all tags; choosing it again clears the filter.
- Ctrl+F searches titles, descriptions, and tags, and search combines with the tag filter.
- The resolved count, below, counts only the resolved tasks that match the search and the tag filter.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden by default, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed: "N resolved" at the bottom of the list expands the resolved tasks, in rank order, each with Reopen, and the count hides while it's 0. This is a decision to confirm.

### View Tasks syncs when it opens and on its Sync button

Opening View Tasks starts a sync without delaying the window, and its Sync button starts one at once, as
[Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.
Both use the shared service's manual-sync operation, which joins an active pass rather than starting another.
The opener owns and observes the opening request; window button/event handlers await their requests and handle
errors. Closing the window cancels its waits and detaches its status subscription, not the shared pass.
Background notices dispatch to the UI and never update a closed window. Existing search, pending undo, editor,
and web-view lifetime behavior stays unchanged.

**Decided by the user:** unexpected failures from View Tasks' opening, button, or F5 sync requests are observed
and reported in its existing failure line. The window remains usable; the failure never becomes an unobserved
Task fault or a success-shaped fallback. This adds no automatic retry.

Window-owned sync requests handle the service's
[`SyncNowAsync` disposal contract](../../../src/Shared/Sync/ISyncService.cs) quietly during shutdown, including
queued requests: service disposal is not a user-facing sync failure.

Proposed, each a decision to confirm:

- F5 also starts a sync.
- While a sync runs, the button shows it's working, and pressing it again starts no second sync.
- When a sync started from View Tasks fails, View Tasks shows the error next to the button. Other failures show only as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) decides.

## Public interface

- **Create View Tasks**: the composition root calls it when the app starts, with the parts View Tasks needs, and gives the result to Shell. The window opens only on Show View Tasks. It takes the Task editor's parts, since it builds the editor and the tag parts, and the [Formatted view's](formatted-view.md#public-interface) two ports: a function that makes a new description web view each time View Tasks opens, and the default browser.
- **Show View Tasks**: opens the window, or brings it forward. Shell calls it from the tray menu and when the app is started a second time. Proposed: it uses the desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show), which says where the window opens and what happens after the user closes it. This is a decision to confirm.
- **Close View Tasks**: closes the window as its close button does, and says whether it closed. A changed edit [asks first](#closing-view-tasks-asks-before-it-drops-a-changed-edit), and the window stays open when the user cancels, the save fails, or the prompt already shows. Shell calls it when the Add new task shortcut [switches to Add](shell.md#a-shortcut-switches-between-add-and-view-tasks), and opens Add only when it closed.

[`ViewTasksWindow.Create`](../../../src/Desktop/TaskList/ViewTasksWindow.cs) retains its existing arguments and
adds the shared `ISyncService` as its last argument. Row 7 of the [desktop delivery backlog](README.md#delivery-backlog) adds `Close` to its port. Row 8 adds Tasks' [tag colors](../tasks.md#tag-colors-are-a-capability-of-their-own) to `Create`, right after `tasks`, as the Task editor's `Create` takes them. There is no sync-less overload, test-only switch, or second service created by this module.

```csharp
namespace Memoria.ToDo.Desktop.TaskList;

public static class ViewTasksWindow
{
    /// Creates View Tasks. The window, and its description web view, are made only on IViewTasksWindow.Show.
    public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette,
        ITheme theme, TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync);
}

/// The View Tasks window, as Shell uses it.
public interface IViewTasksWindow
{
    /// Opens the window, or brings it forward.
    void Show();

    /// Closes the window as its close button does, first asking Save, Discard, or Cancel when an edit
    /// in place has changes, and returns once it's answered. True when the window is closed, or wasn't
    /// open; false when the user cancels, Save doesn't save, or the prompt already shows.
    bool Close();

    /// Whether the window is open now.
    bool IsOpen { get; }
}
```

Proposed: `Close` waits for the prompt's answer and returns it, rather than returning a task. The prompt is a modal dialog, which runs its own message loop while it waits, so Shell's shortcut action stays a plain call. This is a decision to confirm.

## Failure modes

- **The web view fails**, for example when the WebView2 Runtime is missing: the description shows as Markdown source, with a message saying why, as the [Formatted view](formatted-view.md#failure-modes) does. The rest of the list works.
- **A move, resolve, or delete can't be saved**: the task returns to how it was, and a message says why.
- **A deleted task's images can't be removed**: the task's file is already deleted, as [Tasks](../tasks.md#public-interface) says, so the task stays gone. Proposed: the message line says "The task was deleted, but some of its images couldn't be removed: " followed by the reason. This is a decision to confirm.
- **Saving an edit fails**: the editor stays open with the text and shows the error, as the [editor's save](task-editor.md#add-and-view-tasks-save-the-same-way-through-the-editor) does.
- **An edit's images can't be deleted**: the action goes ahead and the message line says why, and a new edit of that task doesn't open until they're deleted, as the [user decided](#edit-turns-the-open-task-into-the-editor-in-place).
- **A tag added or removed without Edit can't be saved**, for example because the task's file can't be written or no clone folder is set: the badges go back to the task's saved tags, no Undo bar shows, and the message line says why, as the editor words each of Tasks' failures. When the task was deleted meanwhile, such as by a sync, it says "This task no longer exists." (Decision to confirm: the wording.)
- **Undoing a tag change fails**: the change stays, the bar goes away, and the message line says why.
- **A tag's color can't be kept**: the tag isn't added and nothing is saved; Custom says why, as in [Add](task-editor.md#failure-modes).
- **Save in the close prompt doesn't save**: View Tasks stays open with the editor's message, and Add doesn't open.
- **A Tasks call from the list throws a failure the call doesn't document**: the list doesn't report it, since [each module reports only its expected failures](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), so the platform shows it and the app ends. That includes an `AggregateException`, which only adding a task and a sync's update throw, and no call from the list makes either.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Where a task's description shows:
  - **A list plus a rendered detail pane**: expanding in place keeps the description under its title.
  - **A list only, with tasks opening in an editor window**: no rendered view.
- How many tasks are open:
  - **Several at once**: a web view for each open task, and a list that's harder to scan.
- What happens when an edit's images can't be deleted:
  - **Say why and go on, with no cleanup before a new edit**: saving a later edit of the task would put the leftover images in the clone, where they sync.
  - **Stop until deleting works**, keeping the Undo bar and refusing to close: a lasting disk problem would block editing, deleting, and closing View Tasks.
  - **Retry on a timer, or delete every waiting edit image when the app starts**: either needs a new rule for how long an edit's images live, and Tasks shares that rule with the Android app.
- Unexpected sync-request failures:
  - **Terminate the app through its central fail-fast boundary**: the user chose an inline failure so an
    unsuccessful sync request does not end local task work.
- How View Tasks uses the tag control:
  - **Tags change only in an edit, and show as colored badges in the rows and the filter**: the user chose to tag a task without Edit.
  - **Only the edit uses the control, and the rows and the filter stay as they are**: a tag would look different in the list than in the editor, and colors would help least where the user scans most.
  - **Saving a tag change when the task collapses**: closing View Tasks or a sync meanwhile could lose it, and the change would need a pending state of its own.
  - **A new Tasks part that adds or removes one tag**: Tasks' save of an edit already applies only the tags that changed.
  - **No Undo for a tag change, since another click reverses it**: a slip on a remove button would be the only change in View Tasks with no Undo, and a tag no other task uses would be gone from the + menu.
- What Esc does while a list or an edit is open:
  - **Close at once, after closing an open list**: an open edit would be lost with no Undo, since the bar closes with the window.
  - **Close at once, and ask about a changed edit as the shortcut does**: a key that means "back one step" would show a dialog, and leaving an edit would take the mouse or another key.
- When closing with a changed edit asks:
  - **Only for the Add new task shortcut**: the close button would still lose the edit that the shortcut protects.
  - **Also for Exit and Windows signing out**: the prompt could block the app's exit or the end of the session.
- How View Tasks tells the user's close from the app's exit:
  - **A flag the platform sets before the app exits, which View Tasks reads**: a new public platform part, for what Windows already tells each window.
  - **A Shell call that closes View Tasks without asking before the app exits**: it changes View Tasks' port, and it must run before Shell's first wait, or signing out races WPF closing the window.
  - **WPF's own exit state**: `Application`'s flag is internal, and `Dispatcher.HasShutdownStarted` turns true only after the windows have closed.
  - **Asking on every plain close message**: WPF's exit sends one, so Exit would show the prompt.
- How Close reports the prompt's answer:
  - **An asynchronous close that returns a task**: Shell's shortcut action, and every caller, would become asynchronous for one dialog, and a second press would still need its own guard.

Who shows formatted Markdown, and who opens links, is decided in the [Formatted view](formatted-view.md#ruled-out).
