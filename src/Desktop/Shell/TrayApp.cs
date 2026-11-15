using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Builds the tray app.</summary>
public static class TrayApp
{
    /// <summary>
    /// Creates the app's lifetime. Start first claims the sign-in session; when another instance runs, it ends the app
    /// and does nothing else. Otherwise it adds the tray icon with its menu (Add new task, Task View, Configure,
    /// Exit; a left click shows Task View), connects Configure's two shortcuts (a press shows Add or Task View, and does
    /// nothing while any of the app's windows is open), begins Updates with the app's exit, and starts the shared sync
    /// service. Startup and later actions open Configure until setup is complete;
    /// otherwise startup shows Task View when <paramref name="startedAtSignIn"/> is false.
    /// Configure also opens when an initial shortcut could not be registered, after Task View on a configured
    /// start by hand. These startup windows wait for local initialization.
    /// StopAsync joins sync, then Updates' stop, without blocking the UI, unregisters both shortcuts,
    /// removes the icon, and releases the claim. Cleanup still completes when one step fails.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IConfigureWindow configure,
        IUpdateLifetime updates,
        IGlobalShortcut shortcut,
        IOneInstance oneInstance,
        bool startedAtSignIn,
        ISyncService sync) =>
        Create(trayIcon, add, viewTasks, configure, updates, shortcut, oneInstance, startedAtSignIn, sync, () => new WpfUiThread());

    internal static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IConfigureWindow configure,
        IUpdateLifetime updates,
        IGlobalShortcut shortcut,
        IOneInstance oneInstance,
        bool startedAtSignIn,
        ISyncService sync,
        Func<IUiThread> uiThread)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(viewTasks);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(oneInstance);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(uiThread);

        bool ConfigureOpen() => configure.IsOpen;
        bool AddOpen() => add.IsOpen;
        bool ViewTasksOpen() => viewTasks.IsOpen;
        bool CloseAdd()
        {
            add.Close();
            return true;
        }

        var redirect = new SetupRedirect(sync, configure);
        var showAdd = redirect.Through(add.Show);
        var showViewTasks = redirect.Through(viewTasks.Show);
        var startup = new StartupNavigation(sync.Setup, configure.Show, showViewTasks, startedAtSignIn, uiThread);
        var addByUser = startup.ByUser(showAdd);
        var viewTasksByUser = startup.ByUser(showViewTasks);

        return new ShellLifetime(
            trayIcon,
            showAdd,
            showViewTasks,
            configure,
            oneInstance,
            new ActiveShortcut(shortcut, new ShortcutSwitch(ConfigureOpen, AddOpen, ViewTasksOpen, viewTasks.Close, addByUser).Press),
            new ActiveShortcut(shortcut, new ShortcutSwitch(ConfigureOpen, ViewTasksOpen, AddOpen, CloseAdd, viewTasksByUser).Press),
            startup,
            updates,
            endApp => new ShellExit(add, viewTasks, configure, endApp),
            sync);
    }
}
