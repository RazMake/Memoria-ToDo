using System.Runtime.ExceptionServices;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// The tray app's lifetime: the one-instance claim, the tray icon with its menu, the shortcuts, the update checks, and
/// the shared sync service. Only the instance that wins the claim starts and stops sync.
/// </summary>
internal sealed class ShellLifetime(
    ITrayIcon trayIcon,
    Action showAdd,
    Action showViewTasks,
    IConfigureWindow configure,
    IOneInstance oneInstance,
    ActiveShortcut addShortcut,
    ActiveShortcut manageShortcut,
    StartupNavigation startup,
    UpdateSchedule updateSchedule,
    ISyncService sync) : IAppLifetime
{
    private readonly SyncTray syncTray = new SyncTray(sync, trayIcon, startup.ByUser(configure.Show));
    private readonly object stopGate = new();
    private Task? stopping;
    private bool claimed;

    /// <inheritdoc />
    public void Start(Action endApp)
    {
        var addByUser = startup.ByUser(showAdd);
        var viewTasksByUser = startup.ByUser(showViewTasks);
        if (!oneInstance.Claim(viewTasksByUser))
        {
            endApp();
            return;
        }

        claimed = true;
        trayIcon.Show(
            AppName.Value,
            [
                new("Add new task", addByUser),
                new("Manage tasks", viewTasksByUser),
                new("Configure", startup.ByUser(configure.Show)),
                new("Exit", endApp),
            ],
            addByUser);
        syncTray.Attach();
        bool shortcutsReady = configure.ConnectShortcuts(addShortcut, manageShortcut);

        updateSchedule.Start();
        startup.Begin(!shortcutsReady);
        sync.Start();
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (stopGate)
        {
            stopping ??= StopOwnedAsync();
        }

        return stopping.WaitAsync(cancellationToken);
    }

    // Runs its synchronous part on the caller's UI thread, then joins the asynchronous cleanup.
    private async Task StopOwnedAsync()
    {
        List<Exception> failures = [];
        Attempt(startup.Cancel, failures);
        Attempt(syncTray.Detach, failures);
        Attempt(configure.Dispose, failures);
        Attempt(addShortcut.Unregister, failures);
        Attempt(manageShortcut.Unregister, failures);
        Attempt(trayIcon.Remove, failures);

        await Attempt(updateSchedule.StopAsync, failures);
        if (claimed)
        {
            await Attempt(() => sync.StopAsync(CancellationToken.None), failures);
            await Attempt(() => sync.DisposeAsync().AsTask(), failures);
        }

        Attempt(oneInstance.Release, failures);

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static void Attempt(Action step, List<Exception> failures)
    {
        try
        {
            step();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    private static async Task Attempt(Func<Task> step, List<Exception> failures)
    {
        try
        {
            await step();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }
}
