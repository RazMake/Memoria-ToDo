using System.Diagnostics;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The real app for the bridge: the one-instance signal tells whether it runs, and a start runs Velopack's starter, or this program in a copy with no install.</summary>
internal sealed class ProcessBridgeApp(string? starterPath, string ownProgramPath) : IBridgeApp
{
    public bool IsRunning() => NamedSignalOneInstance.IsClaimed(NamedSignalOneInstance.AppSignalName);

    public void Start()
    {
        // The shell starts it, so it doesn't inherit the assistant's standard input, output, and error, which would hold them open.
        using var started = Process.Start(new ProcessStartInfo(starterPath ?? ownProgramPath, StartArguments.AtSignIn) { UseShellExecute = true })
            ?? throw new InvalidOperationException("Windows didn't start the app.");
    }
}
