# How the desktop app is built

The desktop app is a Windows Presentation Foundation (WPF) app that lives in the notification area (the tray) and opens Add new Task, Task View, Contacts, and Configure. This page lists its seven modules, each with one design file in this folder, and the other designs they rely on. It also says how the app starts and the steps it's built in.

## Seven modules make up the desktop app

Each module is a project of its own, and the [module map](../modules.md#module-map) says which modules each may use. The table lists them in the order a user meets them:

| Module | What its design file decides |
| --- | --- |
| [Shell](shell.md) | The tray icon and its menu, the four global shortcuts, one running instance, what each start shows, the installer, and how sync status shows |
| [Task editor](task-editor.md) | The Add window: its fields, keys, draft, and tags. Task View edits a task's title or description with the same fields. |
| [Task list](task-list.md) | Task View: tasks grouped by tag, in groups that collapse, beside the selected task's description, editing a description or title in place, search, resolving, deleting, and the Sync button and the version in the status bar |
| [Contact list](contact-list.md) | The Contacts window: contacts grouped by file beside the selected contact's details, with search, editing in place, adding, deleting, and reordering groups |
| [Formatted view](formatted-view.md) | A task's title and description as a reader sees them, with no Markdown characters, the web view the description shows in, and which links open in the default browser |
| [Configure](configure.md) | The settings window: the four global shortcuts, the Close window and Save shortcuts, starting with Windows, the shared deletion delay, and sync status details |
| [Updates](updates.md) | When the app checks for a new version, the status bar Add and Task View share, What is new, Auto update, and how an update restarts the app |

Three shared modules serve the desktop app's screens: [Editing](../editing.md) holds the formatting and snippet commands, [Rendering](../rendering.md) turns Markdown into HTML and styled text, and [Contacts](../contacts.md) holds the contacts and each mention's text. [How the apps fit together](../overview.md) picks the main libraries, such as AvalonEdit for the editor and WebView2 for rendered descriptions.

Proposed: each window module names its own window, [Configure](configure.md#the-window-shows-its-settings-in-labeled-sections) names the global shortcuts, and Shell names its [tray menu items](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions). The tray items match the shortcuts' names, Add new task, Task View, and Configure, by choice, not by a rule, so either can change alone; the windows keep their own titles. This is a decision to confirm.

Each window module keeps its screens' state and logic in internal [view models](../overview.md#each-app-keeps-thin-view-models-rules-live-in-shared-modules), which the window shows and passes the user's actions to. A view model holds no WPF element and needs no WPF dispatcher, so the module's unit tests create it with fakes on any thread, as [Modules](../modules.md#a-module-is-one-capability-behind-a-small-interface) lets a module's own tests do. Logic that sits in a window today moves to a view model one module at a time, keeping its behavior.

## The platform runs the app, and the composition root builds it

The desktop platform, `Memoria.ToDo.Desktop.Platform`, holds the app's startup, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides. The composition root, `Memoria.ToDo.Desktop.App`, builds the app from the modules and hands it to the platform to run.

The platform's public interface has these parts: the three below, which are decided; a third part that [opens windows](#windows-open-on-the-screen-with-the-mouse-pointer), a fourth that [makes them again](#a-closed-window-is-made-again-on-the-next-show), and a fifth, the app's name, each proposed further down; the app's [version](#the-apps-version-has-one-home), which the user decided; and the [keyboard parts](#window-modules-read-keys-through-the-platform) the window modules share; and, proposed, [Close window keys](#the-close-window-shortcut-closes-any-of-the-apps-windows) and the theme follower, below the theme; and [Save keys](#the-save-shortcut-saves-in-any-window-that-edits), which the user decided. The decided parts are:

- **The app's lifetime**: an interface with two steps, start and stop. Start receives a way to end the app. [Shell](shell.md#public-interface) implements it, so the platform names no module. Proposed: the way to end the app works from any thread. The platform passes the call to the UI thread and returns without waiting, so a module that ends the app from a background task needs no thread rule of its own. This is a decision to confirm.
- **Run the app**: takes a function that builds the lifetime, and returns the process's exit code. In order, it:
  1. rejects a call from a thread that isn't a single-threaded apartment (STA) thread, which WPF needs ([threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)), before doing anything else;
  2. creates the WPF application, which ends only when told to, not when its last window closes, since a tray app has no main window ([ShutdownMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.shutdownmode));
  3. sets the WPF application's theme to follow Windows' light or dark setting, before any window exists, so every window and the tray menu follow it ([ThemeMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.thememode));
  4. builds the lifetime and starts it on the UI thread, so the modules' windows are made after the WPF application exists;
  5. when the lifetime ends the app, or Windows ends the session ([SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.sessionending)), stops the lifetime once, then lets WPF shut down, and returns.
- **The theme, the sixth part, says whether windows show the light or the dark theme**, and reports when Windows switches it. Run the app's third step already makes every window follow Windows' light or dark setting. This part reads that setting the way the pinned WPF does, only to tell modules which theme windows show: the app mode in Windows' settings; with no app mode, the Windows mode; with neither, dark ([WPF's source](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/ThemeManager.cs#L454-L465)). The [Task editor](task-editor.md#the-editor-styles-markdown-in-place-with-no-preview) and the Task list need it to pick each [palette color's](../rendering.md#six-colors-read-well-in-light-and-dark-themes) value. The composition root passes it to the modules, so their UX tests use a [fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test):

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  /// Whether the app's windows show the light or the dark theme, which follows Windows' setting.
  public interface ITheme
  {
      /// True while windows show the dark theme.
      bool IsDark { get; }

      /// Raised on the UI thread after Windows switches between light and dark.
      event System.EventHandler? Changed;
  }
  ```

  The platform's implementation is a public class that reads Windows' setting through a second interface, so tests can pass a fake setting and switch it. The platform implements that interface from Windows. The composition root makes both on the UI thread, in the function it passes to Run the app, after the WPF application exists, and passes the theme to each module that takes it:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  /// Windows' light or dark setting, read the way WPF reads it to pick the windows' theme.
  public interface IThemeSetting
  {
      /// Reads the setting now: true when windows show the dark theme.
      bool ReadIsDark();

      /// Raised, on any thread, when Windows may have changed the setting; reading tells whether it did.
      event System.EventHandler? MayHaveChanged;
  }

  /// The signed-in user's setting, from Windows. Making it reads and watches nothing.
  public sealed class WindowsThemeSetting : IThemeSetting
  {
      public WindowsThemeSetting();
      public bool ReadIsDark();
      public event System.EventHandler? MayHaveChanged;
  }

  /// The theme Windows' light or dark setting sets for the app's windows.
  public sealed class WindowsTheme : ITheme
  {
      public WindowsTheme(IThemeSetting setting);
      public bool IsDark { get; }

      /// Raised on the thread that made this object.
      public event System.EventHandler? Changed;
  }
  ```

  Making the theme fails at once, before it touches the setting. A missing setting throws ArgumentNullException, as the platform's other parts check their arguments. No WPF application, or a thread other than its UI thread, throws InvalidOperationException. Only then does the theme start watching the setting and read it, so a switch while it's being made isn't lost.

  Changed reports only the switches the UI thread sees. When the UI thread finds the setting changed since IsDark last said, it sets IsDark, then raises Changed once. Switches it never sees, such as to dark and straight back, raise nothing. A failure while it handles a switch, including one in a module's Changed handler, is an error no module handles, so it's shown and the app ends, as described below.

  The theme watches the setting until the WPF application's UI thread has shut down, then stops. Windows' setting watches Windows only while something listens: from the first listener added to the last one removed. So after shutdown, nothing watches Windows and nothing keeps the theme alive. The theme has no Dispose method, since the app's shutdown is its end.

  A setting value that isn't a number counts as missing, as WPF reads it, so IsDark still names the theme windows show.

  Not confirmed by a test: that Windows' setting reports a real switch, and what it reads when a value is missing or isn't a number. Only changing the signed-in user's own settings shows them, so no test does. Tests switch a fake setting instead, and read Windows' setting only to check what the real one reads.

  Seven options were ruled out:

  - **Run the app makes the theme and passes it to the function that builds the lifetime**: Run the app's built signature, its tests, and the composition root would change.
  - **Each window module reads WPF's theme itself**: the same code in two modules, and their UX tests couldn't switch between light and dark.
  - **With no app mode, the theme says light**: WPF then shows the Windows mode, or dark, so modules would color text for a theme no window shows.
  - **Changed is raised for every switch Windows makes**: switches the UI thread never sees would make modules recolor for a theme no window showed.
  - **The theme reads Windows itself, with no setting interface**: no test could make it see a switch, or fail while it handles one, without changing the user's setting.
  - **The theme needs only a WPF dispatcher, so its switch tests are unit tests**: a theme made on a background thread with its own dispatcher would work, and raise Changed where no window lives.
  - **An integration test that switches Windows' setting and switches it back**: it changes the user's own theme for every app, and a test that stops midway leaves it switched.

**Every button in the app shows the hand pointer**, as the user asked, so the user sees what can be clicked. Run the app sets this once, right after the theme in its third step, for every window and menu the app makes, so no module sets it and none can forget it. It covers push buttons and toggle buttons, such as Configure's section titles and the tag control's pill and x. It doesn't cover check boxes, radio buttons, or a scroll bar's arrows, which WPF also builds from button classes, since they don't read as buttons. A module may still show the hand over other things a click acts on, as [Task View's titles](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) do. It adds nothing to the platform's public interface. The rule holds for the whole process, so its test is one of Run the app's integration tests, each in a program of its own. The user decided that Run the app registers one class-wide rule for buttons ([Mouse.QueryCursor](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.mouse.querycursor), [EventManager.RegisterClassHandler](https://learn.microsoft.com/en-us/dotnet/api/system.windows.eventmanager.registerclasshandler)). An app-wide button style was ruled out, since it replaces the Fluent theme's button style unless based on it; so was a part each window attaches, since each new window and menu must remember it.

**Proposed: a platform part, the theme follower, calls a module back when the theme switches, but only while an element is on screen**, and once when the element comes back on screen, since the theme may have switched while it was away. So a closed window holds no subscription. The Formatted view has it today, and the Task editor keeps a copy of the same rule; Configure's [section titles](configure.md#the-window-shows-its-settings-in-labeled-sections) would be a third user, so it moves to the platform, as [code shared by two modules](../modules.md#the-platform-serves-every-module-and-knows-none) does. This is a decision to confirm:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// Calls themeChanged when the theme switches, while element is loaded, and once each time it loads.
/// Disposing it stops the calls.
public sealed class ThemeFollower : System.IDisposable
{
    /// Throws ArgumentNullException for a null argument.
    public ThemeFollower(System.Windows.FrameworkElement element, ITheme theme, System.Action themeChanged);
    public void Dispose();
}
```

**Cleanup runs while the lifetime's Windows resources still work.** The platform begins
[`IAppLifetime.StopAsync`](../../../src/Desktop/Platform/IAppLifetime.cs) on the UI thread and awaits its one owned
shutdown before ending the dispatcher ([integration tests](../../../tests/Desktop/Tests/Platform/AppRunnerIntegrationTests.cs)).
Repeated exit requests join that shutdown; they do not retry failed cleanup. A cleanup failure is shown and gives
a non-zero exit code, after the remaining cleanup is attempted.

The STA `Main` and [`AppRunner.Run`](../../../src/Desktop/Platform/AppRunner.cs) keep their synchronous signatures:
WPF's native message pump is not a wait on a Task. An async shutdown handler awaits cleanup while that pump remains
alive, then calls WPF shutdown. Startup failures and handled UI errors use the same path. There is no `.Wait()`,
`.Result`, or `GetAwaiter().GetResult()` bridge, and no async work left to a synchronous `finally` after the pump ends.
Event handlers may be async only at this event boundary, with their failures observed and reported.

Windows session ending and a fatal runtime exception can force process termination before
async cleanup finishes. They request the same once-only shutdown without cancelling Windows' logoff or promising
an impossible join. Normal Exit always awaits it. The five-second fatal-error allowance bounds best-effort
UI cleanup, not a blocking wait on a Task.

The SessionEnding handler starts that owned cleanup and returns without setting `Cancel`. It neither blocks nor
uses an unfinished shutdown Task as a reason to veto logoff. Cleanup observes its own failures while the process
and dispatcher remain alive; Windows may end either before completion. Only ordinary app exit keeps the dispatcher
alive until cleanup finishes. These paths share the once-only cleanup, not the same completion guarantee.

**The theme is experimental in WPF, so the build allows that at its one call.** The pinned SDK marks the theme setting with the compiler's experimental-API error, WPF0001 ([what's new in WPF for .NET 9](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)). The build allows WPF0001 only at the platform's one call, never for a whole project. Two options were ruled out:

- **Shell sets the theme when it starts**: it would change app-wide state the platform created, after the hidden Add window already exists.
- **The classic light look until the theme is no longer experimental**: the app would have no dark mode.

When the app starts, the composition root calls the create part of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface), giving Configure its [Start with Windows port](configure.md#public-interface), and the Task list the [Formatted view's](formatted-view.md#the-app-supplies-the-web-view-and-the-browser) web view and default browser. It builds Shell's lifetime from the results, the implementations of [Shell's ports](shell.md#public-interface), and whether the app started at sign-in, and passes it to Run the app. The windows themselves stay `internal`, as all of a module's code does ([Modules](../modules.md#a-module-is-one-capability-behind-a-small-interface)).

For the Sync step, App creates one
[synced repository](../modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload) first,
then one `ITaskStore` on it, and constructs one [`ISyncService`](../../../src/Shared/Sync/ISyncService.cs) with the
repository, that store's synchronization capability, the system
clock, the device data folder, the machine name, and the desktop interval specified by [Sync](../sync.md#public-interface).
Only App chooses the filesystem, secure-key, repository-selection, and setup-action adapters. It passes Tasks'
typed settings to Configure and the same Sync instance to Configure, Task list, and Shell. No factory gets a dummy
service, optional test switch, or old overload unused by production. For colored tags, it passes the store's
[tag colors](../tasks.md#tag-colors-are-a-capability-of-their-own) to the Task editor and the Task list.

Proposed, with [Contacts](../contacts.md#contacts-is-a-shared-module-of-its-own), a
decision to confirm: App also gives the repository to a new Contacts store, and Sync the Contacts store's file rules,
so both stores load the same folder at once. It passes the Contacts store's `IContacts` to the
[Contact list](contact-list.md#public-interface), the [Task editor](task-editor.md#public-interface), and the
[Task list](task-list.md#public-interface), and the Contacts window to Shell. This is not implemented.

The selection adapter reuses `repository-folder.txt`, selects through the synced repository, and never clones, invokes Git, or
stores a second path. Failed adoption restores the previous saved path and selection; rollback failures
retain the original error too. Its initial load follows [Sync's approved initialization boundary](../sync.md#public-interface),
not composition. Client Git/SSH operations belong only to Sync. The web-view factory reads the
selected folder from the synced repository when making each new web view, and gives it the store's
[folder of waiting edit images](../tasks.md#step-5-publishes-small-capabilities-without-growing-task-operations) too, so an edit's
Preview shows [images not saved yet](formatted-view.md#the-clones-address-also-serves-an-open-edits-waiting-images). The setup-action adapter reuses App's existing clipboard
and default-browser components, without adding dependencies between Configure and the window modules.

Decision to confirm: App supplies `%LocalAppData%` joined with the platform's `AppName.Value` as the device folder.
That is the existing selected-path file's home, not a new Sync subfolder: the file stays in place, with no second
copy or migration. The app-name value still comes from the build's Product property, not another literal.
The existing draft and browser-data folders stay where they are. Sync enforces the
[device-folder boundary](../sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one) when accepting a clone.

The secure-key adapter implements [Sync's key port](../../../src/Shared/Sync/DeviceKeys/IDeviceKeyStore.cs), using
the [approved Windows envelope](../sync.md#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it).
Its constructor does no I/O or crypto; all key operations run on Sync's owned worker. ProtectedData is already in the pinned WindowsDesktop framework's WPF reference and runtime sets, so App needs no
additional package. The runtime, SDK, and tools are unchanged.

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

### The app's version has one home

The user decided that the app's version, which Task View's [status bar](task-list.md#a-status-bar-holds-the-sync-button-and-the-version) shows, comes from a platform part, App version, beside App name, so no module's `Create` takes it. Its only home is the build: the [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) builds the app with the version tag's number, and a local build is 1.0.0, the .NET SDK's default.

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// The app's version as users see it, such as 1.4.2.
public static class AppVersion
{
    /// The version the build stamps into the platform assembly's informational version,
    /// without the "+" and commit the .NET SDK adds after it.
    /// Throws InvalidOperationException when the assembly has no informational version.
    public static string Value { get; }
}
```

Proposed, each a decision to confirm:

- **It reads the platform assembly's informational version**, as App name reads its product, so the whole app shows one version: the build passes the same `Version` to every project.
- **Its tests are unit tests**: the version has the form of numbers with dots, and holds no `+`.
- **Updates' install adapter reads it too.** The adapter in the composition root gives [Updates](updates.md#public-interface) Velopack's version when the app is installed, and otherwise the build's: that one is App version's value, so the adapter reads no informational version of its own. An installed copy's two versions match, since a [version tag](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) gives its number to both the app's files and its Velopack package.

### Windows open on the screen with the mouse pointer

Add, Task View, and Configure open the same way, so the rule has one home: the platform, since [code shared by two modules moves to the platform](../modules.md#the-platform-serves-every-module-and-knows-none). Proposed, each a decision to confirm:

- **A third part of the platform's public interface, Show on the pointer's screen, opens a module's window.** The [Task editor](task-editor.md#public-interface) calls it with its Add window. The [Task list](task-list.md#public-interface) and [Configure](configure.md#public-interface) reach it through the [fourth part](#a-closed-window-is-made-again-on-the-next-show). These two, the [theme](#the-platform-runs-the-app-and-the-composition-root-builds-it), and the [keyboard parts](#window-modules-read-keys-through-the-platform) are the only platform parts the window modules use:

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
- **Activated means in front of other windows, with the keyboard focus, but not always on top.** Windows may refuse to bring a window to the front while the user works in another app, and flashes its taskbar button instead ([SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)). Not confirmed by a run: that a click on the tray icon, or a global shortcut, lets the app come to the front.
- **A window hidden while minimized opens in the normal state**, centered like any hidden window. This happens when the user closes the minimized Add window from its taskbar button. Windows can't restore a window without showing it, so the platform restores it while it's cloaked, invisible to the user, and hides it again before placing it ([DWMWA_CLOAK](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)). Proposed, a decision to confirm. Not confirmed by eye: that nothing flashes on the screen or the taskbar.
- **The app is aware of each screen's own scale**, declared in the composition root's app manifest, so windows stay sharp on a screen whose scale differs from the main one. WPF apps use one scale for every screen unless they declare this ([per-monitor DPI](https://github.com/microsoft/WPF-Samples/tree/main/PerMonitorDPI)). Not confirmed by a run: that a window opens centered on a screen whose scale differs from the main one, since WPF may place a window that isn't shown yet at the main screen's scale.
- **Its tests are the platform's integration tests**, each showing a plain test window on the real screen. The window modules' UX tests check that Show shows and activates their window, not where it opens.

Windows Forms' `Screen` class, which finds the pointer's screen, was ruled out: it would load Windows Forms into the app for one lookup, and Windows' own functions give the same answer.

### A closed window is made again on the next Show

Task View and Configure close when the user closes them, and WPF can't show a closed window again ([Window.Show](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.show)). Both need the same rule, so it has one home in the platform. Proposed, each a decision to confirm:

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
- **Its tests are the platform's UX tests**, each with a plain test window. The Task list's and Configure's own UX tests stay as they are, since what their Show does doesn't change.

### Window modules read keys through the platform

The Task list, the Task editor, and Configure read keys the same way, so that knowledge has one home: the platform, since [code shared by two modules moves to the platform](../modules.md#the-platform-serves-every-module-and-knows-none). The platform has three keyboard parts. Two were already in its public interface: `ModifierKey` says which modifier a key stands for and names modifiers for users, and `HeldModifierKeys` tracks the modifiers held down.

**The third, Event key, gives the key a key event is for**, as you chose. WPF reports a key pressed with Alt as `Key.System`, with the key itself in `SystemKey` ([KeyEventArgs.SystemKey](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.keyeventargs.systemkey)). The three modules use this part instead of each unwrapping it in a copy of its own:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

public static class EventKey
{
    /// The key a key event is for: SystemKey when WPF reports Key.System, for a key pressed
    /// with Alt; otherwise Key, unchanged.
    public static System.Windows.Input.Key Of(System.Windows.Input.KeyEventArgs e);
}
```

- **It unwraps only Alt.** A key an input method or a dead key handles stays as WPF reports it, so the Enter that confirms an input method's text isn't taken as the Task list's or the editor's Enter. Only Configure's shortcut field also unwraps those two itself, so a [shortcut](configure.md#public-interface) is always made of a real key.
- **What each key does stays in the design of the module that reads it**: the Task list's [keys](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete) and [search box](task-list.md#search-narrows-the-list), the [Task editor's keys](task-editor.md#keys-save-close-and-move-between-fields), and [Configure's shortcut checks](configure.md#configure-explains-every-shortcut-it-cant-use).

Two other homes were ruled out: a method on `ModifierKey`, since its one job is saying which keys are modifiers; and one copy shared inside the Task list only, which would leave the same code in the Task editor and Configure.

**A fourth part closes a window when Esc reaches it unused**, since the user decided that Esc closes every window, and Task View and Configure would otherwise each close on Esc in a copy of their own. A control that uses Esc first, such as an open list or menu, marks the key handled, so the window stays open. Add doesn't need the part: its editor already [hides Add on Esc](task-editor.md#keys-save-close-and-move-between-fields) after closing an open list. What uses Esc first in Task View is the Task list's [steps](task-list.md#esc-goes-back-one-step-then-closes-task-view), and in Configure its [shortcut fields](configure.md#esc-and-the-close-window-shortcut-close-the-window). The user approved the part, which adds to the platform's public interface:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

public static class EscCloses
{
    /// Closes the window when an Esc key press, with no modifier key held, reaches it
    /// with no control having handled it.
    public static void Attach(System.Windows.Window window);
}
```

The [single window](#a-closed-window-is-made-again-on-the-next-show) could close on Esc instead, since Task View and Configure both use it, but its one job is where a window opens and what happens after it closes.

Proposed: the Windows key counts as a modifier, as it does for `ModifierKey`, so Win+Esc doesn't close the window. Windows uses Win+Esc to close Magnifier ([keyboard shortcuts in Windows](https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows-dcc61a57-8ff0-cffe-9796-cb9706c75eec)). This is a decision to confirm.

### The Close window shortcut closes any of the app's windows

The [Close window shortcut](configure.md#esc-and-the-close-window-shortcut-close-the-window) closes Add, Task View, and Configure the same way, so its key handling has one home: a platform part, Close window keys. The composition root makes one for the app and passes it to Configure, the Task editor, and the Task list:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// The keys that close whichever of the app's windows has the focus, whatever control in it has the focus.
/// The composition root makes one for the app. Use it on the UI thread.
public sealed class CloseWindowKeys
{
    /// No keys yet: no key closes a window until Set is called.
    public CloseWindowKeys();

    /// From now on, these keys close each attached window. Throws ArgumentException when key is Key.None
    /// or a modifier key, or modifiers holds anything but Ctrl, Alt, Shift, and Win.
    public void Set(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key);

    /// From now on, pressing the current keys while window, or any control in it, has the focus calls
    /// window.Close() before any control sees the keys, and the keys go no further.
    /// Throws ArgumentNullException when window is null.
    public void Attach(System.Windows.Window window);
}
```

- **Closing calls the window's own close**, so each window does what its close button does: Add hides and keeps its draft, as its [closing](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) already does, and Task View and Configure close.
- **It works before any control sees the keys**, unlike [Esc closes](#window-modules-read-keys-through-the-platform), which acts only on a plain Esc no control used. So the Close window shortcut closes a window even while a list or menu in it is open.
- **Configure alone decides the keys**: their default, rules, and storage are [Configure's](configure.md#a-new-shortcut-is-checked-at-once). The platform checks only that they're keys at all.
- **The modules use the class itself, not an interface**, as they use [Single window](#a-closed-window-is-made-again-on-the-next-show): it has one implementation and does no I/O, so tests use the real one.
- **Its tests are the platform's UX tests**, each with a plain test window holding a text box.

### The Save shortcut saves in any window that edits

The [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window) saves in Add, and in Task View's Edit Description and Edit Title, so its keys have one home: a platform part, Save keys, the user decided. The composition root makes one for the app and passes it to Configure, which sets it, and to the Task editor and the Task list, whose editors ask it whether a key press is the Save shortcut:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// The keys that save the task being edited, in whichever of the app's windows edits one.
/// The composition root makes one for the app. Use it on the UI thread.
public sealed class SaveKeys
{
    /// No keys yet: no key press is the Save shortcut until Set is called.
    public SaveKeys();

    /// From now on, these keys save. Throws ArgumentException when key is Key.None or a modifier key,
    /// or modifiers holds anything but Ctrl, Alt, Shift, and Win.
    public void Set(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key);

    /// Whether key, pressed with exactly these modifier keys held, is the Save shortcut now.
    public bool Matches(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key);

    /// The Save shortcut as the user reads it, such as "Ctrl+Enter"; empty until Set is called.
    public string Text { get; }
}

/// Writes a shortcut the way the app shows it to the user.
public static class ShortcutText
{
    /// The modifiers in the order Win, Ctrl, Alt, Shift, then the key, such as "Win+Alt+N"; Enter is written "Enter".
    public static string Of(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key);
}
```

Proposed, each a decision to confirm:

- **The platform owns shortcut text.** `ShortcutText` is the one place that writes a shortcut for the user, so Configure's fields and messages, and a tooltip that names the Save shortcut, read the same. `SaveKeys` offers its text so a window, such as the Add window's Save button tooltip, reads it when it opens without asking Configure.
- **An editor asks it on each key press**, with the modifier keys it already tracks through [Held modifier keys](#window-modules-read-keys-through-the-platform), so a change applies to an edit already open, and the editor needs no event.
- **It checks keys as Close window keys does**, through one key check both share, so the rule that a shortcut is real keys has one home. Configure alone decides the keys' default and rules.
- **The modules use the class itself, not an interface**, as for Close window keys: it has one implementation and does no I/O, so tests use the real one. Its tests are unit tests.

## Split panes, search band, and drag parts serve every window that needs them

The user decided that Task View's split bar and search band move into the platform, so Task View and Contacts share one of each, and Task View works exactly as before. Proposed, each a decision to confirm:

- **The group drag's parts move with them**, since Contacts' [group drag](contact-list.md#dragging-a-groups-heading-reorders-the-groups) is their second use: the distance a press must move before a drag starts, the drop line, scrolling near a list's edges during a drag, and the mouse behind them. The edge band and distance check they use stay inside the platform.
- **Each part keeps the look and numbers it has in Task View**: a 6-pixel bar that takes no focus, and the "Search:" label with its box in the status bar's shade. Each window passes its own column shares and minimum widths.
- **They're platform classes, not interfaces**, as the Close window keys are: each has one implementation, and a window's UX tests use the real ones. The mouse sits behind a small interface, so tests drive a drag with a fake pointer.

Proposed, a decision to confirm. These are not implemented; they move from Task View's internal classes:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// Two sides beside each other, and a bar between them the user drags to move the split. The bar takes no focus.
public sealed class SplitPanes : Grid
{
    /// The sides share the width in the ratio leftShare to rightShare, each at least its minimum width.
    public SplitPanes(UIElement left, double leftShare, double leftMinWidth, UIElement right, double rightShare, double rightMinWidth);
}

/// The "Search:" label and its box, edge to edge in the status bar's shade.
public sealed class SearchBand : Border
{
    /// The box's text. Setting it changes the box without raising TextChanged.
    public string Text { get; set; }

    /// Raised when the user changes the text.
    public event EventHandler? TextChanged;

    /// Raised on Down (false) or Up (true) in the box with no modifier key, to select the top or bottom row.
    public event EventHandler<bool>? EdgeRowRequested;

    /// Moves focus to the box and selects its text.
    public void FocusText();
}

/// Tells a press from a drag: a drag starts once the pointer moves the given distance with the button held.
public sealed class PressDrag(double horizontalDistance, double verticalDistance)
{
    public void Press(Point at);

    /// True once, when this move starts a drag.
    public bool Move(Point now, bool leftButtonDown);
    public void Leave();

    /// True when the release ends a press that never became a drag, so it counts as a click.
    public bool Release();
}

/// The line that shows where a dragged item lands.
public sealed class DropLine : Adorner
{
    public DropLine(UIElement adorned);
    public void Show(double x, double y, double width);
    public void Hide();
}

/// Scrolls the viewer while a drag holds the pointer near its top or bottom edge, and reports the pointer after each step.
public static class AutoScroll
{
    public static void Attach(ScrollViewer viewer, TimeProvider time, Action<Point> scrolled);
}

/// The mouse, as a drag sees it.
public interface IDragPointer
{
    Point PositionIn(UIElement element);
    void StartDrag(DependencyObject source, IDataObject data);
}

public sealed class MousePointer : IDragPointer;
```

## Later designs answer what these files leave open

The platform's shared [question dialog](../../../src/Desktop/Platform/QuestionDialog.cs) asks modal questions for Task View, Contacts, and Configure. It follows the theme, centers on its owner, and shows ordered answer buttons. Enter picks the default answer; Esc and closing pick Cancel. Its text wraps at 420 device-independent pixels, proposed for confirmation.

The desktop design relies on rules that belong to the shared Tasks and Sync modules and to the technology stack, a later row of the [design backlog](../README.md#design-backlog). The table lists each, the desktop modules that need it, and the design that answers it:

| Needed | Used by | Answered by |
| --- | --- | --- |
| The rank that puts a new task first, and a task's rank after a move | Task editor, Task list | [Tasks](../tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) |
| The tags in use, how often each is used, and when two tags are the same | Task editor, Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| Each tag's color, and the 30 colors a tag can take | Task editor, Task list | [Tasks](../tasks.md#each-tags-color-is-kept-in-one-synced-file) and [Rendering](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes) |
| The order of the tag groups, and a group's new place after a move | Task list | [Tasks](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) |
| How search matches tasks | Task list | [Tasks](../tasks.md#tags-and-search-ignore-case) |
| The link format of a reference to another task | Task editor, Task list | [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it) |
| The contacts, their relations, search, and saving, and the stored format of a mention | Contact list, Task editor, Task list | [Contacts](../contacts.md) |
| Where an image is saved, its path in the Markdown, its shrinking to 1 MB, and where an unsaved task's images wait | Task editor, Task list | [Tasks](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) |
| Where a new task's draft waits, and when it's deleted | Task editor | [Tasks](../tasks.md#a-draft-waits-on-this-device-until-its-saved) |
| The allowed range of the deletion delay | Configure | [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) |
| What happens when another device changes a task that's being edited here | Task list | [Tasks](../tasks.md#two-versions-of-a-task-merge-field-by-field) |
| The repository address, the device's SSH key, and when a device counts as configured | Configure | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| When sync runs | Shell, Task list, Configure | [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) |
| The kinds of sync error, telling "offline" from an error, and the last successful sync time | Shell, Configure | [Sync through git](../sync.md#failure-modes) |
| What uninstalling does with the clone and the device's key | Shell | [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) |
| The release step that publishes installers to GitHub Releases, and whether the app carries its own .NET runtime | Shell | Technology stack |

## Delivery backlog

The desktop app is built in steps, one row each, and each row links to the design sections it builds. Steps 1 to 5, 7 and 8 are built. Step 5 leaves two checks that no test can make yet to Sync's [validation backlog](../sync.md#implementation-and-focused-checks). You choose the remaining delivery order, recorded below the table. Status is **Next**, **Not scheduled**, or **Implemented**, which means every behavior the row lists is built and tested.

The table lists each step, what it builds, and its status:

| # | Step | Builds | Status |
| --- | --- | --- | --- |
| 1 | Desktop shell: the tray app and its windows, with no task saved | The [startup](#the-platform-runs-the-app-and-the-composition-root-builds-it); Shell's [tray icon and menu](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions), without the shortcuts or the redirect to Configure; the [hidden Add window](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title), without its fields; and the create and Show parts of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface) | Implemented |
| 2 | Two shortcuts, start with Windows, one instance, and theme | Shell's [two shortcuts](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions), with Configure opening when one can't register, and its [one instance and what each start shows](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view); the platform's [theme that every window follows](#the-platform-runs-the-app-and-the-composition-root-builds-it); Configure's [shortcut and Start with Windows settings](configure.md#each-setting-has-one-owner), with the shortcuts not saved yet, in [labeled sections](configure.md#the-window-shows-its-settings-in-labeled-sections), with its [shortcut check](configure.md#a-new-shortcut-is-checked-at-once), the shortcuts [released while a field has focus](configure.md#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released), a [message for every shortcut it can't use](configure.md#configure-explains-every-shortcut-it-cant-use), and the [`Run` key](configure.md#start-with-windows-is-the-run-key-itself) that installing writes and uninstalling removes; and the [Add window's size](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) | Implemented |
| 3 | Adding tasks | The platform's [theme part](#the-platform-runs-the-app-and-the-composition-root-builds-it), which tells modules whether windows are light or dark, built first; then the rest of the [Task editor](task-editor.md#behavior), on [Tasks](../tasks.md), the shared library's [platform](../modules.md#the-file-system-interface-reads-and-writes-whole-files), [Editing](../editing.md), and [Rendering's](../rendering.md#public-interface) styled spans and palette | Implemented |
| 4 | Viewing tasks | The [Formatted view](formatted-view.md#behavior), and the [Task list](task-list.md#behavior) except its Sync button and the sync when Task View opens | Implemented |
| 5 | Sync | [Sync through git](../sync.md); Shell's [sync status](shell.md#sync-stays-quiet-unless-something-is-wrong) and its redirect to Configure until the device is configured; Configure's [sync](configure.md#configure-shows-how-sync-is-doing), deletion delay, and saving and loading the [shortcuts](configure.md#each-setting-has-one-owner) in `settings.json`; the Task list's [Sync button and the sync when Task View opens](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button). | Implemented |
| 6 | Install and updates | Shell's [installer](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), except removing the `Run` key, which comes with step 2; and the [release workflow](../technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), with each release's notes from the [changelog](../version-tags.md#a-version-is-tagged-only-when-the-changelog-has-its-notes). Proposed: the update checks now come with step 10, since [Updates](updates.md) replaces the background download, and the release notes come with this step, so the first release already has them. This is a decision to confirm. | Not scheduled |
| 7 | Esc and switching windows | The platform's [Esc part](#window-modules-read-keys-through-the-platform); Esc in [Task View](task-list.md#esc-goes-back-one-step-then-closes-task-view) and [Configure](configure.md#esc-and-the-close-window-shortcut-close-the-window); Shell's [shortcut switching](shell.md#a-shortcut-switches-between-add-and-task-view), with the close parts of [Add](task-editor.md#public-interface) and [Task View](task-list.md#public-interface); and Task View's [prompt for a changed edit](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) | Implemented |
| 8 | Colored tags | Each tag's color in [Tasks](../tasks.md#each-tags-color-is-kept-in-one-synced-file), and Sync carrying `tags.json`; Rendering's [30 tag colors](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes); the Task editor's [badges and + menu](task-editor.md#a-task-has-one-tag-shown-as-one-pill); and the Task list's [colored tags and tag changes without Edit](task-list.md#tags-show-as-colored-badges-and-change-without-edit) | Implemented |
| 9 | Text look and Task View by tag | Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme) on the description page, in [titles](formatted-view.md#it-shows-what-rendering-decides-never-markdown), in the [editor](task-editor.md#the-editor-styles-markdown-in-place-with-no-preview), and on [Configure's section titles](configure.md#the-window-shows-its-settings-in-labeled-sections); the platform's [hand pointer on every button and theme follower](#the-platform-runs-the-app-and-the-composition-root-builds-it); and Task View's [tag groups beside the description](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description), with [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor) | Not scheduled |
| 10 | Updates you agree to | [Updates](updates.md): the checks at start and at 3 AM, the [status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) in [Add](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) and [Task View](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button), Configure's [Update card](configure.md#the-update-card-leads-the-application-section), What is new, Auto update, and the restart | Implemented |
| 11 | Task View refinements | Rendering's [heading colors](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme), also on [Configure's card titles](configure.md#the-window-shows-its-settings-in-labeled-sections); the Task editor's [lowercase tags with no spaces](task-editor.md#a-task-has-one-tag-shown-as-one-pill); and Task View's [status bar](task-list.md#a-status-bar-holds-the-sync-button-and-the-version), [search box](task-list.md#search-narrows-the-list) with no tag filter, [collapsing groups](task-list.md#a-click-on-a-groups-tag-collapses-or-expands-it), [description-only edit](task-list.md#edit-description-turns-the-right-side-into-the-editor), [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row), [resizable images](task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview) in Edit Description's [Preview](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab), and [no focus rectangle on the split bar or the list](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete); with the platform's [app version](#the-apps-version-has-one-home) and [Save keys](#the-save-shortcut-saves-in-any-window-that-edits), Configure's [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window), and Tasks' [Save shortcut setting](../tasks.md#one-versioned-file-holds-the-three-shared-settings). | Next |
| 12 | Configure's Application section and its shortcut | Configure's [Application section](configure.md#the-window-shows-its-settings-in-labeled-sections), with the [Update card](configure.md#the-update-card-leads-the-application-section) and the Shortcuts card; the [Configure shortcut](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) in Configure and Shell; and Tasks' [Configure shortcut setting](../tasks.md#one-versioned-file-holds-the-three-shared-settings). | Implemented |
| 13 | Group order and the drop line | Task View's [group drag, drop line, and scrolling during a drag](task-list.md#drag-and-drop-moves-tasks-and-groups-and-a-line-shows-where-they-land), with its groups in [the tags' order](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description); Tasks' [tag order](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) in `tags.json` version 2, with its [capability](../tasks.md#tag-order-is-a-capability-of-its-own) and merge. Sync carries `tags.json` as before, with no change. The Android app's [tag chips](../android/task-list.md#tags-and-search-narrow-the-list) follow the order when the Android app is built. | Not scheduled |
| 14 | One tag per task | Tasks' [one-tag rule](../tasks.md#a-task-has-at-most-one-tag) in `Add`, saving an edit, the merge, and kept drafts; the Task editor's [tag pill](task-editor.md#a-task-has-one-tag-shown-as-one-pill) and its [menu](task-editor.md#a-click-on-the-pill-opens-the-tag-menu), with the `TagEditor` interface the user decided; and the Task list's [tag changes without Edit](task-list.md#tags-show-as-colored-badges-and-change-without-edit), a [tag that joins an open edit's one save](task-list.md#a-tag-chosen-during-an-edit-saves-with-the-edit), and one group per task. | Next |
| 15 | Contacts | Built in slices, in this order, each reviewed on its own: (a) the platform's [synced repository](../modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), with Tasks, Sync, and App moved onto it and no change users notice; (b) the shared [Contacts](../contacts.md) module, with the platform's [contact address](../modules.md#a-contact-address-has-one-home), and Tasks' JSON style, version check, skip rule, and path check moved into the platform's [shared file rules](../modules.md#stores-share-one-json-style-and-one-set-of-file-rules) with no change users notice; (c) Sync [carrying contact files](../sync.md#sync-manages-contact-files-through-contacts-rules), with App's composition root creating the Contacts store; (d) the platform's [split panes, search band, and drag parts](#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), with Task View moved onto them unchanged; (e) the [Contact list](contact-list.md), with Shell's and Configure's [Contacts shortcut](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) and tray item; (f) Rendering's [mentions](../rendering.md#a-mention-shows-on-a-colored-background) and Editing's [mention list](../editing.md#typing-an-at-sign-opens-the-mention-list); (g) the [Task editor's "@" list and chips](task-editor.md#typing-an-at-sign-mentions-a-contact), then Task View's [live mention text](task-list.md#mentions-show-each-contacts-current-details). The Android app's mentions come when the Android app is built. | Next |
| 16 | Tasks' notices after a change | Tasks raises its notices one by one after a change: `Changed`, then settings, tag colors, and tag order, then the [local save report](../tasks.md#sync-shares-the-repositorys-lock-never-a-network-wait). When a handler throws, the notices after it and the report are skipped, so Sync doesn't learn of the save. This step makes Tasks attempt every notice and the report, even when one fails, and throw the failures by the platform's [rule for steps that must all run](../modules.md#steps-that-must-all-run-throw-their-failures-together), as [Contacts](../contacts.md#failure-modes) does. Its design is recorded in [Tasks](../tasks.md) when the step starts. | Next |

The remaining work follows this order:

1. Tasks' notices after a change (step 16). The user put it ahead of the rest, pausing Contacts after slice (b).
2. Contacts (step 15), slice by slice from (a), continuing with slice (c). The user started it ahead of the rest.
3. One tag per task (step 14). The user decided it's built ahead of step 11.
4. Task View refinements (step 11), which you asked for ahead of the rest.
5. Install and updates (step 6).
6. Review and refine [Tasks View](task-list.md), including its decisions awaiting confirmation. This review is deferred, not waived.
7. Add the [Add window's formatted preview](task-editor.md).
8. Build the [Android app](../android/README.md).
