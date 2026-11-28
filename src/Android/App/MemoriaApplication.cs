using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Google.Android.Material.Color;
using Memoria.ToDo.Android.Configure;
using Memoria.ToDo.Android.Platform;
using Memoria.ToDo.Android.Shell;
using Memoria.ToDo.Android.TaskEditor;
using Memoria.ToDo.Android.TaskList;
using Serilog;
using Serilog.Core;

namespace Memoria.ToDo.Android.App;

/// <summary>
/// The composition root. Android makes it before any screen, so it builds the logger and every module here, Shell last.
/// </summary>
[Application]
internal sealed class MemoriaApplication(IntPtr handle, JniHandleOwnership ownership) : Application(handle, ownership)
{
    public override void OnCreate()
    {
        base.OnCreate();
        DynamicColors.ApplyToActivitiesIfAvailable(this);

        var root = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(new LogcatSink()).CreateLogger();
        ILogger ForComponent(string name) => root.ForContext(Constants.SourceContextPropertyName, name);
        var log = ForComponent("App");

        // Android ends the app after an error nobody handles, on any thread, including one thrown while building the
        // modules below; this is the one hook that sees each of them, once.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Fatal(e.ExceptionObject as Exception, "An error no module handled ended the app");

        var opener = new ScreenOpener(this);
        var add = AddScreen.Create(opener, ForComponent("Task editor"));
        var configure = ConfigureScreen.Create(opener, ForComponent("Configure"));
        var taskView = TaskViewScreen.Create(add, configure.Show, opener, ForComponent("Task list"));
        AppEntry.Connect(taskView, ForComponent("Shell"));

        log.Information("The app started, version {Version}", VersionName());
    }

    private string? VersionName()
    {
        var packages = PackageManager!;
        var info = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? packages.GetPackageInfo(PackageName!, PackageManager.PackageInfoFlags.Of(0L))
            : packages.GetPackageInfo(PackageName!, 0);
        return info!.VersionName;
    }
}
