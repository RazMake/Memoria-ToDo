using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
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
            .OnAfterInstallFastCallback(_ => HookSteps().Install())
            .OnBeforeUninstallFastCallback(_ => HookSteps().Uninstall())
            .Run();

        return AppRunner.Run(() => TrayApp.Create(
            new TrayIcon(),
            AddWindow.Create(),
            ViewTasksWindow.Create(),
            ConfigureWindow.Create(StartWithWindows(), new GitRepositoryFolder(RepositoryFolderSetting())),
            new VelopackUpdates(RepositoryUrl),
            new HotKeyShortcut(),
            // The product name, not Velopack's installer ID, which a copy run from a build folder doesn't have:
            // every copy must count as the same app.
            new NamedSignalOneInstance($"{AppName.Value}.OneInstance"),
            StartArguments.IsStartAtSignIn(args),
            TimeProvider.System));
    }

    private static RunKeyStartWithWindows StartWithWindows() => new(RunKeyPath, InstalledStarter.Find());

    // Under %LocalAppData%, apart from Velopack's install folder, so updating or uninstalling leaves it.
    private static string RepositoryFolderSetting() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value, "repository-folder.txt");

    // A hook shows no window, so a failed step goes to Velopack's log file.
    private static StartWithWindowsSteps HookSteps() => new(
        StartWithWindows(),
        failure => VelopackLocator.Current.Log.Log(VelopackLogLevel.Error, "The Start with Windows step failed.", failure));
}
