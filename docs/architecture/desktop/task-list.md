# Task list: how the desktop app shows and finds tasks

Status: Decided

The Task list is the desktop app's View Tasks window: one list of open tasks in rank order, where a task expands in place to show its rendered description and turns into the editor in place. Its Sync button syncs on demand.

## Behavior

### One list in rank order, with one task open at a time

View Tasks shows the open tasks in one list, in rank order. Each row shows a checkbox and the title. Selecting a task expands it in place to show its rendered description; expanding another collapses the open one, so one web view serves every task.

- **Titles show their inline Markdown natively**: bold, italic, code, strikethrough, links, and color. Proposed: [Rendering](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) returns styled spans, and the Task list turns them into Windows Presentation Foundation (WPF) text, so titles need no web view. This is a decision to confirm.
- **Descriptions render as HTML in a `WebView2CompositionControl`**, a drop-in WebView2 control, stable since January 2025 ([SDK 1.0.2957.106](https://learn.microsoft.com/microsoft-edge/webview2/release-notes/sdk/1-0-2957-106)). The standard control is a separate window that always draws on top of WPF content, the airspace problem; this one scrolls and clips with the list. It copies the browser's output, which can lower its frame rate ([control spec](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/WPF_WebView2CompositionControl.md)); a description doesn't animate, so that's acceptable.
- **Links in a description**: a web link opens in the default browser, and a link to another task expands that task. Every other link does nothing, as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

Proposed, each a decision to confirm:

- Each row also shows the task's tags.
- The web view starts in the background when View Tasks opens, and is released when it closes, so the tray app holds no browser while View Tasks is closed. Microsoft recommends creating WebView2 only for real web content, and no more instances than needed ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)).
- An open description shows at its full height: the list scrolls, not the description.
- A link to a deleted task shows a short message that the task no longer exists.

### The Task list gets its web view through an interface it publishes

The Task list doesn't create its web view. It publishes a small interface, the [description web view](#public-interface), and the desktop app's composition root supplies the implementation, which uses the `WebView2CompositionControl` above. That implementation lives beside the composition root, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides for an app's implementations of a module's interface. A [UX test](../technology-stack.md#wpf-screens-get-ux-tests-on-the-pc-and-android-screens-on-an-emulator) of View Tasks passes a fake web view instead, since WebView2's browser runs outside the test process. This is a port, which the [patterns](../overview.md#each-pattern-serves-one-requirement) allow where a test needs one.

The interface belongs to the Task list, not to [Rendering](../rendering.md), though Rendering makes the page it shows:

- **The Task list is its only user.** The editor and titles show styled spans, not a web view, as [Rendering](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) decides.
- **Rendering is in the shared library, which has no screens** ([How the apps fit together](../overview.md#two-apps-share-one-c-library)), while the web view is a WPF control.

### Edit turns the open task into the editor, in place

Edit switches the open task to the same editor as Add, from the [Task editor](task-editor.md#public-interface), in the same place in the list. Saving works as in Add.

Proposed, each a decision to confirm:

- Esc, collapsing the task, or opening another one discards the change, and shows the same Undo bar as Delete.
- Changes from other devices show in the list at once. An open edit keeps the user's text, and saving it merges with those changes as [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) decides.

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
- The task's file is deleted when the Undo bar goes away or View Tasks closes.

A moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors).

### Tags and search narrow the list

[Tasks](../tasks.md#tags-and-search-ignore-case) decides how search and the tag filter match tasks. Proposed, each a decision to confirm:

- The same five tag chips as Add, from the [Task editor](task-editor.md#tags-come-from-the-tags-already-in-use), filter the list, and a list of all tags offers the rest.
- One tag filters at a time; choosing it again clears the filter.
- Ctrl+F searches titles, descriptions, and tags, and search combines with the tag filter.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden by default, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed: "N resolved" at the bottom of the list expands the resolved tasks, each with Reopen. This is a decision to confirm.

### View Tasks syncs when it opens and on its Sync button

Opening View Tasks starts a sync, and its Sync button starts one at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides. The user asked for the button, to sync faster.

Proposed, each a decision to confirm:

- F5 also starts a sync.
- While a sync runs, the button shows it's working, and pressing it again starts no second sync.
- When a sync started from View Tasks fails, View Tasks shows the error next to the button. Other failures show only as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) decides.

## Public interface

- **Create View Tasks**: the composition root calls it when the app starts, with the parts View Tasks needs, such as the description web view, and gives the result to Shell. The window opens only on Show View Tasks.
- **Show View Tasks**: opens the window, or brings it forward. Shell calls it from the tray menu and when the app is started a second time.
- **Description web view**: the interface the Task list uses and the composition root implements, as [above](#the-task-list-gets-its-web-view-through-an-interface-it-publishes). The Task list gets a new one each time it needs a web view. Each one:
  - **gives the WPF element** that View Tasks places in the open task;
  - **shows a page**: a full HTML page from Rendering's [description page](../rendering.md#public-interface). It runs no scripts and loads only images from the clone, as [Rendering](../rendering.md#the-web-view-runs-no-scripts-and-loads-only-images-from-the-clone) requires;
  - **reports the address of a link the user clicks**, and opens nothing itself, so the Task list applies the link rules [above](#one-list-in-rank-order-with-one-task-open-at-a-time);
  - **reports that it couldn't start**, and why, so View Tasks can show the Markdown source, as [Failure modes](#failure-modes) says;
  - **is released**, which closes its browser.

## Failure modes

- **The web view can't start**, for example when the WebView2 Runtime is missing: the description shows as Markdown source, with a message saying why. The rest of the list works.
- **A move, resolve, or delete can't be saved**: the task returns to how it was, and a message says why.
- **Saving an edit fails**: the editor stays open with the text and shows the error.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Where a task's description shows:
  - **A list plus a rendered detail pane**: expanding in place keeps the description under its title.
  - **A list only, with tasks opening in an editor window**: no rendered view.
- How many tasks are open:
  - **Several at once**: a web view for each open task, and a list that's harder to scan.
- How the Task list gets its web view:
  - **The Task list creates the WebView2 control itself**: a UX test of View Tasks would need a browser outside the test process, so only integration tests could check View Tasks.
  - **The interface in Rendering**: Rendering has no screens and serves both apps, so it would publish a WPF interface that only the Task list uses.
