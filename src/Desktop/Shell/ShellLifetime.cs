using System.Runtime.ExceptionServices;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The tray app's lifetime: the one-instance claim, the tray icon with its menu, the shortcut, and the update checks.</summary>
internal sealed class ShellLifetime(
    ITrayIcon trayIcon,
    IAddWindow add,
    IViewTasksWindow viewTasks,
    IConfigureWindow configure,
    IOneInstance oneInstance,
    ActiveShortcut addShortcut,
    ActiveShortcut manageShortcut,
    bool startedAtSignIn,
    UpdateSchedule updateSchedule) : IAppLifetime
{
    /// <inheritdoc />
    public void Start(Action endApp)
    {
        if (!oneInstance.Claim(viewTasks.Show))
        {
            endApp();
            return;
        }

        trayIcon.Show(
            AppName.Value,
            [
                new("Add new task", add.Show),
                new("Manage tasks", viewTasks.Show),
                new("Configure", configure.Show),
                new("Exit", endApp),
            ],
            add.Show);
        bool shortcutsReady = configure.ConnectShortcuts(addShortcut, manageShortcut);
        updateSchedule.Start();

        if (!startedAtSignIn)
        {
            viewTasks.Show();
        }

        if (!shortcutsReady)
        {
            configure.Show();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        updateSchedule.Stop();
        List<Exception> failures = [];
        Release(addShortcut, failures);
        Release(manageShortcut, failures);
        trayIcon.Remove();
        oneInstance.Release();

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static void Release(ActiveShortcut shortcut, List<Exception> failures)
    {
        try
        {
            shortcut.Unregister();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }
}
