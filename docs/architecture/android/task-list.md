# Task list: how the Android app shows and finds tasks

Status: Decided

The Task list is the Android app's View Tasks screen: one list of open tasks in rank order, where a tap expands a task in place, swipes resolve and delete, and a floating button adds a task. Pulling the list down syncs at once.

## Behavior

### One list in rank order, with one task open at a time

As on the [desktop](../desktop/task-list.md#one-list-in-rank-order-with-one-task-open-at-a-time), View Tasks shows the open tasks in one list, in rank order. A tap expands a task in place to show its rendered description, and expanding another collapses it. Titles show their inline Markdown as styled text, from [Rendering](../rendering.md#titles-and-the-editor-get-styled-spans-not-html), and links work as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

A floating Add button opens Add from the [Task editor](task-editor.md#public-interface).

### One web view starts the first time a task expands

Proposed: one Android WebView shows every description, as [Rendering](../rendering.md#the-web-view-runs-no-scripts-and-loads-only-images-from-the-clone) allows. It's created the first time a task expands, not when View Tasks opens, so opening the app never waits for it. It's released when View Tasks closes. This is a decision to confirm.

Android screens get UX tests only if the [technology stack](../technology-stack.md#wpf-screens-get-ux-tests-on-the-pc-and-android-screens-on-an-emulator) finds a test runner for them. If they do, the Task list gets its web view as the [desktop's does](../desktop/task-list.md#the-task-list-gets-its-web-view-through-an-interface-it-publishes): through an interface it publishes, which the composition root implements with Android's WebView.

### Swipes resolve and delete, and a long press moves a task

- **Swipe right resolves a task.** Swipe left deletes it, and an Undo bar shows. A setting in [Configure](configure.md#each-setting-has-one-owner) swaps the two directions.
- **A long press and drag moves a task.** The moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors).
- **Edit opens the full-screen editor.** An expanded task has an Edit button, which opens it in the [Task editor](task-editor.md#edit-opens-the-same-screen-for-a-task-in-view-tasks).

RecyclerView's [ItemTouchHelper](https://developer.android.com/reference/androidx/recyclerview/widget/ItemTouchHelper) handles both swipes and the drag.

Proposed: the task's file is deleted when the Undo bar goes away or View Tasks closes, as on the [desktop](../desktop/task-list.md#keys-and-the-mouse-reorder-resolve-and-delete). This is a decision to confirm.

### Tags and search narrow the list

Proposed: tag chips and search work as on the [desktop](../desktop/task-list.md#tags-and-search-narrow-the-list), and search opens from a button in the top app bar. This is a decision to confirm.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed: "N resolved" at the bottom of the list shows them, as on the [desktop](../desktop/task-list.md#resolved-tasks-wait-behind-a-count-at-the-bottom), and the resolve swipe reopens a resolved task. This is a decision to confirm.

### Pulling the list down starts a sync

Opening the app starts a sync, as [Shell](shell.md#sync-runs-while-the-app-is-open-and-every-few-hours-in-the-background) decides, and pulling the list down starts one at once, like the [desktop's Sync button](../desktop/task-list.md#view-tasks-syncs-when-it-opens-and-on-its-sync-button). Android's standard control for this gesture, [SwipeRefreshLayout](https://developer.android.com/develop/ui/views/touch-and-input/swipe/add-swipe-interface), handles it. Its package, [Xamarin.AndroidX.SwipeRefreshLayout](https://www.nuget.org/packages/Xamarin.AndroidX.SwipeRefreshLayout/1.2.0.3) 1.2.0.3, is a new dependency, listed with the [other libraries](README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material).

### View Tasks shows sync status and banners

Proposed, each a decision to confirm:

- When the phone is offline or sync fails, a line under the top app bar says so, with the last successful sync time. [Configure](configure.md#configure-shows-how-sync-is-doing) shows the details.
- A banner at the top shows a message with an action, such as a newer version to install. Shell uses it for [updates](shell.md#updates-come-from-this-repositorys-github-releases).

## Public interface

Shell uses these parts:

- **Show View Tasks**: opens the screen, with one task expanded if Shell names it.
- **The swipe direction**: which way resolves. Shell sets it from Configure's setting, so the Task list doesn't use Configure.
- **Show a banner**: a message and its action, at the top of the list.
- **The top open tasks**: the first open tasks in rank order, with a notice when they change. Shell's widget shows them.

## Failure modes

- **The web view can't start, or a move, resolve, or delete can't be saved**: View Tasks handles it as the [desktop does](../desktop/task-list.md#failure-modes).
- **A sync started from View Tasks fails**: the status line shows the error.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Resolving and deleting:
  - **A checkbox and buttons, with no swipes**: offered, and not chosen.
- Starting a sync by hand:
  - **A Sync button in the top app bar**: offered, and not chosen.
  - **Both pulling the list down and a Sync button**: offered, and not chosen.
