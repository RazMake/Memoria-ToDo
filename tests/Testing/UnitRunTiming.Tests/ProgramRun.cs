using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>One test's own folder with a valid set of the program's inputs, and a way to run the program on them.</summary>
internal sealed class ProgramRun : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    public ProgramRun(string duration = "3s 249ms")
    {
        Root = Path.Combine(Path.GetTempPath(), "timing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        ReportsFolder = Path.Combine(Root, "results");
        Directory.CreateDirectory(Path.Combine(ReportsFolder, "unit"));
        File.WriteAllText(
            Path.Combine(ReportsFolder, "unit", "a.junit.xml"),
            Samples.Report(Samples.TestCase("Case_Slow", "1.212000"), Samples.TestCase("Case_Fast", "0.014000")));
        OutputFile = Path.Combine(Root, "test-output.txt");
        File.WriteAllText(OutputFile, Samples.Output(duration));
        TextFile = Path.Combine(Root, "issue.md");
    }

    public string Root { get; }
    public string ReportsFolder { get; }
    public string OutputFile { get; }
    public string TextFile { get; }

    /// <summary>The five arguments, valid for this folder: a 2.5-second limit and an open issue.</summary>
    public List<string> Arguments(string limitSeconds = "2.5", string issueOpen = "true") =>
    [
        "--reports", ReportsFolder,
        "--test-output", OutputFile,
        "--limit-seconds", limitSeconds,
        "--issue-open", issueOpen,
        "--text-file", TextFile,
    ];

    public static void Replace(List<string> arguments, string name, string value) =>
        arguments[arguments.IndexOf(name) + 1] = value;

    public static void Remove(List<string> arguments, string name) =>
        arguments.RemoveRange(arguments.IndexOf(name), 2);

    public (int ExitCode, string Output, string Error) Run(IEnumerable<string> arguments)
    {
        var dotnet = Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var start = new ProcessStartInfo(Path.GetFullPath(dotnet))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(typeof(TimingIssue).Assembly.Location);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The timing program didn't start.");
        using var bound = new CancellationTokenSource(Bound);
        var output = process.StandardOutput.ReadToEndAsync(bound.Token);
        var error = process.StandardError.ReadToEndAsync(bound.Token);
        process.WaitForExitAsync(bound.Token).GetAwaiter().GetResult();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
