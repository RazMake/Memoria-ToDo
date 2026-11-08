using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The program the platform's integration tests start. WPF allows one Application per process, so each test
/// starts this program with one scenario name, then checks the exit code, the lines it prints, and its windows.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Scenarios.All.TryGetValue(args[0], out var scenario))
        {
            Console.Error.WriteLine($"Usage: PlatformTestHost <{string.Join('|', Scenarios.All.Keys)}>");
            return 2;
        }

        return AppRunner.Run(() => new ScenarioLifetime(scenario.Start, scenario.Stop));
    }
}
