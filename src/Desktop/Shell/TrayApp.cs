using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Builds the tray app.</summary>
public static class TrayApp
{
    /// <summary>
    /// Creates the app's lifetime. Start adds the tray icon with its menu (each item calls its window's Show,
    /// Exit ends the app, a left click shows Add) and begins the update checks. Stop removes the icon and ends the checks.
    /// </summary>
    public static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IConfigureWindow configure,
        IAppUpdates updates,
        TimeProvider time) =>
        new ShellLifetime(trayIcon, add, viewTasks, configure, new UpdateSchedule(updates, time));
}
