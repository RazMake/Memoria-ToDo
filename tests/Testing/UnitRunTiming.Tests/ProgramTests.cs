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

    [Fact]
    public void Prints_note_when_the_run_is_over_the_limit_and_the_issue_is_open()
    {
        var result = _run.Run(_run.Arguments(limitSeconds: "2.5", issueOpen: "true"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("note", result.Output.Trim());
        Assert.Contains("Case_Slow", File.ReadAllText(_run.TextFile));
    }

    [Fact]
    public void Prints_close_and_writes_one_line_when_the_run_is_under_the_limit_and_the_issue_is_open()
    {
        var result = _run.Run(_run.Arguments(limitSeconds: "5", issueOpen: "true"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("close", result.Output.Trim());
        var text = File.ReadAllText(_run.TextFile).Trim();
        Assert.DoesNotContain('\n', text);
        Assert.Contains("3s 249ms", text);
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
    public void Counts_a_run_exactly_at_the_limit_as_under_it()
    {
        var result = _run.Run(_run.Arguments(limitSeconds: "3.249", issueOpen: "false"));

        Assert.Equal("none", result.Output.Trim());
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

    [Fact]
    public void Ignores_terminal_color_codes_in_the_saved_output()
    {
        File.WriteAllText(_run.OutputFile, "\u001b[90m  duration:\u001b[m \u001b[1m3s 249ms\u001b[m\r\n");

        var result = _run.Run(_run.Arguments(limitSeconds: "5", issueOpen: "false"));

        Assert.Equal("none", result.Output.Trim());
    }
}

[Integration]
public sealed class Running_the_timing_program_on_invalid_input : IDisposable
{
    private readonly ProgramRun _run = new();

    public void Dispose() => _run.Dispose();

    private void AssertInvalid(IEnumerable<string> arguments)
    {
        var result = _run.Run(arguments);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.False(string.IsNullOrWhiteSpace(result.Error), "The program named no problem.");
        Assert.Single(result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Assert.False(File.Exists(_run.TextFile), "The program wrote the text file.");
    }

    [Theory]
    [InlineData("--reports")]
    [InlineData("--test-output")]
    [InlineData("--limit-seconds")]
    [InlineData("--issue-open")]
    [InlineData("--text-file")]
    public void Exits_with_1_when_an_argument_is_missing(string missing)
    {
        var arguments = _run.Arguments();
        ProgramRun.Remove(arguments, missing);

        AssertInvalid(arguments);
    }

    [Fact]
    public void Exits_with_1_when_an_argument_is_repeated() =>
        AssertInvalid([.. _run.Arguments(), "--limit-seconds", "3"]);

    [Fact]
    public void Exits_with_1_when_an_argument_is_unknown() =>
        AssertInvalid([.. _run.Arguments(), "--verbose", "yes"]);

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("fast")]
    [InlineData("2,5")]
    public void Exits_with_1_when_the_limit_is_not_a_positive_number(string limit) =>
        AssertInvalid(_run.Arguments(limitSeconds: limit));

    [Theory]
    [InlineData("yes")]
    [InlineData("True ")]
    [InlineData("")]
    public void Exits_with_1_when_issue_open_is_neither_true_nor_false(string value) =>
        AssertInvalid(_run.Arguments(issueOpen: value));

    [Fact]
    public void Exits_with_1_when_the_text_files_folder_does_not_exist()
    {
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--text-file", Path.Combine(_run.Root, "missing", "issue.md"));

        AssertInvalid(arguments);
    }

    [Fact]
    public void Exits_with_1_when_the_reports_folder_does_not_exist()
    {
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--reports", Path.Combine(_run.Root, "no-such-folder"));

        AssertInvalid(arguments);
    }

    [Fact]
    public void Exits_with_1_when_the_reports_folder_holds_no_report()
    {
        var empty = Path.Combine(_run.Root, "empty");
        Directory.CreateDirectory(empty);
        var arguments = _run.Arguments();
        ProgramRun.Replace(arguments, "--reports", empty);

        AssertInvalid(arguments);
    }

    [Fact]
    public void Exits_with_1_when_a_report_is_not_JUnit_XML()
    {
        File.WriteAllText(Path.Combine(_run.ReportsFolder, "bad.junit.xml"), "<html />");

        AssertInvalid(_run.Arguments());
    }

    [Fact]
    public void Exits_with_1_when_the_saved_output_has_no_duration_line()
    {
        File.WriteAllText(_run.OutputFile, "Test run summary: Passed!\r\n  total: 4\r\n");

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
