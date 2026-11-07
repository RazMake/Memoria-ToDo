using System.Diagnostics;
using System.IO;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// One run of the platform's test program with one scenario. Tests wait for it to end, read what it printed,
/// and, for an unhandled error, read and close its message box. Disposing it stops a program that hangs.
/// </summary>
internal sealed class HostProcess : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly Process _process;
    private readonly DialogWatcher _dialogs;
    private readonly List<string> _lines = [];

    private HostProcess(Process process, DialogWatcher dialogs)
    {
        _process = process;
        _dialogs = dialogs;
    }

    /// <summary>What the program printed, one entry per line. Read it after <see cref="WaitForExit"/>.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public static HostProcess Start(string scenario)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(HostPath(), scenario)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        var host = new HostProcess(process, new DialogWatcher()); // Listening before the program starts, so no message box is missed.
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (host._lines) { host._lines.Add(e.Data); }
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        return host;
    }

    /// <summary>Waits for the program to end, failing the test after a limit, and returns its exit code.</summary>
    public int WaitForExit()
    {
        Assert.True(_process.WaitForExit(Limit), $"The test program didn't end within {Limit}.");
        _process.WaitForExit(); // Lets the output reader read the last lines.
        return _process.ExitCode;
    }

    /// <summary>Waits for the program's message box, closes it, and returns its text. Fails after a limit.</summary>
    public string CloseMessageBox()
    {
        var box = _dialogs.WaitForMessageBoxOf(_process, Limit);
        var text = Win32.MessageBoxText(box);
        Win32.CloseWindow(box);
        return text;
    }

    public int Count(string line) => _lines.Count(l => l == line);

    public void Dispose()
    {
        _dialogs.Dispose();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }
        _process.Dispose();
    }

    // The project reference builds the program but doesn't copy it, so it's found in its own project's output,
    // which mirrors this test project's: <tests>\Desktop\<project>\bin\<configuration>\<framework>.
    private static string HostPath()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var framework = output.Name;
        var configuration = output.Parent!.Name;
        var desktop = output.Parent.Parent!.Parent!.Parent!;
        const string name = "Memoria.ToDo.Desktop.PlatformTestHost";
        return Path.Combine(desktop.FullName, "PlatformTestHost", "bin", configuration, framework, name + ".exe");
    }
}
