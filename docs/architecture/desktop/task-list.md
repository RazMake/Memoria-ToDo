# Task list: how the desktop app shows and finds tasks

Status: Decided

The Task list is the desktop app's View Tasks window: one list of open tasks in rank order, where a task expands in place to show its formatted description and turns into the editor in place. Its Sync button syncs on demand.

## Behavior

### One list in rank order, with one task open at a time

View Tasks shows the open tasks in one list, in rank order. Each row shows a checkbox and the title, and takes the [title's name](formatted-view.md#a-titles-name-is-the-text-it-shows) as its own. Selecting a task expands it in place to show its formatted description; expanding another collapses the open one, so one web view serves every task.

The user decided that titles and descriptions show formatted, as a reader sees them, never as Markdown: no Markdown characters, such as `**` or `:red[`, show. The [Formatted view](formatted-view.md) shows both, so Add can later show a preview the same way:

- **Titles show as native Windows Presentation Foundation (WPF) text**, so they need no web view.
- **The open task's description shows in the one web view**, which the Task list makes when View Tasks opens and releases when it closes.
- **Links**: a web link opens in the default browser, as the [Formatted view](formatted-view.md#web-links-open-in-the-default-browser-and-other-links-are-reported) does. A link to another task expands that task, which the Task list tells by asking [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it). Every other link does nothing, as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

Proposed, each a decision to confirm:

- Each row also shows the task's tags.
- View Tasks opens 680 wide and 720 tall, in device-independent pixels, and can't be made narrower than 400. It opens where [Single window](README.md#a-closed-window-is-made-again-on-the-next-show) places it and remembers neither its size nor its place, so each time it's made again it starts at this size. Sizing the window to its list would make it jump as tasks expand, and remembering its size needs a saved setting that no design has yet.
- The web view starts in the background when View Tasks opens, and is released when it closes, so the tray app holds no browser while View Tasks is closed. Microsoft recommends creating WebView2 only for real web content, and no more instances than needed ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)).
- An open description shows at its full height: the list scrolls, not the description.
- A link to a deleted task shows "This task no longer exists." in the message line. A link to a resolved task shows the resolved tasks and expands it. A link to a hidden task clears the search if the search hides it, and the tag filter if the filter hides it, keeping whichever doesn't, then expands it.
- When the browser can't open a web link, View Tasks shows the error in its message line, the same line where a failed move, resolve, or delete says why.

### Edit turns the open task into the editor, in place

Edit switches the open task to the same editor as Add, from the [Task editor](task-editor.md#public-interface), in the same place in the list. Saving works as in Add, through the [editor's save](task-editor.md#add-and-view-tasks-save-the-same-way-through-the-editor), which passes Tasks' [save of an edit](../tasks.md#public-interface) with the task as the editor got it.

Proposed, each a decision to confirm:

- Esc, collapsing the task, or opening another one discards the change, and shows the same Undo bar as Delete.
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

### Tags and search narrow the list

[Tasks](../tasks.md#tags-and-search-ignore-case) decides how search and the tag filter match tasks. Proposed, each a decision to confirm:

- The same five tag chips as Add, from the [Task editor](task-editor.md#tags-come-from-the-tags-already-in-use), filter the list, and a list of all tags offers the rest.
- One tag filters at a time; choosing it again clears the filter.
- Ctrl+F searches titles, descriptions, and tags, and search combines with the tag filter.
- The resolved count, below, counts only the resolved tasks that match the search and the tag filter.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden by default, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed: "N resolved" at the bottom of the list expands the resolved tasks, in rank order, each with Reopen, and the count hides while it's 0. This is a decision to confirm.

### View Tasks syncs when it opens and on its Sync button

Opening View Tasks starts a sync, and its Sync button starts one at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides. The user asked for the button, to sync faster. Step 5 of the [delivery backlog](README.md#delivery-backlog) builds both; until then, View Tasks has no Sync button and starts no sync.

Proposed, each a decision to confirm:

- F5 also starts a sync.
- While a sync runs, the button shows it's working, and pressing it again starts no second sync.
- When a sync started from View Tasks fails, View Tasks shows the error next to the button. Other failures show only as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) decides.

## Public interface

- **Create View Tasks**: the composition root calls it when the app starts, with the parts View Tasks needs, and gives the result to Shell. The window opens only on Show View Tasks. It takes the Task editor's parts, since it builds the editor, and the [Formatted view's](formatted-view.md#public-interface) two ports: a function that makes a new description web view each time View Tasks opens, and the default browser.
- **Show View Tasks**: opens the window, or brings it forward. Shell calls it from the tray menu and when the app is started a second time. Proposed: it uses the desktop platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show), which says where the window opens and what happens after the user closes it. This is a decision to confirm.

Step 4 of the [desktop delivery backlog](README.md#delivery-backlog) gives `Create` its parameters:

```csharp
namespace Memoria.ToDo.Desktop.TaskList;

public static class ViewTasksWindow
{
    /// Creates View Tasks. The window opens only on IViewTasksWindow.Show.
    public static IViewTasksWindow Create(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser);
}

/// View Tasks, as Shell uses it.
public interface IViewTasksWindow
{
    /// Opens the window, or brings it forward.
    void Show();

    /// Whether the window is open now.
    bool IsOpen { get; }
}
```

## Failure modes

- **The web view fails**, for example when the WebView2 Runtime is missing: the description shows as Markdown source, with a message saying why, as the [Formatted view](formatted-view.md#failure-modes) does. The rest of the list works.
- **A move, resolve, or delete can't be saved**: the task returns to how it was, and a message says why.
- **A deleted task's images can't be removed**: the task's file is already deleted, as [Tasks](../tasks.md#public-interface) says, so the task stays gone. Proposed: the message line says "The task was deleted, but some of its images couldn't be removed: " followed by the reason. This is a decision to confirm.
- **Saving an edit fails**: the editor stays open with the text and shows the error, as the [editor's save](task-editor.md#add-and-view-tasks-save-the-same-way-through-the-editor) does.
- **An edit's images can't be deleted**: the action goes ahead and the message line says why, and a new edit of that task doesn't open until they're deleted, as the [user decided](#edit-turns-the-open-task-into-the-editor-in-place).

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

Who shows formatted Markdown, and who opens links, is decided in the [Formatted view](formatted-view.md#ruled-out).
