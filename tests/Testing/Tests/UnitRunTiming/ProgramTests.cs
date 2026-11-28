using Xunit;

namespace Memoria.ToDo.Testing.UnitRunTiming;

[Integration]
public sealed class Running_the_timing_program_on_real_files : IDisposable
{
    private readonly ProgramRun _run = new();

    public void Dispose() => _run.Dispose();

    [Fact]
    public void Prints_open_and_writes_the_issue_text_when_the_run_is_over_the_limit_and_no_issue_is_open()
    {
        var result = _run.Run(_run.Arguments(limitSeconds: "2.5", issueOpen: "false"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("open", result.Output.Trim());
        var text = File.ReadAllText(_run.TextFile);
        Assert.Contains("3s 249ms", text);
        Assert.Contains("Case_Slow", text);
        Assert.True(text.IndexOf("Case_Slow", StringComparison.Ordinal) < text.IndexOf("Case_Fast", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2.5", "note")]
    [InlineData("5", "close")]
    public void Prints_the_action_and_writes_the_text_when_the_issue_is_open(string limitSeconds, string action)
    {
        var result = _run.Run(_run.Arguments(limitSeconds: limitSeconds, issueOpen: "true"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(action, result.Output.Trim());
        Assert.Contains("3s 249ms", File.ReadAllText(_run.TextFile));
    }

    [Fact]
    public void Prints_none_and_creates_no_file_when_the_run_is_under_the_limit_and_no_issue_is_open()
    {
        var result = _run.Run(_run.Arguments(limitSeconds: "5", issueOpen: "false"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("none", result.Output.Trim());
        Assert.False(File.Exists(_run.TextFile));
    }

    [Fact]
    public void Reads_the_reports_in_subfolders_and_ignores_files_that_are_not_junit_reports()
    {
        Directory.CreateDirectory(Path.Combine(_run.ReportsFolder, "deep", "er"));
        File.WriteAllText(
            Path.Combine(_run.ReportsFolder, "deep", "er", "b.junit.xml"),
            Samples.Report(Samples.TestCase("Case_Deep", "0.900000")));
        File.WriteAllText(Path.Combine(_run.ReportsFolder, "notes.xml"), "not a report");

        var result = _run.Run(_run.Arguments(issueOpen: "false"));

        Assert.Equal("open", result.Output.Trim());
        Assert.Contains("Case_Deep", File.ReadAllText(_run.TextFile));
    }

}

[Integration]
public sealed class Running_the_timing_program_on_invalid_input : IDisposable
{
    private readonly ProgramRun _run = new();

    public void Dispose() => _run.Dispose();

    /// <param name="problem">Words the program's one error line must contain, so the program (not a later failure) caught the input.</param>
    private void AssertInvalid(IEnumerable<string> arguments, string problem = "")
    {
        var result = _run.Run(arguments);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.False(string.IsNullOrWhiteSpace(result.Error), "The program named no problem.");
        Assert.Contains(problem, result.Error);
        Assert.Single(result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Assert.False(File.Exists(_run.TextFile), "The program wrote the text file.");
    }

    [Fact]
    public void Exits_with_1_when_the_text_files_folder_does_not_exist()
    {
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--text-file", Path.Combine(_run.Root, "missing", "issue.md"));

        AssertInvalid(arguments, "text file");
    }

    [Fact]
    public void Exits_with_1_when_the_reports_folder_does_not_exist()
    {
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--reports", Path.Combine(_run.Root, "no-such-folder"));

        AssertInvalid(arguments, "reports folder");
    }

    [Fact]
    public void Exits_with_1_when_the_reports_folder_holds_no_report()
    {
        var empty = Path.Combine(_run.Root, "empty");
        Directory.CreateDirectory(empty);
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--reports", empty);

        AssertInvalid(arguments, "holds no");
    }

    [Fact]
    public void Exits_with_1_when_a_report_is_not_JUnit_XML()
    {
        File.WriteAllText(Path.Combine(_run.ReportsFolder, "bad.junit.xml"), "<html />");

        AssertInvalid(_run.Arguments());
    }

    [Fact]
    public void Writes_nothing_over_an_existing_text_file_when_the_input_is_invalid()
    {
        File.WriteAllText(_run.TextFile, "earlier text");
        File.WriteAllText(_run.OutputFile, "no duration here");

        var result = _run.Run(_run.Arguments());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("earlier text", File.ReadAllText(_run.TextFile));
    }
}
