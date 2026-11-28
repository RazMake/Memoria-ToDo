using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;

namespace Memoria.ToDo.Android.DeviceHost;

internal static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
        .ConfigureUITesting()
        .UseVisualTestRunner(configuration => configuration
            .AddCliConfiguration()
            .AddConsoleResultChannel()
            .AddTestAssembly(typeof(MauiProgram).Assembly)
            .AddXunit3())
        .Build();
}
