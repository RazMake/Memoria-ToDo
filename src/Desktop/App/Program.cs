using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Velopack;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The composition root: the only place that chooses the implementations the modules use.</summary>
internal static class Program
{
    private const string RepositoryUrl = "https://github.com/RazMake/Memoria-ToDo";

    [STAThread]
    private static int Main()
    {
        // Velopack's install, update, and uninstall steps must run before anything else.
        VelopackApp.Build().Run();

        return AppRunner.Run(() => TrayApp.Create(
            new TrayIcon(),
            AddWindow.Create(),
            ViewTasksWindow.Create(),
            ConfigureWindow.Create(),
            new VelopackUpdates(RepositoryUrl),
            TimeProvider.System));
    }
}
