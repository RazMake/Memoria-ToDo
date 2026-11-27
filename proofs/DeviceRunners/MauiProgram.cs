using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;

namespace Memoria.DeviceRunnersProof;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .ConfigureUITesting()
            .UseVisualTestRunner(configuration => configuration
                .AddCliConfiguration()
                .AddConsoleResultChannel()
                .AddTestAssembly(typeof(NativeScreenTests).Assembly)
                .AddXunit3());
        return builder.Build();
    }
}
