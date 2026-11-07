using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The tray app's lifetime: the tray icon with its menu, and the update checks.</summary>
internal sealed class ShellLifetime(
    ITrayIcon trayIcon,
    IAddWindow add,
    IViewTasksWindow viewTasks,
    IConfigureWindow configure,
    UpdateSchedule updateSchedule) : IAppLifetime
{
    /// <inheritdoc />
    public void Start(Action endApp)
    {
        trayIcon.Show(
            AppName.Value,
            [
                new("Add new Task", add.Show),
                new("View Tasks", viewTasks.Show),
                new("Configure", configure.Show),
                new("Exit", endApp),
            ],
            add.Show);
        updateSchedule.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        updateSchedule.Stop();
        trayIcon.Remove();
    }
}
