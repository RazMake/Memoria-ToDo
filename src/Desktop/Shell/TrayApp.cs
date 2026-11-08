using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Builds the tray app.</summary>
public static class TrayApp
{
    /// <summary>
    /// Creates the app's lifetime. Start first claims the sign-in session; when another instance runs, it ends the app
    /// and does nothing else. Otherwise it adds the tray icon with its menu (Add new task, Manage tasks, Configure,
    /// Exit; a left click shows Add), connects Configure's two shortcuts (a press shows Add or View Tasks, and does
    /// nothing while any of the app's windows is open), begins the
    /// update checks, shows View Tasks when <paramref name="startedAtSignIn"/> is false, and then shows Configure when
    /// a default shortcut couldn't be registered; each later start shows View Tasks only.
    /// Stop ends the checks, unregisters both shortcuts, removes the icon, and releases the claim, then passes on any
    /// release failure.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IConfigureWindow configure,
        IAppUpdates updates,
        IGlobalShortcut shortcut,
        IOneInstance oneInstance,
        bool startedAtSignIn,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(viewTasks);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(oneInstance);
        ArgumentNullException.ThrowIfNull(time);

        bool AnyWindowOpen() => add.IsOpen || viewTasks.IsOpen || configure.IsOpen;

        return new ShellLifetime(
            trayIcon,
            add,
            viewTasks,
            configure,
            oneInstance,
            new ActiveShortcut(shortcut, () => { if (!AnyWindowOpen()) add.Show(); }),
            new ActiveShortcut(shortcut, () => { if (!AnyWindowOpen()) viewTasks.Show(); }),
            startedAtSignIn,
            new UpdateSchedule(updates, time));
    }
}
