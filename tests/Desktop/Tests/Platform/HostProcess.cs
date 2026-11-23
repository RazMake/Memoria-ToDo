using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Sdk;

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
    private readonly List<HostLogEntry> _entries = [];
    private readonly List<string> _output = [];

    private HostProcess(Process process, DialogWatcher dialogs)
    {
        _process = process;
        _dialogs = dialogs;
    }

    /// <summary>What the program printed, one entry per line, apart from its log entries. Read it after <see cref="WaitForExit"/>.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>The log entries the platform wrote, in order. Read it after <see cref="WaitForExit"/>.</summary>
    public IReadOnlyList<HostLogEntry> LogEntries => _entries;

    /// <summary>
    /// Everything the program printed, lines and log entries together in the order it printed them, each log entry as its
    /// <see cref="HostLogEntry.LinePrefix"/> line. Read it after <see cref="WaitForExit"/>.
    /// </summary>
    public IReadOnlyList<string> Output => _output;

    public static HostProcess Start(string scenario)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(HostPath(), scenario)
            {
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            },
        };
        var host = new HostProcess(process, new DialogWatcher()); // Listening before the program starts, so no message box is missed.
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (host._lines)
            {
                host._output.Add(e.Data);
                if (e.Data.StartsWith(HostLogEntry.LinePrefix, StringComparison.Ordinal))
                {
                    host._entries.Add(HostLogEntry.Parse(e.Data));
                }
                else
                {
                    host._lines.Add(e.Data);
                }
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        return host;
    }

    /// <summary>Tells a scenario that keeps the app running to end it, by writing a line to the program's input.</summary>
    public void TellToEndApp() => _process.StandardInput.WriteLine("end");

    /// <summary>Fails when the program showed a message box nobody closed. Call it after <see cref="WaitForExit"/>.</summary>
    public void AssertNoOtherMessageBox() =>
        Assert.Throws<XunitException>(() => _dialogs.WaitForMessageBoxOf(_process, Limit));

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
        try
        {
            var box = _dialogs.WaitForMessageBoxOf(_process, Limit);
            var text = Win32.MessageBoxText(box);
            Win32.CloseWindow(box);
            return text;
        }
        catch (XunitException error)
        {
            lock (_lines)
            {
                throw new XunitException($"{error.Message}{Environment.NewLine}Host output:{Environment.NewLine}{string.Join(Environment.NewLine, _output)}");
            }
        }
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
    // which has this test project's configuration but its own framework folder:
    // <tests>\Desktop\PlatformTestHost\bin\<configuration>\<framework>.
    private static string HostPath()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var desktop = output.Parent.Parent!.Parent!.Parent!;
        const string name = "Memoria.ToDo.Desktop.PlatformTestHost";
        var hostOutputs = Path.Combine(desktop.FullName, "PlatformTestHost", "bin", configuration);
        var found = (Directory.Exists(hostOutputs) ? Directory.GetDirectories(hostOutputs) : [])
            .Select(framework => Path.Combine(framework, name + ".exe"))
            .Where(File.Exists)
            .ToArray();
        return found.Length == 1
            ? found[0]
            : throw new InvalidOperationException(
                $"Expected exactly one {name}.exe in a framework folder of {hostOutputs}, but found {found.Length}.");
    }
}
