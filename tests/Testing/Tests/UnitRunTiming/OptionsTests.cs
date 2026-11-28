using Xunit;

namespace Memoria.ToDo.Testing.UnitRunTiming;

[Unit]
public sealed class Reading_the_command_line
{
    // The folder needn't exist: reading the arguments touches no disk.
    private const string TextFile = "no-such-folder/issue.md";

    private static List<string> Valid(string limitSeconds = "2.5", string issueOpen = "true") =>
    [
        "--reports", "results",
        "--test-output", "output.txt",
        "--limit-seconds", limitSeconds,
        "--issue-open", issueOpen,
        "--text-file", TextFile,
    ];

    private static void AssertRejectedNaming(IEnumerable<string> arguments, string argumentName)
    {
        var failure = Assert.Throws<ArgumentException>(() => Options.Parse([.. arguments]));

        Assert.Contains(argumentName, failure.Message);
    }

    [Fact]
    public void Reads_the_five_arguments()
    {
        var options = Options.Parse([.. Valid()]);

        Assert.Equal("results", options.ReportsFolder);
        Assert.Equal("output.txt", options.TestOutputFile);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), options.Limit);
        Assert.True(options.IssueOpen);
        Assert.Equal(TextFile, options.TextFile);
    }

    [Fact]
    public void Reads_the_arguments_in_any_order()
    {
        var reversed = Valid().Chunk(2).Reverse().SelectMany(pair => pair);

        var options = Options.Parse([.. reversed]);

        Assert.Equal("results", options.ReportsFolder);
        Assert.Equal(TextFile, options.TextFile);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Reads_whether_the_issue_is_open(string value, bool expected) =>
        Assert.Equal(expected, Options.Parse([.. Valid(issueOpen: value)]).IssueOpen);

    [Theory]
    [InlineData("--reports")]
    [InlineData("--test-output")]
    [InlineData("--limit-seconds")]
    [InlineData("--issue-open")]
    [InlineData("--text-file")]
    public void Rejects_a_missing_argument(string missing)
    {
        var arguments = Valid();
        arguments.RemoveRange(arguments.IndexOf(missing), 2);

        AssertRejectedNaming(arguments, missing);
    }

    [Fact]
    public void Rejects_a_repeated_argument() =>
        AssertRejectedNaming([.. Valid(), "--limit-seconds", "3"], "--limit-seconds");

    [Fact]
    public void Rejects_an_unknown_argument() =>
        AssertRejectedNaming([.. Valid(), "--verbose", "yes"], "--verbose");

    [Fact]
    public void Rejects_an_argument_with_no_value() =>
        AssertRejectedNaming([.. Valid(), "--reports"], "--reports");

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("fast")]
    [InlineData("2,5")]
    public void Rejects_a_limit_that_is_not_a_positive_number(string limit) =>
        AssertRejectedNaming(Valid(limitSeconds: limit), "--limit-seconds");

    [Theory]
    [InlineData("yes")]
    [InlineData("True ")]
    [InlineData("")]
    public void Rejects_an_issue_open_value_that_is_neither_true_nor_false(string value) =>
        AssertRejectedNaming(Valid(issueOpen: value), "--issue-open");
}
