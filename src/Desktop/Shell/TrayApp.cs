using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.LocalAPI;
using Memoria.ToDo.Desktop.Logs;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Sync;
using Serilog;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Builds the tray app.</summary>
public static class TrayApp
{
    /// <summary>
    /// Creates the app's lifetime. Start first claims the sign-in session; when another instance runs, it ends the app
    /// and does nothing else. Otherwise it adds the tray icon with its menu (Add new task, Task View, Contacts while
    /// contacts are available, Configure, Logs, Exit; a left click shows Task View; Logs, and a click on a sync error
    /// notification, close every other window and show the Logs window at once, whatever the setup), connects Configure's four shortcuts (a press
    /// shows Add, Task View, Contacts, or Configure, and does nothing while Configure is open), begins Updates with the
    ///     app's exit, starts the MCP server once local initialization has loaded, and starts the shared sync
    /// service. Startup and later actions open Configure until setup is complete;
    /// otherwise startup shows Task View when <paramref name="startedAtSignIn"/> is false.
    /// Configure also opens when an initial shortcut could not be registered, after Task View on a configured
    /// start by hand. These startup windows wait for local initialization.
    /// StopAsync stops the MCP server, joins sync, then Updates' stop, without blocking the UI, unregisters the four shortcuts,
    /// removes the icon, and releases the claim. Cleanup still completes when one step fails.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IContactsWindow contacts,
        IConfigureWindow configure,
        ILogsWindow logs,
        IUpdateLifetime updates,
        ILocalApiLifetime localApi,
        IMcpServerLifetime mcpServer,
        IGlobalShortcut shortcut,
        IOneInstance oneInstance,
        bool startedAtSignIn,
        ISyncService sync,
        ILogger logger) =>
        Create(trayIcon, add, viewTasks, contacts, configure, logs, updates, localApi, mcpServer, shortcut, oneInstance, startedAtSignIn, sync, logger, () => new WpfUiThread());

    internal static IAppLifetime Create(
        ITrayIcon trayIcon,
        IAddWindow add,
        IViewTasksWindow viewTasks,
        IContactsWindow contacts,
        IConfigureWindow configure,
        ILogsWindow logs,
        IUpdateLifetime updates,
        ILocalApiLifetime localApi,
        IMcpServerLifetime mcpServer,
        IGlobalShortcut shortcut,
        IOneInstance oneInstance,
        bool startedAtSignIn,
        ISyncService sync,
        ILogger logger,
        Func<IUiThread> uiThread)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(viewTasks);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(localApi);
        ArgumentNullException.ThrowIfNull(mcpServer);
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(oneInstance);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(uiThread);

        bool ConfigureOpen() => configure.IsOpen;
        bool AddOpen() => add.IsOpen;
        bool ViewTasksOpen() => viewTasks.IsOpen;
        bool ContactsOpen() => contacts.IsOpen;
        bool CloseAdd()
        {
            add.Close();
            return true;
        }

        OtherWindow addWindow = new(AddOpen, CloseAdd);
        OtherWindow viewTasksWindow = new(ViewTasksOpen, viewTasks.Close);
        OtherWindow contactsWindow = new(ContactsOpen, contacts.Close);

        var redirect = new SetupRedirect(sync, configure);
        var showAdd = redirect.Through(add.Show);
        var showViewTasks = redirect.Through(viewTasks.Show);
        var showContacts = redirect.Through(contacts.Show);
        var startup = new StartupNavigation(sync.Setup, configure.Show, showViewTasks, startedAtSignIn, uiThread);
        var addByUser = startup.ByUser(showAdd);
        var viewTasksByUser = startup.ByUser(showViewTasks);
        var contactsByUser = startup.ByUser(showContacts);
        var configureByUser = startup.ByUser(configure.Show);

        return new ShellLifetime(
            trayIcon,
            showAdd,
            showViewTasks,
            showContacts,
            contacts,
            configure,
            oneInstance,
            new ActiveShortcut(shortcut, Press("Add new task", new ShortcutSwitch(ConfigureOpen, AddOpen, [viewTasksWindow, contactsWindow], addByUser)), "Add new task", logger),
            new ActiveShortcut(shortcut, Press("Task View", new ShortcutSwitch(ConfigureOpen, ViewTasksOpen, [addWindow, contactsWindow], viewTasksByUser)), "Task View", logger),
            new ActiveShortcut(shortcut, Press("Configure", new ShortcutSwitch(ConfigureOpen, ConfigureOpen, [addWindow, viewTasksWindow, contactsWindow], configureByUser)), "Configure", logger),
            new ContactsShortcutAction(
                shortcut,
                Press("Contacts", new ShortcutSwitch(ConfigureOpen, ContactsOpen, [addWindow, viewTasksWindow], contactsByUser)),
                () => contacts.IsAvailable,
                startup.ShortcutFailed,
                logger),
            startup,
            updates,
            localApi,
            new McpServerStart(sync.Setup, mcpServer, uiThread, logger),
            mcpServer,
            endApp => new ShellExit(add, viewTasks, contacts, configure, endApp),
            sync,
            ShowLogs,
            logger);

        Action Press(string name, ShortcutSwitch switcher) => LoggedUse.Of(logger, $"Shortcut for {name} used", switcher.Press);

        // Logs opens alone. Task View and Contacts may first ask about a changed edit; Logs opens whatever the answer.
        void ShowLogs()
        {
            add.Close();
            viewTasks.Close();
            contacts.Close();
            configure.Close();
            logs.Show();
        }
    }
}
