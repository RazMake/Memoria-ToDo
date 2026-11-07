using System.Globalization;
using Xunit;

namespace Memoria.ToDo.Testing.UnitRunTiming;

[Unit]
public sealed class Choosing_the_issue_action
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    private static TimingDecision Decide(string duration, bool issueOpen, TimeSpan? limit = null) =>
        TimingIssue.Decide([Samples.Report(Samples.TestCase("Case_A", "0.100000"))], Samples.Output(duration), limit ?? Limit, issueOpen);

    [Fact]
    public void Opens_the_issue_when_the_run_is_over_the_limit_and_no_issue_is_open() =>
        Assert.Equal(IssueAction.Open, Decide("7s 000ms", issueOpen: false).Action);

    [Fact]
    public void Adds_a_note_when_the_run_is_over_the_limit_and_the_issue_is_open() =>
        Assert.Equal(IssueAction.Note, Decide("7s 000ms", issueOpen: true).Action);

    [Fact]
    public void Closes_the_issue_when_the_run_is_under_the_limit_and_the_issue_is_open() =>
        Assert.Equal(IssueAction.Close, Decide("3s 249ms", issueOpen: true).Action);

    [Fact]
    public void Does_nothing_when_the_run_is_under_the_limit_and_no_issue_is_open()
    {
        var decision = Decide("3s 249ms", issueOpen: false);

        Assert.Equal(IssueAction.None, decision.Action);
        Assert.Null(decision.Text);
    }

    [Fact]
    public void Counts_a_run_exactly_at_the_limit_as_under_it_and_closes_the_open_issue() =>
        Assert.Equal(IssueAction.Close, Decide("5s 000ms", issueOpen: true).Action);

    [Fact]
    public void Counts_a_run_exactly_at_the_limit_as_under_it_and_does_nothing_without_an_issue() =>
        Assert.Equal(IssueAction.None, Decide("5s 000ms", issueOpen: false).Action);

    [Fact]
    public void Counts_a_run_one_millisecond_over_the_limit_as_over_it() =>
        Assert.Equal(IssueAction.Open, Decide("5s 001ms", issueOpen: false).Action);

    [Fact]
    public void Compares_with_a_limit_that_has_decimals()
    {
        var limit = TimeSpan.FromSeconds(2.5);

        Assert.Equal(IssueAction.None, Decide("2s 500ms", issueOpen: false, limit).Action);
        Assert.Equal(IssueAction.Open, Decide("2s 501ms", issueOpen: false, limit).Action);
    }

    [Fact]
    public void Gives_text_for_every_action_except_none()
    {
        Assert.NotNull(Decide("7s 000ms", issueOpen: false).Text);
        Assert.NotNull(Decide("7s 000ms", issueOpen: true).Text);
        Assert.NotNull(Decide("1s 000ms", issueOpen: true).Text);
    }
}

[Unit]
public sealed class Reading_the_duration_from_the_console_output
{
    private static IssueAction ActionFor(string output, TimeSpan limit) =>
        TimingIssue.Decide([Samples.Report()], output, limit, issueOpen: false).Action;

    [Theory]
    [InlineData("312ms", 312)]
    [InlineData("4s 312ms", 4_312)]
    [InlineData("1m 05s 012ms", 65_012)]
    public void Counts_a_duration_equal_to_the_limit_as_not_over(string duration, int milliseconds) =>
        Assert.Equal(IssueAction.None, ActionFor(Samples.Output(duration), TimeSpan.FromMilliseconds(milliseconds)));

    [Theory]
    [InlineData("312ms", 312)]
    [InlineData("4s 312ms", 4_312)]
    [InlineData("1m 05s 012ms", 65_012)]
    public void Counts_a_duration_one_millisecond_above_the_limit_as_over(string duration, int milliseconds) =>
        Assert.Equal(IssueAction.Open, ActionFor(Samples.Output(duration), TimeSpan.FromMilliseconds(milliseconds - 1)));

    [Fact]
    public void Ignores_terminal_color_codes_around_and_inside_the_duration_line()
    {
        const string output = "Test run summary: Passed!\n\u001b[90m  total: 4\u001b[m\n  \u001b[90mduration:\u001b[m \u001b[1m7s 000ms\u001b[m\n";

        Assert.Equal(IssueAction.Open, ActionFor(output, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Reads_the_duration_when_the_output_uses_unix_line_endings() =>
        Assert.Equal(IssueAction.Open, ActionFor(Samples.Output("7s 000ms").Replace("\r\n", "\n"), TimeSpan.FromSeconds(5)));
}

[Unit]
public sealed class Writing_the_issue_text
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2.5);

    private static readonly string[] TwoProjects =
    [
        Samples.Report(
            Samples.TestCase("Case_Fast", "0.014000"),
            Samples.TestCase("Case_Slow", "1.212000", "Sample.Tests.Billing")),
        Samples.Report(Samples.TestCase("Case_Middle", "0.500000", "Sample.Tests.Shipping")),
    ];

    private static TimingDecision Decide(IReadOnlyList<string> reports, bool issueOpen, string duration = "3s 249ms") =>
        TimingIssue.Decide(reports, Samples.Output(duration), Limit, issueOpen);

    private static string[] Lines(string? text) =>
        Assert.IsType<string>(text).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    private static string RowOf(string? text, string shortName) =>
        Assert.Single(Lines(text), l => l.Contains(shortName, StringComparison.Ordinal));

    private static int PositionOf(string? text, string shortName) =>
        Array.FindIndex(Lines(text), l => l.Contains(shortName, StringComparison.Ordinal));

    [Fact]
    public void Gives_the_runs_duration_in_the_issue_text() =>
        Assert.Contains("3s 249ms", Decide(TwoProjects, issueOpen: false).Text);

    [Fact]
    public void Gives_the_limit_in_the_issue_text()
    {
        var text = Decide(TwoProjects, issueOpen: false).Text;

        Assert.True(text!.Contains("2.5", StringComparison.Ordinal) || text.Contains("2s 500ms", StringComparison.Ordinal), text);
    }

    [Fact]
    public void Lists_each_test_as_a_table_row_with_its_class_and_its_time_in_milliseconds()
    {
        var text = Decide(TwoProjects, issueOpen: false).Text;

        var row = RowOf(text, "Case_Slow");
        Assert.StartsWith("|", row);
        Assert.Contains("Sample.Tests.Billing", row);
        Assert.Contains("1212", row);
        Assert.Contains("14", RowOf(text, "Case_Fast"));
    }

    [Fact]
    public void Lists_the_slowest_test_first_across_all_reports()
    {
        var text = Decide(TwoProjects, issueOpen: false).Text;

        var slow = PositionOf(text, "Case_Slow");
        var middle = PositionOf(text, "Case_Middle");
        var fast = PositionOf(text, "Case_Fast");
        Assert.True(slow >= 0 && slow < middle && middle < fast, $"slow={slow} middle={middle} fast={fast}");
    }

    [Fact]
    public void Lists_tests_with_the_same_time_in_name_order()
    {
        string[] reports =
        [
            Samples.Report(
                Samples.TestCase("Case_Charlie", "0.250000"),
                Samples.TestCase("Case_Alpha", "0.250000"),
                Samples.TestCase("Case_Bravo", "0.250000")),
        ];

        var text = Decide(reports, issueOpen: false).Text;

        var alpha = PositionOf(text, "Case_Alpha");
        var bravo = PositionOf(text, "Case_Bravo");
        var charlie = PositionOf(text, "Case_Charlie");
        Assert.True(alpha >= 0 && alpha < bravo && bravo < charlie, $"alpha={alpha} bravo={bravo} charlie={charlie}");
    }

    [Fact]
    public void Lists_every_case_of_a_theory_separately()
    {
        string[] reports =
        [
            Samples.Report(
                Samples.TestCase("Themed(x: 1)", "0.121000"),
                Samples.TestCase("Themed(x: 2)", "0.000000")),
        ];

        var text = Decide(reports, issueOpen: false).Text;

        Assert.Contains("Themed(x: 1)", text);
        Assert.Contains("Themed(x: 2)", text);
    }

    [Fact]
    public void Gives_a_note_the_same_text_as_the_issue_would_have()
    {
        var issue = Decide(TwoProjects, issueOpen: false);
        var note = Decide(TwoProjects, issueOpen: true);

        Assert.Equal(IssueAction.Open, issue.Action);
        Assert.Equal(IssueAction.Note, note.Action);
        Assert.Equal(issue.Text, note.Text);
    }

    [Fact]
    public void Writes_a_run_with_no_test_cases_without_rows()
    {
        var text = Decide([Samples.Report()], issueOpen: false).Text;

        Assert.Contains("3s 249ms", text);
        Assert.DoesNotContain("Case_", text);
    }

    [Fact]
    public void Writes_one_line_with_the_duration_and_the_limit_when_closing()
    {
        var decision = TimingIssue.Decide(TwoProjects, Samples.Output("1s 200ms"), Limit, issueOpen: true);

        Assert.Equal(IssueAction.Close, decision.Action);
        var text = Assert.IsType<string>(decision.Text).Trim();
        Assert.DoesNotContain('\n', text);
        Assert.Contains("1s 200ms", text);
        Assert.True(text.Contains("2.5", StringComparison.Ordinal) || text.Contains("2s 500ms", StringComparison.Ordinal), text);
    }
}

[Unit]
public sealed class Capping_the_issue_text_at_GitHubs_body_limit
{
    private const int GitHubBodyLimit = 65_536;
    private const int TestCount = 2_000;

    private static readonly string LongClassName = "Sample.Tests." + new string('X', 80);

    private static TimingDecision DecideWithManyTests(bool issueOpen)
    {
        // Case_0000 is the fastest and Case_1999 the slowest, so the slowest-first order is the reverse of the names.
        var cases = Enumerable.Range(0, TestCount)
            .Select(i => Samples.TestCase($"Case_{i:D4}", ((i + 1) / 1000.0).ToString("F6", CultureInfo.InvariantCulture), LongClassName))
            .ToArray();
        return TimingIssue.Decide([Samples.Report(cases)], Samples.Output("30s 000ms"), TimeSpan.FromSeconds(5), issueOpen);
    }

    private static List<int> IncludedIndexes(string text) =>
        text.Split('\n')
            .Select(l => (Line: l, At: l.IndexOf("Case_", StringComparison.Ordinal)))
            .Where(x => x.At >= 0)
            .Select(x => int.Parse(x.Line.AsSpan(x.At + "Case_".Length, 4), CultureInfo.InvariantCulture))
            .ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keeps_the_text_within_65536_characters(bool issueOpen)
    {
        var text = Assert.IsType<string>(DecideWithManyTests(issueOpen).Text);

        Assert.True(text.Length <= GitHubBodyLimit, $"The text has {text.Length} characters.");
    }

    [Fact]
    public void Keeps_the_slowest_tests_and_drops_the_fastest()
    {
        var included = IncludedIndexes(Assert.IsType<string>(DecideWithManyTests(issueOpen: false).Text));

        Assert.NotEmpty(included);
        Assert.True(included.Count < TestCount, "Nothing was dropped.");
        Assert.Equal(Enumerable.Range(TestCount - included.Count, included.Count).Reverse(), included);
    }

    [Fact]
    public void Fills_the_text_with_as_many_of_the_slowest_tests_as_fit()
    {
        var text = Assert.IsType<string>(DecideWithManyTests(issueOpen: false).Text);

        Assert.True(text.Length > GitHubBodyLimit - 1_000, $"The text has only {text.Length} characters.");
    }

    [Fact]
    public void Ends_by_saying_how_many_tests_were_left_out()
    {
        var text = Assert.IsType<string>(DecideWithManyTests(issueOpen: false).Text);
        var leftOut = TestCount - IncludedIndexes(text).Count;

        var lastLine = text.Split('\n').Select(l => l.Trim()).Last(l => l.Length > 0);

        Assert.Contains(leftOut.ToString(CultureInfo.InvariantCulture), lastLine);
    }
}

[Unit]
public sealed class Rejecting_invalid_input
{
    private static readonly string ValidReport = Samples.Report(Samples.TestCase("Case_A", "0.100000"));
    private static readonly string ValidOutput = Samples.Output("3s 249ms");
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    private static void AssertRejected(IReadOnlyList<string> reports, string output, TimeSpan? limit = null)
    {
        var failure = Record.Exception(() => TimingIssue.Decide(reports, output, limit ?? Limit, issueOpen: true));

        Assert.True(
            failure is ArgumentException or FormatException,
            $"Expected an ArgumentException or a FormatException, got: {failure?.GetType().Name ?? "no exception"}");
    }

    [Fact]
    public void Rejects_a_run_with_no_reports() =>
        AssertRejected([], ValidOutput);

    [Fact]
    public void Rejects_a_report_that_is_not_XML() =>
        AssertRejected(["this is not xml"], ValidOutput);

    [Fact]
    public void Rejects_XML_that_is_not_a_JUnit_report() =>
        AssertRejected(["<html><body><p>hello</p></body></html>"], ValidOutput);

    [Fact]
    public void Rejects_a_bad_report_among_good_ones() =>
        AssertRejected([ValidReport, "this is not xml"], ValidOutput);

    [Fact]
    public void Rejects_a_test_case_with_no_time() =>
        AssertRejected([Samples.ReportWithTestCaseAttributes("name=\"Sample.Case_A\" classname=\"Sample\"")], ValidOutput);

    [Fact]
    public void Rejects_a_test_case_time_that_is_not_a_number() =>
        AssertRejected([Samples.ReportWithTestCaseAttributes("name=\"Sample.Case_A\" time=\"fast\" classname=\"Sample\"")], ValidOutput);

    [Fact]
    public void Rejects_output_with_no_duration_line() =>
        AssertRejected([ValidReport], "Test run summary: Passed!\r\n  total: 4\r\n");

    [Fact]
    public void Rejects_output_with_two_duration_lines() =>
        AssertRejected([ValidReport], ValidOutput + "  duration: 1s 000ms\r\n");

    [Theory]
    [InlineData("soon")]
    [InlineData("")]
    [InlineData("4 seconds")]
    public void Rejects_a_duration_that_is_not_in_the_summarys_format(string duration) =>
        AssertRejected([ValidReport], Samples.Output(duration));

    [Fact]
    public void Rejects_a_limit_of_zero() =>
        AssertRejected([ValidReport], ValidOutput, TimeSpan.Zero);

    [Fact]
    public void Rejects_a_negative_limit() =>
        AssertRejected([ValidReport], ValidOutput, TimeSpan.FromSeconds(-1));
}
