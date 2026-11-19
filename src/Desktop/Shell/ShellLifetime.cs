using System.Runtime.ExceptionServices;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// The tray app's lifetime: the one-instance claim, the tray icon with its menu, the shortcuts, Updates, and
/// the shared sync service. Only the instance that wins the claim starts and stops sync.
/// </summary>
internal sealed class ShellLifetime(
    ITrayIcon trayIcon,
    Action showAdd,
    Action showViewTasks,
    Action showContacts,
    IContactsWindow contacts,
    IConfigureWindow configure,
    IOneInstance oneInstance,
    ActiveShortcut addShortcut,
    ActiveShortcut manageShortcut,
    ActiveShortcut configureShortcut,
    ContactsShortcutAction contactsShortcut,
    StartupNavigation startup,
    IUpdateLifetime updates,
    Func<Action, IAppExit> createExit,
    ISyncService sync) : IAppLifetime
{
    private readonly SyncTray syncTray = new SyncTray(sync, trayIcon, startup.ByUser(configure.Show));
    private readonly object stopGate = new();
    private Task? stopping;
    private bool claimed;
    private EventHandler? followAvailability;

    /// <inheritdoc />
    public void Start(Action endApp)
    {
        var addByUser = startup.ByUser(showAdd);
        var viewTasksByUser = startup.ByUser(showViewTasks);
        var contactsByUser = startup.ByUser(showContacts);
        var configureByUser = startup.ByUser(configure.Show);
        if (!oneInstance.Claim(viewTasksByUser))
        {
            endApp();
            return;
        }

        // The Contacts item shows only while contacts are available, so the menu is built again when that changes.
        IReadOnlyList<TrayMenuItem> Menu()
        {
            List<TrayMenuItem> items = [new("Add new task", addByUser), new("Task View", viewTasksByUser)];
            if (contacts.IsAvailable)
            {
                items.Add(new("Contacts", contactsByUser));
            }

            items.Add(new("Configure", configureByUser));
            items.Add(new("Exit", endApp));
            return items;
        }

        claimed = true;
        trayIcon.Show(AppName.Value, Menu(), viewTasksByUser);
        syncTray.Attach();
        bool shortcutsReady = configure.ConnectShortcuts(addShortcut, manageShortcut, configureShortcut, contactsShortcut);

        updates.Start(createExit(endApp));
        startup.Begin(!shortcutsReady);
        followAvailability = (_, _) =>
        {
            trayIcon.UpdateMenu(Menu());
            contactsShortcut.AvailabilityChanged();
        };
        contacts.AvailableChanged += followAvailability;
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
        Attempt(StopFollowingAvailability, failures);
        Attempt(addShortcut.Unregister, failures);
        Attempt(manageShortcut.Unregister, failures);
        Attempt(configureShortcut.Unregister, failures);
        Attempt(contactsShortcut.Unregister, failures);
        Attempt(trayIcon.Remove, failures);

        if (claimed)
        {
            await Attempt(() => sync.StopAsync(CancellationToken.None), failures);
            await Attempt(() => sync.DisposeAsync().AsTask(), failures);
        }

        // After Sync, since this starts Velopack's updater when an update ended the app, which waits only 60 seconds for it.
        await Attempt(updates.StopAsync, failures);

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

    private void StopFollowingAvailability()
    {
        if (followAvailability is not null)
        {
            contacts.AvailableChanged -= followAvailability;
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
