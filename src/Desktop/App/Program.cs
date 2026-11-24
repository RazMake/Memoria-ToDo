using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Logs;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The composition root: the only place that chooses the implementations the modules use.</summary>
internal static class Program
{
    private const string RepositoryUrl = "https://github.com/RazMake/Memoria-ToDo";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    private static int Main(string[] args)
    {
        // Velopack's install, update, and uninstall steps must run before anything else.
        // Each hook builds its steps when it runs, so the starter's path is read once Velopack has found its install.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false) // Updates applies an update when the user or the night run says so, never at a start.
            .OnAfterInstallFastCallback(_ => HookSteps().Install())
            .OnBeforeUninstallFastCallback(_ => HookSteps().Uninstall())
            .Run();

        if (StartArguments.IsMcpBridge(args))
        {
            // The bridge is no app: no log files, no one-instance claim, and no WPF. Without a message pump, blocking on its one task is safe.
            return RunMcpBridge();
        }

        // The log is made after Velopack's steps, so an install or uninstall hook, which logs to Velopack's own log, leaves no file here.
        var log = AppLog.Create(AppLog.Folder);
        // Raised at the end of Main and by Environment.Exit, so an app ended by an error flushes the log too.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => log.Dispose();

        return AppRunner.Run(() =>
        {
            var files = new WindowsFileSystem();
            var repository = new SyncedRepository(log.ForModule("Platform"));
            var tasks = TaskStore.Create(files, new WpfImageEncoder(), TimeProvider.System, DraftFolder(), repository, log.ForModule("Tasks"));
            var contacts = ContactStore.Create(files, repository, log.ForModule("Contacts"));
            var theme = new WindowsTheme(new WindowsThemeSetting());
            var clipboard = new WpfClipboard();
            var imageFiles = new WpfImageFileChooser();
            var browser = new ShellDefaultBrowser();
            var deviceFolder = DeviceFolder();
            var appLog = log.ForModule("App");
            var repositories = new RepositorySelection(files, repository.Select, System.IO.Path.Combine(deviceFolder, "repository-folder.txt"), appLog);
            var closeWindowKeys = new CloseWindowKeys();
            // Each open of a description web view serves the folder the repository is in then.
            Func<IDescriptionWebView> createWebView = () => new WpfDescriptionWebView(
                repository.Folder, tasks.EditImagesFolder, BrowserDataFolder(), files, tasks.Synchronization.Files, appLog);
            var pointer = new MousePointer();
            var updates = AppUpdates.Create(
                new VelopackUpdates(RepositoryUrl), files, deviceFolder, TimeProvider.System, theme, createWebView, browser, closeWindowKeys,
                pointer, log.ForModule("Updates"));
            var saveKeys = new SaveKeys();
            var sync = SyncService.Create(
                repository,
                tasks.Synchronization,
                contacts.Files,
                files,
                new WindowsDeviceKeyStore(files, new WindowsUserProtection(), deviceFolder),
                repositories,
                TimeProvider.System,
                deviceFolder,
                Environment.MachineName,
                TimeSpan.FromMinutes(30),
                log.ForModule("Sync"));
            var (add, viewTasks) = TaskWindows.Create(
                tasks,
                contacts.Contacts,
                theme,
                TimeProvider.System,
                clipboard,
                imageFiles,
                files,
                createWebView,
                browser,
                sync,
                closeWindowKeys,
                updates,
                saveKeys,
                pointer,
                log.ForModule("Task editor"),
                log.ForModule("Task list"));
            var logSource = new LogFileSource(AppLog.Folder, () => log.WriteProblem, appLog);
            var logs = LogsWindow.Create(logSource, theme, closeWindowKeys, TimeProvider.System, log.ForModule("Logs"));
            var startWithWindows = StartWithWindows(appLog);
            var mcpServer = McpServer.McpServer.Create(
                tasks, tasks.TagColors, tasks.TagOrder, tasks.Settings, contacts.Contacts, sync, updates, startWithWindows, logSource, files,
                deviceFolder, PipeName.ForCurrentUser(), Environment.ProcessPath!, TimeProvider.System, log.ForModule("MCP server"));
            return TrayApp.Create(
                new TrayIcon(),
                add,
                viewTasks,
                ContactsWindow.Create(contacts.Contacts, theme, TimeProvider.System, closeWindowKeys, saveKeys, pointer, log.ForModule("Contact list")),
                ConfigureWindow.Create(
                    startWithWindows,
                    tasks.Settings,
                    sync,
                    new WindowsSetupActions(browser),
                    closeWindowKeys,
                    theme,
                    updates,
                    mcpServer,
                    saveKeys,
                    log.ForModule("Configure")),
                logs,
                updates,
                mcpServer,
                new HotKeyShortcut(),
                new NamedSignalOneInstance(NamedSignalOneInstance.AppSignalName),
                StartArguments.IsStartAtSignIn(args),
                sync,
                log.ForModule("Shell"));
        }, log.ForModule("Platform"));
    }

    private static int RunMcpBridge() => new McpBridge(
            new ProcessBridgeApp(InstalledStarter.Find(), Environment.ProcessPath!),
            new NamedPipeBridgePipe(PipeName.ForCurrentUser()),
            TimeProvider.System)
        .RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.Error, CancellationToken.None)
        .GetAwaiter().GetResult();

    private static RunKeyStartWithWindows StartWithWindows(Serilog.ILogger logger) => new(RunKeyPath, InstalledStarter.Find(), logger);

    // Under %LocalAppData%, apart from Velopack's install folder, so updating or uninstalling leaves it. The one
    // authority for the device's own files, outside every repository clone; the saved repository folder stays in it.
    private static string DeviceFolder() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value);

    // The draft lives on this device only, apart from Velopack's install folder.
    private static string DraftFolder() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value, "draft");

    // The web view's browser data lives on this device only, apart from Velopack's install folder.
    private static string BrowserDataFolder() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value, "WebView2");

    // A hook shows no window and runs before the log exists, so a failed step goes to Velopack's log file and the step itself logs nothing.
    private static StartWithWindowsSteps HookSteps() => new(
        StartWithWindows(Serilog.Core.Logger.None),
        failure => VelopackLocator.Current.Log.Log(VelopackLogLevel.Error, "The Start with Windows step failed.", failure));
}
