# Task list: how the Android app shows and finds tasks

Status: Decided

The Task list is the Android app's Task View screen: one list of open tasks in rank order, where a tap expands a task in place, swipes resolve and delete, and a floating button adds a task. Pulling the list down syncs at once.

## Behavior

### One list in rank order, with one task open at a time

Task View shows the open tasks in one list, in rank order; the [desktop](../desktop/task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) instead groups them by tag, beside the selected task's description. A tap expands a task in place to show its rendered description, and expanding another collapses it. Titles show their inline Markdown as styled text, from [Rendering](../rendering.md#titles-and-the-editor-get-styled-spans-not-html), and links work as [Rendering](../rendering.md#only-web-links-open-in-the-browser) decides.

A floating Add button opens Add from the [Task editor](task-editor.md#public-interface).

### One web view starts the first time a task expands

Proposed: one Android WebView shows every description, as [Rendering](../rendering.md#the-web-view-runs-no-scripts-and-loads-images-only-from-the-clones-address) allows. It's created the first time a task expands, not when Task View opens, so opening the app never waits for it. It's released when Task View closes. This is a decision to confirm.

Android screens get UX tests on an emulator, as the [technology stack](../technology-stack.md#wpf-screens-get-ux-tests-on-the-pc-and-android-screens-on-an-emulator) decides, once the implementation finds a test runner that can host them. If they do, the Task list gets its web view the way the desktop's [Formatted view](../desktop/formatted-view.md#the-app-supplies-the-web-view-and-the-browser) does: through an interface it publishes, which the composition root implements with Android's WebView.

### Swipes resolve and delete, and a long press moves a task

- **Swipe right resolves a task.** Swipe left deletes it, and an Undo bar shows. A setting in [Configure](configure.md#each-setting-has-one-owner) swaps the two directions.
- **A long press and drag moves a task.** The moved task gets its new rank from [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors).
- **Edit opens the full-screen editor.** An expanded task has an Edit button, which opens it in the [Task editor](task-editor.md#edit-opens-the-same-screen-for-a-task-in-task-view).

RecyclerView's [ItemTouchHelper](https://developer.android.com/reference/androidx/recyclerview/widget/ItemTouchHelper) handles both swipes and the drag.

Proposed: the task's file is deleted when the Undo bar goes away or Task View closes, as on the [desktop](../desktop/task-list.md#keys-and-the-mouse-reorder-resolve-and-delete). This is a decision to confirm.

### Tags and search narrow the list

The user decided that Task View's tag chips come in the [tags' order](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) that Tasks keeps, the order of the desktop's [tag groups](../desktop/task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description). A group moved on the PC moves its chip on the phone after the next sync, and a tag never moved shows first. The composition root gives the Task list Tasks' tag order, as the desktop's does, and the chips are the [Task editor's](task-editor.md#public-interface), given the tags in that order.

Proposed, each a decision to confirm:

- Task View shows a chip for each tag an open task has, in one row that scrolls sideways, so every tag can filter. Add's chips keep their five most-used tags, as the desktop's + menu keeps its most-used order.
- The phone doesn't move tags: the order changes only by dragging groups on the desktop, and a long press and drag on the phone moves tasks only.
- Tag chips filter the list one tag at a time, keeping the tasks that have that tag anywhere in their tags, and choosing a tag again clears the filter; search works as on the [desktop](../desktop/task-list.md#search-narrows-the-list), and opens from a button in the top app bar. The desktop dropped its tag filter, since its tag groups collapse.

Deferred: the desktop shows tags as [colored badges](../desktop/task-list.md#tags-show-as-colored-badges-and-change-without-edit) on group headings, and changes them on the selected task without Edit. Whether the phone's chips take each tag's color, and whether a tag changes without Edit, follows the [Task editor's](task-editor.md#tags-sit-as-chips-under-the-title) answer when the Android app is built.

### Resolved tasks wait behind a count at the bottom

Resolved tasks are hidden, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides. Proposed: "N resolved" at the bottom of the list shows them, as on the [desktop](../desktop/task-list.md#resolved-tasks-wait-behind-a-count-at-the-bottom), and the resolve swipe reopens a resolved task. This is a decision to confirm.

### Mentions show each contact's current details

The user decided that the phone shows mentions as the desktop does, so a mention in a title or a description shows the contact's current details, as the [desktop's Task list](../desktop/task-list.md#mentions-show-each-contacts-current-details) says, on Rendering's mention background, and a tap on it does nothing. The composition root gives the Task list [Contacts](../contacts.md#public-interface) for it.

### Pulling the list down starts a sync

Opening the app starts a sync, as [Shell](shell.md#sync-runs-while-the-app-is-open-and-every-few-hours-in-the-background) decides, and pulling the list down starts one at once, like the [desktop's Sync button](../desktop/task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button). Android's standard control for this gesture, [SwipeRefreshLayout](https://developer.android.com/develop/ui/views/touch-and-input/swipe/add-swipe-interface), handles it. Its package, [Xamarin.AndroidX.SwipeRefreshLayout](https://www.nuget.org/packages/Xamarin.AndroidX.SwipeRefreshLayout/1.2.0.3) 1.2.0.3, is a new dependency, listed with the [other libraries](README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material).

### Task View shows sync status and banners

Proposed, each a decision to confirm:

- When the phone is offline or sync fails, a line under the top app bar says so, with the last successful sync time. [Configure](configure.md#configure-shows-how-sync-is-doing) shows the details.
- A banner at the top shows a message with an action, such as a newer version to install. Shell uses it for [updates](shell.md#updates-come-from-this-repositorys-github-releases).

### A Settings item opens Configure

The menu of Task View's top app bar has a Settings item, where Android apps usually put settings. It's how a configured phone reaches [Configure](configure.md), since [Shell](shell.md#four-ways-lead-into-the-app) opens Configure only until the phone is configured. The Task list can't use Configure, as the [module map](../modules.md#module-map) says, so the item calls an action the composition root passes to Task View's create method. You chose this on November 26, 2026.

## Public interface

Shell uses these parts:

- **Show Task View**: opens the screen, with one task expanded if Shell names it.
- **The swipe direction**: which way resolves. Shell sets it from Configure's setting, so the Task list doesn't use Configure.
- **Show a banner**: a message and its action, at the top of the list.
- **The top open tasks**: the first open tasks in rank order, with a notice when they change. Shell's widget shows them.

Proposed for [delivery step 1](README.md#delivery-backlog), Show Task View with an empty list, its floating Add button, and its [Settings item](#a-settings-item-opens-configure); later steps add the rest:

```csharp
namespace Memoria.ToDo.Android.TaskList;

/// Task View.
public interface ITaskViewScreen
{
    /// Opens Task View in front of the app's other screens.
    void Show();
}

public static class TaskViewScreen
{
    /// Prepares Task View. Its Show asks screens to open Task View's activity, its floating button calls
    /// add's Show, and its Settings item calls openSettings. Throws ArgumentNullException when an argument is null.
    public static ITaskViewScreen Create(
        Memoria.ToDo.Android.TaskEditor.IAddScreen add, System.Action openSettings,
        Memoria.ToDo.Android.Platform.IScreenOpener screens, Serilog.ILogger logger);
}
```

Each screen opens through the platform's [screen opener](platform.md#screens-open-through-one-opener-the-composition-root-supplies), as Add and Configure do.

## Failure modes

- **The web view can't start, or a move, resolve, or delete can't be saved**: Task View handles it as the [desktop does](../desktop/task-list.md#failure-modes).
- **A sync started from Task View fails**: the status line shows the error.
- **`tags.json` can't be read, or has a newer version**: the chips come in alphabetical order, as [Tasks](../tasks.md#failure-modes) says, and the list still loads.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Resolving and deleting:
  - **A checkbox and buttons, with no swipes**: offered, and not chosen.
- Starting a sync by hand:
  - **A Sync button in the top app bar**: offered, and not chosen.
  - **Both pulling the list down and a Sync button**: offered, and not chosen.
- The order of the tag chips:
  - **Decided when the Android app is built**: the user wants one order everywhere now, set on the desktop.
- How a configured phone reaches Configure:
  - **A second app shortcut, "Settings", beside "Add task"**: few users find long-press shortcuts.
  - **A second icon in the app list that opens Configure**: the phone would show two icons for one app.
