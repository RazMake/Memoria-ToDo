# How the desktop app is built

The desktop app is a Windows Presentation Foundation (WPF) app that lives in the notification area (the tray) and opens Add new Task, View Tasks, and Configure. This page lists its four modules, each with one design file in this folder, and the other designs they rely on. It also says how the app starts and the steps it's built in.

## Four modules make up the desktop app

Each module is a project of its own, and the [module map](../modules.md#module-map) says which modules each may use. The table lists them in the order a user meets them:

| Module | What its design file decides |
| --- | --- |
| [Shell](shell.md) | The tray icon and its menu, the global shortcut, starting with Windows, one running instance, updates, the theme, and how sync status shows |
| [Task editor](task-editor.md) | The Add window: its fields, keys, draft, and tags. View Tasks edits a task with the same editor. |
| [Task list](task-list.md) | View Tasks: the list in rank order, expanding and editing in place, the tag filter, search, resolving, deleting, and the Sync button |
| [Configure](configure.md) | The settings window: this device's settings, the shared deletion delay, and sync status details |

Two shared modules serve the desktop app: [Editing](../editing.md) holds the formatting and snippet commands, and [Rendering](../rendering.md) turns Markdown into HTML and styled text. [How the apps fit together](../overview.md) picks the main libraries, such as AvalonEdit for the editor and WebView2 for rendered descriptions.

Proposed: each window module names its own window, and Shell names its [tray menu items](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions). The names match today by choice, not by a rule, so either can change alone. This is a decision to confirm.

## The platform runs the app, and the composition root builds it

The desktop platform, `Memoria.ToDo.Desktop.Platform`, holds the app's startup, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides. The composition root, `Memoria.ToDo.Desktop.App`, builds the app from the modules and hands it to the platform to run.

The platform's public interface has two parts. Three more are proposed further down: one that [opens windows](#windows-open-on-the-screen-with-the-mouse-pointer), one that [makes them again](#a-closed-window-is-made-again-on-the-next-show), and the app's name. The two parts are:

- **The app's lifetime**: an interface with two steps, start and stop. Start receives a way to end the app. [Shell](shell.md#public-interface) implements it, so the platform names no module. Proposed: the way to end the app works from any thread. The platform passes the call to the UI thread and returns without waiting, so a module that ends the app from a background task needs no thread rule of its own. This is a decision to confirm.
- **Run the app**: takes a function that builds the lifetime, and returns the process's exit code. In order, it:
  1. rejects a call from a thread that isn't a single-threaded apartment (STA) thread, which WPF needs ([threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)), before doing anything else;
  2. creates the WPF application, which ends only when told to, not when its last window closes, since a tray app has no main window ([ShutdownMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.shutdownmode));
  3. builds the lifetime and starts it on the UI thread, so the modules' windows are made after the WPF application exists;
  4. when the lifetime ends the app, or Windows ends the session ([SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.sessionending)), stops the lifetime once and returns.

When the app starts, the composition root calls the create part of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface). It builds Shell's lifetime from the results and passes it to Run the app. The windows themselves stay `internal`, as all of a module's code does ([Modules](../modules.md#a-module-is-one-capability-behind-a-small-interface)).

**An error no module handles is shown, then the app ends.** The platform shows the error's message in a message box, then ends the app with a non-zero exit code. This covers errors on the UI thread ([DispatcherUnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception)) and on other threads ([UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception)). Each module still reports its own expected failures, such as a sync error. Catching nothing, WPF's default, was ruled out: the app would vanish from the tray with no message.

Proposed, each a decision to confirm:

- **A fifth part of the platform's public interface, App name, gives the app's name** to the message box and Shell's tray tooltip. Its only home is the build's `Product` property, as the [technology stack](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) proposes:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  /// The app's name as users see it, such as in the error box's title and the tray icon's tooltip.
  public static class AppName
  {
      /// The name. Its only home is the build's Product property in Directory.Build.props.
      public static string Value { get; }
  }
  ```

- **The message box is titled with the app's name, with an error icon and one OK button, and the exit code is 1.**
- **After an error, the platform stops the lifetime once before the app ends**, so the tray icon goes away. It stops it whenever Start was called, even if Start failed, so a half-started lifetime cleans up too.
- **After an error on another thread, the platform stops the lifetime on the UI thread, waiting at most 5 seconds, then ends the process itself.** Otherwise .NET would end it as a crash, with Windows' error report ([UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception)).
- **Run the app's thread check has a unit test; its other steps have integration tests, each in a test program of its own.** A process can create only one WPF application ([Application](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application)), so each test starts a small program that runs the app with the lifetime the test picks, then checks its exit code and output. The program lives in a test folder of its own, as the [technology stack](../technology-stack.md#code-and-tests-sit-in-mirrored-trees-by-part-in-one-solution) proposes.
- **One icon serves the tray, every window, the app's file, and the installer.** Until you supply one, it's a placeholder: a white check mark on a blue square. It's one file, `src/Desktop/App/app.ico`, beside the composition root, and it holds at least the 16, 20, 24, 32, 48, and 256 pixel sizes, since the tray uses 16 to 32 pixels depending on the screen's scale ([icon sizes](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction)). Three things use it, and none keeps a copy:
  - **The app's file and every window.** The composition root's project sets it as the app's icon. A WPF window that sets no icon shows the first icon in the running program's file ([Window.Icon](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.icon), [WPF's source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/AppModel/IconHelper.cs)), so no module's window names the file. Not confirmed by a run.
  - **The tray icon.** The tray adapter in the composition root loads the same file, built into the composition root.
  - **The installer.** The [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) passes the same file to Velopack's packing step ([vpk options](https://docs.velopack.io/reference/cli/content/vpk-windows)).

### Windows open on the screen with the mouse pointer

Add, View Tasks, and Configure open the same way, so the rule has one home: the platform, since [code shared by two modules moves to the platform](../modules.md#the-platform-serves-every-module-and-knows-none). Proposed, each a decision to confirm:

- **A third part of the platform's public interface, Show on the pointer's screen, opens a module's window.** The [Task editor](task-editor.md#public-interface) calls it with its Add window. The [Task list](task-list.md#public-interface) and [Configure](configure.md#public-interface) reach it through the [fourth part](#a-closed-window-is-made-again-on-the-next-show). These two are the only platform parts the window modules use:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  public static class WindowPlacement
  {
      /// A hidden window: shown centered on the work area of the screen with the mouse pointer,
      /// then activated. A visible window stays where it is: restored if minimized, then activated.
      /// Throws InvalidOperationException off the window's UI thread, and Win32Exception when
      /// Windows can't give the pointer's position, or its screen's work area or scale, or move or
      /// restore the window.
      public static void ShowOnPointerScreen(System.Windows.Window window);
  }
  ```

- **The work area is the screen minus the taskbar**, so a window never opens under it. A window wider or taller than the work area starts at the work area's left or top edge, so its title bar stays on the screen, and stays centered in the other direction.
- **Activated means in front of other windows, with the keyboard focus, but not always on top.** Windows may refuse to bring a window to the front while the user works in another app, and flashes its taskbar button instead ([SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)). Not confirmed by a run: that a click on the tray icon, or the global shortcut, lets the app come to the front.
- **A window hidden while minimized opens in the normal state**, centered like any hidden window. This happens when the user closes the minimized Add window from its taskbar button. Windows can't restore a window without showing it, so the platform restores it while it's cloaked, invisible to the user, and hides it again before placing it ([DWMWA_CLOAK](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)). Proposed, a decision to confirm. Not confirmed by eye: that nothing flashes on the screen or the taskbar.- **The app is aware of each screen's own scale**, declared in the composition root's app manifest, so windows stay sharp on a screen whose scale differs from the main one. WPF apps use one scale for every screen unless they declare this ([per-monitor DPI](https://github.com/microsoft/WPF-Samples/tree/main/PerMonitorDPI)). Not confirmed by a run: that a window opens centered on a screen whose scale differs from the main one, since WPF may place a window that isn't shown yet at the main screen's scale.
- **Its tests are UX tests in the platform's test project**, each showing a plain test window. The window modules' UX tests check that Show shows and activates their window, not where it opens.

Windows Forms' `Screen` class, which finds the pointer's screen, was ruled out: it would load Windows Forms into the app for one lookup, and Windows' own functions give the same answer.

### A closed window is made again on the next Show

View Tasks and Configure close when the user closes them, and WPF can't show a closed window again ([Window.Show](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.show)). Both need the same rule, so it has one home in the platform. Proposed, each a decision to confirm:

- **A fourth part of the platform's public interface, Single window, keeps one window at a time.** The Task list and Configure each make one in their create part, with a function that makes their window. Their Show part calls its Show:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  /// At most one window at a time: made when first shown, and again after the user closes it.
  public sealed class SingleWindow
  {
      /// Makes no window yet. createWindow makes a new, hidden window each time it's called.
      /// Create it on the UI thread. Throws ArgumentNullException when createWindow is null.
      public SingleWindow(System.Func<System.Windows.Window> createWindow);

      /// With no window yet, or after the user closed the last one: makes one with createWindow,
      /// then opens it with WindowPlacement.ShowOnPointerScreen. Otherwise passes the window it has
      /// to ShowOnPointerScreen, which brings an open one forward.
      /// Throws InvalidOperationException off the thread that created this object, before making
      /// a window, and when createWindow returns null. Passes on createWindow's exceptions, keeping
      /// no window, and ShowOnPointerScreen's.
      public void Show();
  }
  ```

- **The Task editor doesn't use it.** Add is made when the app starts, so it opens at once, and closing it only hides it, as the [Task editor](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) proposes.
- **The modules use the class itself, not an interface**, as they use Show on the pointer's screen. It has one implementation, and no test needs a fake of it, so a port would add an interface for nothing ([patterns](../overview.md#each-pattern-serves-one-requirement)).
- **Its tests are UX tests in the platform's test project**, each with a plain test window. The Task list's and Configure's own UX tests stay as they are, since what their Show does doesn't change.

## Later designs answer what these files leave open

The desktop design relies on rules that belong to the shared Tasks and Sync modules and to the technology stack, a later row of the [design backlog](../README.md#design-backlog). The table lists each, the desktop modules that need it, and the design that answers it:

| Needed | Used by | Answered by |
| --- | --- | --- |
| The rank that puts a new task first, and a task's rank after a move | Task editor, Task list | [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) |
| The tags in use, how often each is used, and when two tags are the same | Task editor, Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| How search and the tag filter match tasks | Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| The link format of a reference to another task | Task editor, Task list | [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it) |
| Where an image is saved, its path in the Markdown, its shrinking to 1 MB, and where an unsaved task's images wait | Task editor, Task list | [Tasks](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) |
| The allowed range of the deletion delay | Configure | [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) |
| What happens when another device changes a task that's being edited here | Task list | [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) |
| The repository address, the device's SSH key, and when a device counts as configured | Configure | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| When sync runs | Shell, Task list, Configure | [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) |
| The kinds of sync error, telling "offline" from an error, and the last successful sync time | Shell, Configure | [Sync through git](../sync.md#failure-modes) |
| What uninstalling does with the clone and the device's key | Shell | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| The release step that publishes installers to GitHub Releases, and whether the app carries its own .NET runtime | Shell | Technology stack |

## Delivery backlog

The desktop app is built in steps, one row each, and each row links to the design sections it builds. Steps 1 and 6, the desktop shell and install and updates, are scheduled; you choose each next row. Status is **Next**, **Not scheduled**, or **Implemented**, which means every behavior the row lists is built and tested.

Until its row is built, a feature is missing, and the app says so:

- **Each window says which of its features aren't built yet.**
- **Nothing pretends to work.** No step reports a save, a sync, or a setting it didn't make. Nothing shows the device as configured or unconfigured before Sync can tell.

Proposed, each a decision to confirm:

- **Until its own step, each window holds only a one-line notice that its feature is coming soon, and sizes to fit it**, so Add has no fields and no Save.
- **Until sync status is built, the tray icon's tooltip shows the app's name.**

The table lists each step, what it builds, and its status:

| # | Step | Builds | Status |
| --- | --- | --- | --- |
| 1 | Desktop shell: the tray app and its windows, with no task saved | The [startup](#the-platform-runs-the-app-and-the-composition-root-builds-it); Shell's [tray icon and menu](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions), without the shortcut or the redirect to Configure; the [hidden Add window](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title), without its fields; and the create and Show parts of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface) | Next |
| 2 | Shortcut, start with Windows, one instance, and theme | Shell's [shortcut](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions), and its [start with Windows, one instance, and theme](shell.md#the-app-starts-with-windows-and-runs-once-per-user); Configure's [device settings](configure.md#each-setting-has-one-owner) and [shortcut check](configure.md#a-new-shortcut-is-checked-at-once); [removing the `Run` key](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date) when the app is uninstalled; and a new look at the [tray icon failure](shell.md#failure-modes) | Not scheduled |
| 3 | Adding tasks | The rest of the [Task editor](task-editor.md#behavior), on [Tasks](../tasks.md), [Editing](../editing.md), and [Rendering](../rendering.md) | Not scheduled |
| 4 | Viewing tasks | The [Task list](task-list.md#behavior), except its Sync button | Not scheduled |
| 5 | Sync | [Sync through git](../sync.md); Shell's [sync status](shell.md#sync-stays-quiet-unless-something-is-wrong) and its redirect to Configure until the device is configured; Configure's [sync](configure.md#configure-shows-how-sync-is-doing) and deletion delay; the Task list's [Sync button](task-list.md#view-tasks-syncs-when-it-opens-and-on-its-sync-button) | Not scheduled |
| 6 | Install and updates | Shell's [installer and updates](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), except removing the `Run` key, which comes with step 2; and the [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) | Next |
