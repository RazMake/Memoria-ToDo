namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>Decides what to do with the timing issue after a unit run.</summary>
public static class TimingIssue
{
    /// <summary>
    /// Decides the issue action from the reports' XML text, the console output, the limit and the issue state.
    /// </summary>
    /// <exception cref="ArgumentException">An input is invalid.</exception>
    /// <exception cref="FormatException">A report or the output can't be read.</exception>
    public static TimingDecision Decide(IReadOnlyList<string> junitReports, string testOutput, TimeSpan limit, bool issueOpen)
    {
        ArgumentNullException.ThrowIfNull(junitReports);
        ArgumentNullException.ThrowIfNull(testOutput);
        if (junitReports.Count == 0)
        {
            throw new ArgumentException("There is no JUnit report.");
        }

        if (limit <= TimeSpan.Zero)
        {
            throw new ArgumentException("The limit must be positive.");
        }

        var tests = new List<TestTime>();
        foreach (var report in junitReports)
        {
            JUnitReportReader.ReadInto(report, tests);
        }

        var (duration, value) = RunDurationReader.Read(testOutput);
        if (value > limit)
        {
            return new TimingDecision(
                issueOpen ? IssueAction.Note : IssueAction.Open,
                IssueTextWriter.Slow(duration, limit, tests));
        }

        return issueOpen
            ? new TimingDecision(IssueAction.Close, IssueTextWriter.Closing(duration, limit))
            : new TimingDecision(IssueAction.None, null);
    }
}
