using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.FormattedView;
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

        return AppRunner.Run(() =>
        {
            var files = new WindowsFileSystem();
            var repository = new SyncedRepository();
            var tasks = TaskStore.Create(files, new WpfImageEncoder(), TimeProvider.System, DraftFolder(), repository);
            var contacts = ContactStore.Create(files, repository);
            var theme = new WindowsTheme(new WindowsThemeSetting());
            var clipboard = new WpfClipboard();
            var imageFiles = new WpfImageFileChooser();
            var browser = new ShellDefaultBrowser();
            var deviceFolder = DeviceFolder();
            var repositories = new RepositorySelection(files, repository.Select, System.IO.Path.Combine(deviceFolder, "repository-folder.txt"));
            var closeWindowKeys = new CloseWindowKeys();
            // Each open of a description web view serves the folder the repository is in then.
            Func<IDescriptionWebView> createWebView = () => new WpfDescriptionWebView(
                repository.Folder, tasks.EditImagesFolder, BrowserDataFolder(), files, tasks.Synchronization.Files);
            var updates = AppUpdates.Create(
                new VelopackUpdates(RepositoryUrl), files, deviceFolder, TimeProvider.System, theme, createWebView, browser, closeWindowKeys);
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
                TimeSpan.FromMinutes(30));
            var (add, viewTasks) = TaskWindows.Create(
                tasks,
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
                saveKeys);
            return TrayApp.Create(
                new TrayIcon(),
                add,
                viewTasks,
                ConfigureWindow.Create(
                    StartWithWindows(),
                    tasks.Settings,
                    sync,
                    new WindowsSetupActions(browser),
                    closeWindowKeys,
                    theme,
                    updates,
                    saveKeys),
                updates,
                new HotKeyShortcut(),
                // The product name, not Velopack's installer ID, which a copy run from a build folder doesn't have:
                // every copy must count as the same app.
                new NamedSignalOneInstance($"{AppName.Value}.OneInstance"),
                StartArguments.IsStartAtSignIn(args),
                sync);
        });
    }

    private static RunKeyStartWithWindows StartWithWindows() => new(RunKeyPath, InstalledStarter.Find());

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

    // A hook shows no window, so a failed step goes to Velopack's log file.
    private static StartWithWindowsSteps HookSteps() => new(
        StartWithWindows(),
        failure => VelopackLocator.Current.Log.Log(VelopackLogLevel.Error, "The Start with Windows step failed.", failure));
}
