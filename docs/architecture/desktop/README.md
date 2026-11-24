# How the desktop app is built

The desktop app is a Windows Presentation Foundation (WPF) app that lives in the notification area (the tray) and opens Add new Task, Task View, Contacts, Configure, and Logs. This page lists its nine modules, each with one design file in this folder, and the other designs they rely on. It also says how the app starts and the steps it's built in.

## Nine modules make up the desktop app

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
| [Logs](logs.md) | The window the tray menu opens to read the app's [log](../logging.md): entries newest first, with filters by time, severity, and component, and a quick search |
| [Local API](../local-api.md) | The gRPC server, with no window, that lets your own programs on this PC reach tasks, contacts, sync, and shared settings over a named pipe only your account opens. Its design file sits beside the shared modules' pages, with the [contract](../local-api-contract.md) its clients share. |

The composition root creates the Local API with the task store, Contacts, Sync, its pipe name, and its logger, and passes its lifetime to Shell, which [starts and stops it](shell.md#public-interface) as it does Updates.

Every module logs its operations and failures through Serilog, as [Logging](../logging.md) decides.

Three shared modules serve the desktop app's screens: [Editing](../editing.md) holds the formatting and snippet commands, [Rendering](../rendering.md) turns Markdown into HTML and styled text, and [Contacts](../contacts.md) holds the contacts and each mention's text. [How the apps fit together](../overview.md) picks the main libraries, such as AvalonEdit for the editor and WebView2 for rendered descriptions.

Proposed: each window module names its own window, [Configure](configure.md#the-window-shows-its-settings-in-labeled-sections) names the global shortcuts, and Shell names its [tray menu items](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions). The tray items match the shortcuts' names, Add new task, Task View, Configure, and Contacts, by choice, not by a rule, so either can change alone; the windows keep their own titles. This is a decision to confirm.

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

**Every button in the app shows the hand pointer**, as the user asked, so the user sees what can be clicked. Run the app sets this once, right after the theme in its third step, for every window and menu the app makes, so no module sets it and none can forget it. It covers push buttons and toggle buttons, such as Configure's section titles and the tag control's pill and x. It doesn't cover check boxes, radio buttons, or a scroll bar's arrows, which WPF also builds from button classes, since they don't read as buttons. A module may still show the hand over other things a click acts on, as [Task View's checkboxes](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) do. It adds nothing to the platform's public interface. The rule holds for the whole process, so its test is one of Run the app's integration tests, each in a program of its own. The user decided that Run the app registers one class-wide rule for buttons ([Mouse.QueryCursor](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.mouse.querycursor), [EventManager.RegisterClassHandler](https://learn.microsoft.com/en-us/dotnet/api/system.windows.eventmanager.registerclasshandler)). An app-wide button style was ruled out, since it replaces the Fluent theme's button style unless based on it; so was a part each window attaches, since each new window and menu must remember it.

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
alive, then calls WPF shutdown. Startup failures and UI errors that end the app use the same path. There is no `.Wait()`,
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
[Task list](task-list.md#public-interface), and the Contacts window to Shell. The store and Sync's part are built.
Slice (e) of step 15 makes the Contacts window with the store's `IContacts`, the theme, the system clock, the Close
window keys, the Save keys, and the one `MousePointer` Task View gets, and passes it to Shell after Task View's window.
The Task editor and the Task list get `IContacts` in later slices.

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

**An error no module handles is logged, then shown; once the app has started, an error on the UI thread doesn't end it.** The user decided this, so the app survives one bad click. The platform first [logs the error](../logging.md#errors-no-module-handles-are-logged-before-theyre-shown), then shows its message in a message box:

- **On the UI thread, after the app has started** ([DispatcherUnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception)): the platform marks the error handled, and the app keeps running. The box adds that the app keeps running and the details are in the log. Only one such box shows at a time: an error while it's open is only logged.
- **While the app starts or stops**: the box shows, then the app ends with a non-zero exit code, as before, since there's no tray icon to keep.
- **On any other thread** ([UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception)): the box shows, then the app ends, since .NET ends the process anyway.
- **In a task nobody waited for** ([UnobservedTaskException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception)): it's only logged, and marked observed, so nothing else changes.

This keeps [fail fast](../../../AGENTS.md#code-design): no error is swallowed or turned into success. Each is logged and shown, and the operation that failed has stopped. Stores save each file whole, so a failed click leaves no half-written file behind. Each module still reports its own expected failures, such as a sync error. Ruled out, by the user: **ending the app after every such error**, which loses the tray app over one bad click; and **catching every error at the start of each background thread and timer** and going on, which would let a background loop, such as Sync's worker, run on in a broken state with no one told. Catching nothing, WPF's default, was ruled out earlier: the app would vanish from the tray with no message.

Proposed, each a decision to confirm:

- **A fifth part of the platform's public interface, App name, gives the app's name** to the message box and Shell's tray tooltip. Its only home is the build's `Product` property, as the [technology stack](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) proposes:

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
  - **The installer.** The [release workflow](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) passes the same file to Velopack's packing step ([vpk options](https://docs.velopack.io/reference/cli/content/vpk-windows)).

### The app's version has one home

The user decided that the app's version, which Task View's [status bar](task-list.md#a-status-bar-holds-the-sync-button-and-the-version) shows, comes from a platform part, App version, beside App name, so no module's `Create` takes it. Its only home is the build: the [release workflow](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) builds the app with the version tag's number, and a local build is 1.0.0, the .NET SDK's default.

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
- **Updates' install adapter reads it too.** The adapter in the composition root gives [Updates](updates.md#public-interface) Velopack's version when the app is installed, and otherwise the build's: that one is App version's value, so the adapter reads no informational version of its own. An installed copy's two versions match, since a [version tag](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) gives its number to both the app's files and its Velopack package.

### Windows open on the screen with the mouse pointer

Add, Task View, and Configure open the same way, so the rule has one home: the platform, since [code shared by two modules moves to the platform](../modules.md#the-platform-serves-every-module-and-knows-none). Proposed, each a decision to confirm:

- **A third part of the platform's public interface, Show on the pointer's screen, opens a module's window.** The [Task editor](task-editor.md#public-interface) calls it with its Add window. The [Task list](task-list.md#public-interface) and [Configure](configure.md#public-interface) reach it through the [fourth part](#a-closed-window-is-made-again-on-the-next-show). These two, the [theme](#the-platform-runs-the-app-and-the-composition-root-builds-it), the [keyboard parts](#window-modules-read-keys-through-the-platform), the [question dialog](#later-designs-answer-what-these-files-leave-open), the [modal dialog](#windows-open-on-the-screen-with-the-mouse-pointer), and the [split panes, search band, and drag parts](#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them) are the platform parts the window modules use:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  public static class WindowPlacement
  {
      /// A hidden window: shown centered on the work area of the screen with the mouse pointer,
      /// then activated. A window shown for the first time stays invisible on the screen until WPF
      /// has drawn it, which happens after this call returns. A visible window stays where it is:
      /// restored if minimized, then activated. Logs its choices at Debug through logger, the
      /// caller's own.
      /// Throws ArgumentNullException when logger is null, InvalidOperationException off the
      /// window's UI thread, and Win32Exception when Windows can't give the pointer's position, or
      /// its screen's work area or scale, or move, restore, or cloak the window.
      public static void ShowOnPointerScreen(System.Windows.Window window, Serilog.ILogger logger);
  }
  ```

- **The work area is the screen minus the taskbar**, so a window never opens under it. A window wider or taller than the work area starts at the work area's left or top edge, so its title bar stays on the screen, and stays centered in the other direction.
- **Activated means in front of other windows, with the keyboard focus, but not always on top.** Windows may refuse to bring a window to the front while the user works in another app, and flashes its taskbar button instead ([SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)). Not confirmed by a run: that a click on the tray icon, or a global shortcut, lets the app come to the front.
- **A window hidden while minimized opens in the normal state**, centered like any hidden window. This happens when the user closes the minimized Add window from its taskbar button. Windows can't restore a window without showing it, so the platform restores it while it's cloaked, invisible to the user, and hides it again before placing it ([DWMWA_CLOAK](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)). Proposed, a decision to confirm. Not confirmed by eye: that nothing flashes on the screen or the taskbar.
- **A window shown for the first time stays invisible until WPF has drawn it**, so it never shows a white frame before the theme's colors. Windows puts a new window on the screen before WPF has drawn into it, and that first frame is white, even in the dark theme. So the platform cloaks the window before it shows it, as it does to restore a minimized one. It uncloaks the window once the window has loaded and WPF has drawn it: at the step where WPF raises the window's [ContentRendered](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.contentrendered) event, which comes after the first frame ([WPF's source](https://github.com/dotnet/wpf/blob/v11.0.0-rc.3.26522.148/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs#L3506-L3535)). That step comes for every window, with content or not, so no window waits for good and no timer is needed. The call returns before that: the window is placed and active, and WPF counts it as visible, but the user sees it a few frames later. A window hidden and shown again isn't cloaked: it never showed white. The user decided this rule covers every window drawn by WPF that the app shows: the windows Show on the pointer's screen opens, and the dialogs the modal dialog part below opens.
  - **Measured outside the repository, not by a test.** On Windows build 26300 in the dark theme, a tool read one pixel of a WPF window on every frame Windows drew. The first window a fresh app showed was white for 32 to 92 milliseconds in 4 of 4 runs. A window hidden and shown again was white in none of 8 runs. A cloaked window became the active window, in front of other windows, while still cloaked. Then the tool called the platform's own Show on the pointer's screen and Modal dialog. Their first window or dialog was white in none of 11 runs, against 6 of 6 for WPF's plain Show and ShowDialog. It showed the theme's colors about 35 milliseconds later than a plain window: 277 to 312 milliseconds after the call, against 246 to 277. It was active, in front, and uncloaked in every run.
  - **Failures.** When Windows can't cloak the window, the call throws Win32Exception and the window doesn't show. When Windows can't uncloak it, the call has already returned, so the failure is an [error no module handles](#the-platform-runs-the-app-and-the-composition-root-builds-it): it's logged and shown, the app keeps running, and the window stays invisible. Not confirmed by a test: no test can make Windows refuse. A window closed before that step is left alone: it's already gone from the screen. When WPF's own Show throws after the cloak, the window stays cloaked, and the failure reaches the caller as it would without the cloak. Proposed, a decision to confirm.
  - **Not confirmed by a run**: what the window's taskbar button shows while it's cloaked.
  - **Ruled out.** Waiting for ContentRendered itself: WPF raises it only for a window with content, so an empty window would stay invisible, or need a rule against it. Uncloaking anyway after a time limit: the window could still show white, and each open would start a timer. Covering only the windows Show on the pointer's screen opens, or those and the question dialog: the other dialogs would still show white. Showing the window see-through until drawn: WPF makes a window see-through only when it allows transparency, which needs a window with no system frame ([AllowsTransparency](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency)). Showing each window once, cloaked, when it's made, then hiding it: every window would be drawn twice, and Task View, Configure, Logs, and Contacts are made only when they open.
- **Each show logs its choices at Debug**, so a window that never appears can be traced from the log, as [Logging](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) requires. The user decided that the caller passes its own logger, so each entry names the module whose window it is. Proposed, each a decision to confirm:
  - **Show on the pointer's screen writes one entry per call, before it shows the window.** For a hidden window, it names the window's type, `Window`; whether it was cloaked for its first show, `FirstShow`; whether a window hidden while minimized was restored while cloaked, `RestoredWhileCloaked`; and the work area it opened on, in pixels, `WorkArea`. For a visible window, it names `Window` and whether it was restored from minimized, `Restored`.
  - **Modal dialog writes one entry per call, before it shows the dialog**, naming `Window` and `FirstShow`.
  - **A window cloaked for its first show writes one more entry at the step that uncloaks it**, naming `Window` and whether it was uncloaked, `Uncloaked`: false when the window closed first. So every cloak has a matching entry, and a cloaked window with none is one that never appeared.
  - **Single window and the question dialog write no entries of their own.** They pass their caller's logger on, and the entries above explain their choices.
- **A part of the platform's public interface, Modal dialog, opens a WPF dialog** under the same rule. The platform's question dialog and Updates' [What is new](updates.md#what-is-new-shows-each-newer-releases-notes) open through it instead of WPF's own ShowDialog. The file and folder pickers and the message boxes are drawn by Windows, not WPF, so they don't need it. The user decided this part:

  ```csharp
  namespace Memoria.ToDo.Desktop.Platform;

  public static class ModalDialog
  {
      /// Shows the window as a modal dialog and returns when it closes. A window shown for the
      /// first time stays invisible on the screen until WPF has drawn it. Logs its choices at
      /// Debug through logger, the caller's own.
      /// Throws ArgumentNullException when logger is null, InvalidOperationException off the
      /// window's UI thread, and Win32Exception when Windows can't cloak the window.
      public static void Show(System.Windows.Window dialog, Serilog.ILogger logger);
  }
  ```
- **The app is aware of each screen's own scale**, declared in the composition root's app manifest, so windows stay sharp on a screen whose scale differs from the main one. WPF apps use one scale for every screen unless they declare this ([per-monitor DPI](https://github.com/microsoft/WPF-Samples/tree/main/PerMonitorDPI)). Not confirmed by a run: that a window opens centered on a screen whose scale differs from the main one, since WPF may place a window that isn't shown yet at the main screen's scale.
- **Its tests are the platform's integration tests**, each showing a plain test window on the real screen. They ask Windows whether the window is cloaked right after the call returns, and again once WPF has drawn it. Modal dialog's tests do the same with a plain dialog. No test checks the Debug entries a show writes, as [Logging](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) decides. The window modules' UX tests check that Show shows and activates their window, not where it opens or whether it's cloaked.

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
      /// logger is the caller's own, passed on to ShowOnPointerScreen. Create it on the UI thread.
      /// Throws ArgumentNullException when createWindow or logger is null.
      public SingleWindow(System.Func<System.Windows.Window> createWindow, Serilog.ILogger logger);

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

    /// From now on, pressing the current keys while window, or any control in it, has the focus sends
    /// window the user's close command, as its close button does, before any control sees the keys,
    /// and the keys go no further. Throws ArgumentNullException when window is null.
    public void Attach(System.Windows.Window window);
}
```

- **Closing sends the window's own close command**, so each window does what its close button does: Add hides and keeps its draft, as its [closing](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) already does, Task View and Contacts ask first about a changed edit, and Configure closes. The user decided this in step 15's slice (e), fixing Task View, whose changed edit the shortcut dropped without asking: a plain close is how the app ends, which [never asks](#leaving-a-changed-edit-asks-once).
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

The user decided that Task View's split bar and search band move into the platform, so Task View and Contacts share one of each, and Task View works exactly as before. The user also confirmed the rest:

- **The group drag's parts move with them**, since Contacts' [group drag](contact-list.md#dragging-a-groups-heading-reorders-the-groups) is their second use: the distance a press must move before a drag starts, the drop line, scrolling near a list's edges during a drag, and the mouse behind them. The edge band and distance check they use stay inside the platform.
- **Each part keeps the look and numbers it has in Task View**: a 6-pixel bar that takes no focus, and the "Search:" label with its box in the status bar's shade. Each window passes its own column shares and minimum widths.
- **They're platform classes, not interfaces**, as the Close window keys are: each has one implementation, and a window's UX tests use the real ones. The mouse sits behind a small interface, so tests drive a drag with a fake pointer. Its one fake moves to the desktop's [fakes project](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test).
- **The composition root gives each window module the mouse**, the user's choice, since only it chooses the implementation of an interface the platform publishes ([AGENTS.md](../../../AGENTS.md#code-design)). It makes one `MousePointer` and passes it to each window module's `Create`. The Task list's `Create` takes `IDragPointer pointer` as its last argument, after `saveKeys`, and so will the Contact list's. Two options were ruled out: one shared `MousePointer.Instance` that modules use directly, which breaks the wiring rule; and a mouse inside each module, which the Contact list would copy.
- **Setting the search band's text raises TextChanged**, the user's choice, as WPF's text box does, so each window has one path: Contacts empties its search by setting the text, and its handler filters again. Task View's handler writes the text back to its filter, which ignores an unchanged text. Raising it only for the user's changes was ruled out: a window that sets the text would need a second path, and the band a flag to tell the two apart.
- **Press drag reads Windows' drag distances itself**, the user's choice, so task rows, group headings, and Contacts' headings can't differ, and the rule has one home. Its tests use the test machine's distances. Having each caller pass the distances, as Task View did, was ruled out: the rule would sit in every caller.
- **Task View's Undo bar stays in the Task list**, under the band, since only Task View has one. Its message line moves to the platform when the Contact list is built, as [below](#the-message-line-and-store-failures-serve-every-window-that-shows-them) says.
- **Each part checks its arguments before it builds anything**, as the platform's other parts do: a null argument throws `ArgumentNullException`, and split panes reject a share that isn't a positive number, or a minimum width that's negative or not a number, with `ArgumentOutOfRangeException`.
- **Their tests move with them.** Tests of the moved parts move to the platform's tests; the Task list keeps the tests of its own rows, headings, and window, which show it works as before.

The user confirmed these declarations, with the user's three choices above. They moved from Task View's internal classes, and [`src/Desktop/Platform`](../../../src/Desktop/Platform/SplitPanes.cs) implements them. `MousePointer` doesn't check the drag data itself, since WPF rejects missing data first with the same error:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// Two sides beside each other, and a bar between them the user drags to move the split.
/// The bar takes no focus and is no keyboard stop.
public sealed class SplitPanes : System.Windows.Controls.Grid
{
    /// The sides share the width in the ratio leftShare to rightShare, each at least its minimum width.
    /// Throws ArgumentNullException for a null side, and ArgumentOutOfRangeException for a share that isn't
    /// a positive finite number, or a minimum width that's negative or not finite.
    public SplitPanes(System.Windows.UIElement left, double leftShare, double leftMinWidth,
        System.Windows.UIElement right, double rightShare, double rightMinWidth);
}

/// The "Search:" label and its box, named "Search" for screen readers, edge to edge in the status bar's shade.
public sealed class SearchBand : System.Windows.Controls.Border
{
    public SearchBand();

    /// The box's text. Throws ArgumentNullException when set to null.
    public string Text { get; set; }

    /// Raised whenever the box's text changes, by the user or through Text.
    public event System.EventHandler? TextChanged;

    /// Raised on Down (false) or Up (true) in the box with no modifier key held, to select the top or bottom row.
    /// The key goes no further.
    public event System.EventHandler<bool>? EdgeRowRequested;

    /// Moves focus to the box and selects its text.
    public void FocusText();
}

/// Tells a press from a drag: a drag starts once the pointer moves Windows' drag distance from the press,
/// across or down, with the left button held. A release before that is a click.
public sealed class PressDrag
{
    /// Reads Windows' drag distances, SystemParameters.MinimumHorizontalDragDistance and MinimumVerticalDragDistance.
    public PressDrag();

    /// Starts a gesture at the pointer's place.
    public void Press(System.Windows.Point at);

    /// True once per press, when a move with the left button held reaches the drag distance.
    public bool Move(System.Windows.Point now, bool leftButtonDown);

    /// Forgets the press, as the pointer left the element and its release goes unheard.
    public void Leave();

    /// Ends the gesture: true when no drag started, so it counts as a click.
    public bool Release();
}

/// The line that shows where a dragged item lands: thin, in the theme's accent color, with a small circle at its start,
/// drawn above the adorned element so it takes no room. The line's automation ID is "DropLine".
public sealed class DropLine : System.Windows.Documents.Adorner
{
    /// Throws ArgumentNullException for null.
    public DropLine(System.Windows.UIElement adorned);

    /// Shows the line with its middle at y, from x, width wide, in the adorned element's coordinates.
    public void Show(double x, double y, double width);

    /// Takes the line away.
    public void Hide();
}

/// Scrolls a viewer while a drag holds the pointer near its top or bottom edge. It stops when the pointer leaves
/// the edge, the viewer reaches its end, the drop lands, or the drag leaves or is canceled.
public static class AutoScroll
{
    /// Works for as long as the viewer lives. time paces the steps; scrolled gets the pointer's place in the viewer
    /// after each step. Throws ArgumentNullException for a null argument.
    public static void Attach(System.Windows.Controls.ScrollViewer viewer, System.TimeProvider time,
        System.Action<System.Windows.Point> scrolled);
}

/// The pointer's position, as a part that follows it sees it.
/// The composition root passes MousePointer; tests pass a fake.
public interface IPointerPosition
{
    /// The pointer's place in element's own coordinates.
    System.Windows.Point PositionIn(System.Windows.UIElement element);
}

/// The mouse, as a drag sees it.
public interface IDragPointer : IPointerPosition
{
    /// Whether the left button is down now.
    bool IsLeftButtonDown { get; }

    /// Runs a move drag of data from source, and returns when the drag ends.
    void StartDrag(System.Windows.DependencyObject source, System.Windows.IDataObject data);
}

/// The PC's mouse. Real-input integration tests check its connection to WPF.
public sealed class MousePointer : IDragPointer
{
    public MousePointer();
    public bool IsLeftButtonDown { get; }
    public System.Windows.Point PositionIn(System.Windows.UIElement element);
    public void StartDrag(System.Windows.DependencyObject source, System.Windows.IDataObject data);
}
```

## A hover card shows while the pointer rests on a part

The user asked that resting the mouse pointer on a mention show the contact's card, in the Task editor's chips and in the Formatted view's titles and descriptions. Those are two modules, so the user decided that when a card opens, where, and when it closes has one home: a platform part, Hover card, which the Task editor and the Formatted view use. It knows nothing of contacts or mentions: its user says which part of an element is under the pointer, and which card goes with it. The card itself is the [Contact list's](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details). Proposed, each a decision to confirm:

- **A card opens once the pointer rests on a part for Windows' hover time**, [SystemParameters.MouseHoverTime](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.mousehovertime), 400 ms by default, as a tooltip does.
- **It opens under the part, lined up with its left edge**, in a popup, and, where it doesn't fit on the screen's work area, above the part or moved left, as the ["@" list](task-editor.md#typing-an-at-sign-mentions-a-contact) does.
- **It closes when the pointer is off both the part and the card**, as [below](#a-tall-card-scrolls-and-the-pointer-can-reach-it); **when a mouse button is pressed or the wheel turns anywhere but on the card; when a key other than a modifier is pressed**, which still does its own job; and **when the window is deactivated, moved, or hidden, or the element leaves the screen**. Ctrl, Shift, Alt, and Win don't close it, so Ctrl can change the card.
- **Cards show only while their window is active**, since a card's Ctrl needs the window to hear the keys.
- **At most one card shows in the app.** Resting on another part closes the first card and opens the next after the hover time.
- **An answer that comes after the pointer moved on shows nothing**, since a description's web view answers later than a title does.
- **It's a platform class, not an interface**, as the other platform parts are: one implementation, and UX tests use the real one.

The user decided that hover reads the pointer through `IPointerPosition`, declared [with the drag parts](#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them). The composition root passes its one `MousePointer` through each module that creates a hover card. UX tests pass the shared fake instead, under the [input test boundary](../technology-stack.md#unit-and-ux-tests-cannot-send-real-input).

Hover reads positions in the owner's coordinates. Entering and leaving the card use the card's own routed events, so crossing to its separate popup does not depend on a global mouse-position query. The rest time, placement, and activation rules stay unchanged. A null pointer is rejected before any event handler is attached.

The user decided that hover takes its caller's logger, last, under the [logging rules](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens). Debug entries record leaving the owner while a card shows (kept on the card, grace started, or existing grace kept), returning to the part or card before grace ends, and closing because grace expired. They include the owner's control type, the grace duration where relevant, and whether a rest wait was cancelled or remains pending. Moves, unchanged timer ticks, and owner leaves with no card shown are not logged. A null logger is rejected before any event handler is attached.

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// A card that shows by a part of an element while the pointer rests on that part, as a tooltip does.
public sealed class HoverCard
{
    /// Watches owner's pointer while owner is on screen. Once the pointer rests on one place for Windows' hover time,
    /// asks partAt for the part there, in owner's coordinates, and shows its card, if any, under the part.
    /// Throws ArgumentNullException for a null argument. Use it on the UI thread.
    public HoverCard(System.Windows.FrameworkElement owner,
        System.Func<System.Windows.Point, System.Threading.Tasks.Task<HoverPart?>> partAt,
        IPointerPosition pointer, Serilog.ILogger logger);
}

/// A part of an element, its box in the element's coordinates, and the card to show for it.
public sealed record HoverPart(System.Windows.Rect Box, System.Windows.FrameworkElement Card);
```

WPF's own tooltip, with the card as its content, was ruled out: chips and descriptions would still need their own hit tests and opening, a tooltip closes on its own after a few seconds, and it may close when Ctrl is pressed.

A test-only constructor was ruled out: it would leave module-level hover tests dependent on the real cursor. Feeding WPF's internal mouse reports through reflection was ruled out because it depends on private framework details instead of the pointer interface the app already needs.

### A tall card scrolls, and the pointer can reach it

With Ctrl held, a card can be taller than the screen, such as for a manager with many direct reports. The user decided:

- **A card is never taller than the work area of the screen it shows on**, the screen minus the taskbar, as for [windows](#windows-open-on-the-screen-with-the-mouse-pointer). Content taller than that scrolls inside the card.
- **The mouse wheel over the card scrolls it**, and doesn't close it. The wheel anywhere else still closes it.

So the pointer must be able to reach the card. Proposed, each a decision to confirm:

- **The pointer may cross from the part to the card.** Leaving the part starts a short grace, Windows' hover time again, so a user who slows the hover also gets longer to cross. The card closes when the grace ends with the pointer on neither the part nor the card.
  - **The user decided that leaving the whole owner follows this same grace**, even when no move outside the part arrived first. Entering the card or returning to the part cancels it; leaving the owner does not restart a grace already running. Previously, leaving the owner could close the card immediately. Keeping that immediate close was ruled out because the owner's leave and the popup's enter can arrive in either order.
- **The card stays open while the pointer is over it**, and leaving it starts the same grace. Coming back to the part or the card within the grace keeps it open.
- **A press on the card keeps it open**, so its scroll bar can be dragged. Nothing on the card acts on a click, and the card never takes the keyboard focus, so keys and Ctrl still reach the window.
- **The pointer on the card doesn't count as resting on the owner**, so it opens no other card.
- **A card as tall as the work area may cover its part**: the pointer is then on the card, so the card stays open.

The hover card owns all of this, the cap and the scrolling included, since it alone knows which screen the card shows on and where the pointer is. A card's maker, such as the [Contact list](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details), sets no height of its own. The public declarations don't change.

Two other ways to reach the card were ruled out:

- **The wheel scrolls the card from wherever the pointer is**, with the card still closing when the pointer leaves the part: the pointer must stay on a small mention, and the list under the pointer couldn't scroll while a card shows.
- **No grace: the card opens touching its part, so the pointer crosses no gap**: a pointer moved slightly sideways, or a card moved left at the screen's edge, would still close it on the way.

## The search band publishes its box for the alias list

The user decided that the [search band](#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them) publishes its box, so the Task list can attach the Task editor's ["@" list for aliases](task-editor.md#a-plain-text-box-can-offer-contacts-by-alias) to it, and that its Down and Up select the top or bottom row only when no handler before the band's, such as an open list, used the key. The band stays the one search box for every window.

- **With no list open, Down and Up select the top or bottom row, exactly as before.** The band still hears them as they tunnel down to the box, before the box's own handling, which would otherwise use them up.
- **An open list gets the first refusal**: the alias list hears Down and Up before the band, and the band acts only on a key the list left unhandled.

Proposed, a decision to confirm: the members' shape below.

```csharp
// On SearchBand:

    /// The search box itself, for a part that works on any text box. Its text is Text.
    public System.Windows.Controls.TextBox Box { get; }

    /// Raised on Down (false) or Up (true) in the box with no modifier key held, when no handler before the band's
    /// used the key, to select the top or bottom row. The key goes no further.
    public event System.EventHandler<bool>? EdgeRowRequested;
```

Two other ways were ruled out:

- **The band takes an optional helper through a small interface the platform defines**: an extension point for one caller.
- **Task View drops the band for a search box of its own**: Task View and Contacts would stop sharing the band the user moved into the platform.

## The message line and store failures serve every window that shows them

The Task list decided the look of Task View's message line, and the [Contact list](contact-list.md#messages-and-problems-show-at-the-top) shows one at its top too. Proposed, each a decision to confirm, made when the Contact list is built:

- **The message line moves into the platform**, since Contacts' line is its second use. It keeps Task View's look: text that wraps, in the theme's critical color, `SystemFillColorCriticalBrush`, and hidden while it has no text. Each window sets its own margins. Task View moves onto it with no change users notice, and its tests show that.
- **So do the messages for failures every store call can throw**, which Task View's list maps today. No clone gets "There is no repository folder yet. Open Configure and choose one."; a file error, a denied file, unreadable data, and a rejected argument get the failure's own message. Each window adds the failures only its store throws, such as Tasks' task that no longer exists.
- **The Task editor's line under its fields keeps its own look for now**, a fixed red that doesn't follow the theme. Moving it onto the platform's line is a later change users would notice.
- **They're platform classes, not interfaces**, as the other parts are, and their tests are unit tests, with one UX test of the line's look.

These declarations, built in step 15's slice (e) in [`MessageLine.cs`](../../../src/Desktop/Platform/MessageLine.cs) and [`StoreFailures.cs`](../../../src/Desktop/Platform/StoreFailures.cs):

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// A window's line for why the last action failed: wrapping text in the theme's critical color,
/// hidden while it has no text.
public sealed class MessageLine : System.Windows.Controls.TextBlock
{
    public MessageLine();

    /// Shows the text, or hides the line when it's null or empty.
    public void Show(string? text);
}

/// The messages for failures any store call can throw, and a way to run a call that reports them.
public static class StoreFailures
{
    /// "There is no repository folder yet. Open Configure and choose one." for NoRepositoryFolderException;
    /// the failure's own message for IOException, UnauthorizedAccessException, InvalidDataException,
    /// and ArgumentException; null for any other failure.
    public static string? MessageOf(System.Exception failure);

    /// Runs the call and returns true. When it throws a failure messageOf gives a message for, passes
    /// the message to report and returns false; any other failure passes on. Throws ArgumentNullException
    /// for a null argument.
    public static bool Run(System.Action call, System.Func<System.Exception, string?> messageOf,
        System.Action<string> report);
}
```

A window passes `StoreFailures.MessageOf` to `Run` when it has no failures of its own, or a function that tries its own first, then `StoreFailures.MessageOf`. Proposed, a decision to confirm: the desktop platform's project references the shared library's platform, `Memoria.ToDo.Shared.Platform`, for `NoRepositoryFolderException`; a platform may use another platform, and still names no module.

### Leaving a changed edit asks once

Task View asks Save, Discard, or Cancel before it drops a changed edit, and never asks again while the question shows. The Contact list asks the same before it leaves a changed form, so the user decided the rule moves from the Task list into the platform, renamed for leaving rather than closing. Task View keeps its behavior, and its tests show that. Each window supplies its own prompt, usually through the [question dialog](../../../src/Desktop/Platform/QuestionDialog.cs). These declarations, built in step 15's slice (e):

```csharp
namespace Memoria.ToDo.Desktop.Platform;

public enum LeaveAnswer { Save, Discard, Cancel }

/// Asks the user what to do with a changed edit. Closing the prompt any other way answers Cancel.
public interface ILeavePrompt
{
    LeaveAnswer Ask();
}

/// The changed edit a window is leaving.
public interface ILeavingEdit
{
    bool HasChanges { get; }

    /// Saves the edit. False when it can't be saved; the window then shows why.
    bool Save();

    /// Drops the edit.
    void Discard();

    /// Puts the cursor back in the edit.
    void ReturnToEditor();
}

/// Whether a window may leave an edit: no edit, or one with no changes, leaves without asking; a changed
/// one asks and acts on the answer.
public sealed class LeaveDecision
{
    public LeaveDecision(ILeavePrompt prompt);

    /// True when the edit may be left. False on Cancel, a failed save, or while the prompt already shows.
    public bool MayLeave(ILeavingEdit? edit);
}
```

Only the user's own close command asks: the close button, Alt+F4, and the window menu's Close. The app ending, or Windows ending the session, closes without asking. Task View tells them apart today, and the Contacts window needs the same, so the user decided the rule moves from the Task list into the platform unchanged:

```csharp
namespace Memoria.ToDo.Desktop.Platform;

/// Tells the user's close command from the application ending.
public static class CloseCommandHook
{
    /// Calls userClose instead of closing when the user closes the window; a plain close from the app
    /// closes the window's owned windows first, answering Cancel, then the window. Call once the window has a handle.
    public static void Attach(System.Windows.Window window, System.Action userClose);
}
```

## Later designs answer what these files leave open

The platform's shared [question dialog](../../../src/Desktop/Platform/QuestionDialog.cs) asks modal questions for Task View, Contacts, and Configure. It follows the theme, centers on its owner, and shows ordered answer buttons. It opens through the [Modal dialog](#windows-open-on-the-screen-with-the-mouse-pointer), so it never shows a white frame first, and passes on the logger its caller gives it last. Enter picks the default answer; Esc and closing pick Cancel. Its text wraps at 420 device-independent pixels, proposed for confirmation.

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

The desktop app is built in steps, one row each, and each row links to the design sections it builds. Each row's status says whether it's built. Step 5 leaves two checks that no test can make yet to Sync's [validation backlog](../sync.md#implementation-and-focused-checks). You choose the remaining delivery order, recorded below the table. Status is **Next**, **Not scheduled**, or **Implemented**, which means every behavior the row lists is built and tested.

The table lists each step, what it builds, and its status:

| # | Step | Builds | Status |
| --- | --- | --- | --- |
| 1 | Desktop shell: the tray app and its windows, with no task saved | The [startup](#the-platform-runs-the-app-and-the-composition-root-builds-it); Shell's [tray icon and menu](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions), without the shortcuts or the redirect to Configure; the [hidden Add window](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title), without its fields; and the create and Show parts of the [Task editor](task-editor.md#public-interface), the [Task list](task-list.md#public-interface), and [Configure](configure.md#public-interface) | Implemented |
| 2 | Two shortcuts, start with Windows, one instance, and theme | Shell's [two shortcuts](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions), with Configure opening when one can't register, and its [one instance and what each start shows](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view); the platform's [theme that every window follows](#the-platform-runs-the-app-and-the-composition-root-builds-it); Configure's [shortcut and Start with Windows settings](configure.md#each-setting-has-one-owner), with the shortcuts not saved yet, in [labeled sections](configure.md#the-window-shows-its-settings-in-labeled-sections), with its [shortcut check](configure.md#a-new-shortcut-is-checked-at-once), the shortcuts [released while a field has focus](configure.md#while-a-shortcut-field-has-focus-the-apps-shortcuts-are-released), a [message for every shortcut it can't use](configure.md#configure-explains-every-shortcut-it-cant-use), and the [`Run` key](configure.md#start-with-windows-is-the-run-key-itself) that installing writes and uninstalling removes; and the [Add window's size](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) | Implemented |
| 3 | Adding tasks | The platform's [theme part](#the-platform-runs-the-app-and-the-composition-root-builds-it), which tells modules whether windows are light or dark, built first; then the rest of the [Task editor](task-editor.md#behavior), on [Tasks](../tasks.md), the shared library's [platform](../modules.md#the-file-system-interface-reads-and-writes-whole-files), [Editing](../editing.md), and [Rendering's](../rendering.md#public-interface) styled spans and palette | Implemented |
| 4 | Viewing tasks | The [Formatted view](formatted-view.md#behavior), and the [Task list](task-list.md#behavior) except its Sync button and the sync when Task View opens | Implemented |
| 5 | Sync | [Sync through git](../sync.md); Shell's [sync status](shell.md#sync-stays-quiet-unless-something-is-wrong) and its redirect to Configure until the device is configured; Configure's [sync](configure.md#configure-shows-how-sync-is-doing), deletion delay, and saving and loading the [shortcuts](configure.md#each-setting-has-one-owner) in `settings.json`; the Task list's [Sync button and the sync when Task View opens](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button). | Implemented |
| 6 | Install and updates | Shell's [installer](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), except removing the `Run` key, which comes with step 2; and the [release workflow](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases), with each release's notes from the [changelog](../version-tags.md#a-version-is-tagged-only-when-the-changelog-has-its-notes). Proposed: the update checks now come with step 10, since [Updates](updates.md) replaces the background download, and the release notes come with this step, so the first release already has them. This is a decision to confirm. | Implemented |
| 7 | Esc and switching windows | The platform's [Esc part](#window-modules-read-keys-through-the-platform); Esc in [Task View](task-list.md#esc-goes-back-one-step-then-closes-task-view) and [Configure](configure.md#esc-and-the-close-window-shortcut-close-the-window); Shell's [shortcut switching](shell.md#a-shortcut-switches-between-add-and-task-view), with the close parts of [Add](task-editor.md#public-interface) and [Task View](task-list.md#public-interface); and Task View's [prompt for a changed edit](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) | Implemented |
| 8 | Colored tags | Each tag's color in [Tasks](../tasks.md#each-tags-color-is-kept-in-one-synced-file), and Sync carrying `tags.json`; Rendering's [30 tag colors](../rendering.md#thirty-tag-colors-stay-distinct-in-both-themes); the Task editor's [badges and + menu](task-editor.md#a-task-has-one-tag-shown-as-one-pill); and the Task list's [colored tags and tag changes without Edit](task-list.md#tags-show-as-colored-badges-and-change-without-edit) | Implemented |
| 9 | Text look and Task View by tag | Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme) on the description page, in [titles](formatted-view.md#it-shows-what-rendering-decides-never-markdown), in the [editor](task-editor.md#the-editor-styles-markdown-in-place-with-no-preview), and on [Configure's section titles](configure.md#the-window-shows-its-settings-in-labeled-sections); the platform's [hand pointer on every button and theme follower](#the-platform-runs-the-app-and-the-composition-root-builds-it); and Task View's [tag groups beside the description](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description), with [Edit Description](task-list.md#edit-description-turns-the-right-side-into-the-editor) | Implemented |
| 10 | Updates you agree to | [Updates](updates.md): the checks at start and at 3 AM, the [status bar](updates.md#add-and-task-view-show-an-update-in-a-shared-status-bar) in [Add](task-editor.md#add-opens-at-once-with-the-cursor-in-the-title) and [Task View](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button), Configure's [Update card](configure.md#the-update-card-leads-the-application-section), What is new, Auto update, and the restart | Implemented |
| 11 | Task View refinements | Rendering's [heading colors](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme), also on [Configure's card titles](configure.md#the-window-shows-its-settings-in-labeled-sections); the Task editor's [lowercase tags with no spaces](task-editor.md#a-task-has-one-tag-shown-as-one-pill); and Task View's [status bar](task-list.md#a-status-bar-holds-the-sync-button-and-the-version), [search box](task-list.md#search-narrows-the-list) with no tag filter, [collapsing groups](task-list.md#a-click-on-a-groups-tag-collapses-or-expands-it), [description-only edit](task-list.md#edit-description-turns-the-right-side-into-the-editor), [Edit Title](task-list.md#edit-title-edits-the-title-in-its-row), [resizable images](task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview) in Edit Description's [Preview](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab), and [no focus rectangle on the split bar or the list](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete); with the platform's [app version](#the-apps-version-has-one-home) and [Save keys](#the-save-shortcut-saves-in-any-window-that-edits), Configure's [Save shortcut](configure.md#the-save-shortcut-saves-an-edit-in-any-window), and Tasks' [Save shortcut setting](../tasks.md#one-versioned-file-holds-the-three-shared-settings). | Implemented |
| 12 | Configure's Application section and its shortcut | Configure's [Application section](configure.md#the-window-shows-its-settings-in-labeled-sections), with the [Update card](configure.md#the-update-card-leads-the-application-section) and the Shortcuts card; the [Configure shortcut](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) in Configure and Shell; and Tasks' [Configure shortcut setting](../tasks.md#one-versioned-file-holds-the-three-shared-settings). | Implemented |
| 13 | Group order and the drop line | Task View's [group drag, drop line, and scrolling during a drag](task-list.md#drag-and-drop-moves-tasks-and-groups-and-a-line-shows-where-they-land), with its groups in [the tags' order](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description); Tasks' [tag order](../tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) in `tags.json` version 2, with its [capability](../tasks.md#tag-order-is-a-capability-of-its-own) and merge. Sync carries `tags.json` as before, with no change. The Android app's [tag chips](../android/task-list.md#tags-and-search-narrow-the-list) follow the order when the Android app is built. | Implemented |
| 14 | One tag per task | Tasks' [one-tag rule](../tasks.md#a-task-has-at-most-one-tag) in `Add`, saving an edit, the merge, and kept drafts; the Task editor's [tag pill](task-editor.md#a-task-has-one-tag-shown-as-one-pill) and its [menu](task-editor.md#a-click-on-the-pill-opens-the-tag-menu), with the `TagEditor` interface the user decided; and the Task list's [tag changes without Edit](task-list.md#tags-show-as-colored-badges-and-change-without-edit), a [tag that joins an open edit's one save](task-list.md#a-tag-chosen-during-an-edit-saves-with-the-edit), and one group per task. | Implemented |
| 15 | Contacts | Built in slices, in this order, each reviewed on its own: (a) the platform's [synced repository](../modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), with Tasks, Sync, and App moved onto it and no change users notice; (b) the shared [Contacts](../contacts.md) module, with the platform's [contact address](../modules.md#a-contact-address-has-one-home), and Tasks' JSON style, version check, skip rule, and path check moved into the platform's [shared file rules](../modules.md#stores-share-one-json-style-and-one-set-of-file-rules) with no change users notice; (c) Sync [carrying contact files](../sync.md#sync-manages-contact-files-through-contacts-rules), with App's composition root creating the Contacts store; (d) the platform's [split panes, search band, and drag parts](#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), with Task View moved onto them unchanged; (e) the [Contact list](contact-list.md), with Shell's and Configure's [Contacts shortcut](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) and tray item; (f) Rendering's [mentions](../rendering.md#a-mention-shows-on-a-colored-background) and Editing's [mention list](../editing.md#typing-an-at-sign-opens-the-mention-list); (g) the [Task editor's "@" list and chips](task-editor.md#typing-an-at-sign-mentions-a-contact), then Task View's [live mention text](task-list.md#mentions-show-each-contacts-current-details). The Android app's mentions come when the Android app is built. | Implemented |
| 16 | Tasks' notices after a change | Tasks raises its notices one by one after a change: `Changed`, then settings, tag colors, and tag order, then the [local save report](../tasks.md#sync-shares-the-repositorys-lock-never-a-network-wait). When a handler throws, the notices after it and the report are skipped, so Sync doesn't learn of the save. This step makes Tasks attempt every notice and the report, even when one fails, and throw the failures by the platform's [rule for steps that must all run](../modules.md#steps-that-must-all-run-throw-their-failures-together), as [Contacts](../contacts.md#failure-modes) does. Its design is in Tasks' [failure modes](../tasks.md#failure-modes). | Implemented |
|
| 18 | Contact cards and the "@" list at the cursor | The Task editor's ["@" list in a popup at the "@"](task-editor.md#typing-an-at-sign-mentions-a-contact), with its template header and "**Full Name** (_alias_)" rows, and its [alias list for a plain text box](task-editor.md#a-plain-text-box-can-offer-contacts-by-alias); Task View's [search by "@" alias](task-list.md#search-narrows-the-list); the Contact list's [contact card](contact-list.md#a-contact-card-shows-a-mentioned-contacts-details), with Contacts' `ContactOf`; the platform's [hover card](#a-hover-card-shows-while-the-pointer-rests-on-a-part) and search band box; Rendering's [mention numbers](../rendering.md#a-mention-shows-on-a-colored-background); and the cards on [chips](task-editor.md#resting-the-pointer-on-a-chip-shows-the-contacts-card), [titles and descriptions](task-list.md#resting-the-pointer-on-a-mention-shows-the-contacts-card), through the [Formatted view](formatted-view.md#a-mention-shows-a-card-its-user-makes). | Not scheduled |
| 19 | Local API | The [Local API](../local-api.md) module, the [contract](../local-api-contract.md) and both clients, Tasks' [direct task changes](../tasks.md#direct-task-changes-are-a-capability-of-their-own), Shell [starting and stopping it](shell.md#public-interface), the Api part's table in the [module map](../modules.md#module-map) with the architecture test's fourth part, and the release files. Its cost is measured first, against the [limits](../local-api.md#shell-starts-the-api-once-this-is-the-only-instance-off-the-ui-thread) it sets. | Not scheduled |

The remaining work follows this order:

1. Review and refine [Task View](task-list.md), including its decisions awaiting confirmation. This review is deferred, not waived.
2. Add the [Add window's formatted preview](task-editor.md).
3. Build the [Android app](../android/README.md).
