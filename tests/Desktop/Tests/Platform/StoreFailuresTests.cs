using System.IO;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The messages for the failures every store call can throw, which every window that shows them shares.</summary>
[Unit]
public class StoreFailuresMessageTests
{
    [Fact]
    public void Tells_the_user_to_choose_a_repository_folder_when_there_is_none()
    {
        Assert.Equal(
            "There is no repository folder yet. Open Configure and choose one.",
            StoreFailures.MessageOf(new NoRepositoryFolderException()));
    }

    [Fact]
    public void Gives_a_file_a_denied_file_unreadable_data_and_a_rejected_argument_the_failures_own_message()
    {
        Exception[] failures =
        [
            new IOException("The disk is full."),
            new UnauthorizedAccessException("Access denied."),
            new InvalidDataException("Bad file."),
            new ArgumentException("An empty name."),
        ];

        Assert.Equal(
            ["The disk is full.", "Access denied.", "Bad file.", "An empty name."],
            failures.Select(StoreFailures.MessageOf));
    }

    [Fact]
    public void Gives_a_more_specific_file_or_argument_failure_its_own_message_too()
    {
        var nullArgument = new ArgumentNullException("name", "Name is required.");

        Assert.Equal("Missing.", StoreFailures.MessageOf(new FileNotFoundException("Missing.")));
        Assert.Equal(nullArgument.Message, StoreFailures.MessageOf(nullArgument));
    }

    [Fact]
    public void Knows_no_message_for_a_failure_only_one_store_throws_so_that_its_window_words_it()
    {
        Assert.Null(StoreFailures.MessageOf(new TaskNotFoundException()));
    }

    [Fact]
    public void Knows_no_message_for_any_other_failure_so_that_it_is_not_swallowed()
    {
        Assert.Null(StoreFailures.MessageOf(new NotSupportedException("Unexpected.")));
        Assert.Null(StoreFailures.MessageOf(new AggregateException()));
    }
}

/// <summary>Running a store call so that a known failure is reported with its message and any other failure passes on.</summary>
[Unit]
public class StoreFailuresRunTests
{
    private readonly List<string> _reports = [];

    [Fact]
    public void A_call_that_succeeds_returns_true_and_reports_nothing()
    {
        var calls = 0;

        var done = StoreFailures.Run(() => calls++, StoreFailures.MessageOf, _reports.Add);

        Assert.True(done);
        Assert.Equal(1, calls);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_known_failure_is_reported_once_with_its_message_and_returns_false()
    {
        var done = StoreFailures.Run(
            () => throw new IOException("The disk is full."), StoreFailures.MessageOf, _reports.Add);

        Assert.False(done);
        Assert.Equal(["The disk is full."], _reports);
    }

    [Fact]
    public void A_missing_repository_folder_is_reported_with_the_platforms_message()
    {
        var done = StoreFailures.Run(
            () => throw new NoRepositoryFolderException(), StoreFailures.MessageOf, _reports.Add);

        Assert.False(done);
        Assert.Equal(["There is no repository folder yet. Open Configure and choose one."], _reports);
    }

    [Fact]
    public void A_failure_the_given_function_words_is_reported_with_that_words()
    {
        var failure = new TaskNotFoundException();
        Exception? asked = null;

        var done = StoreFailures.Run(
            () => throw failure,
            thrown =>
            {
                asked = thrown;
                return thrown is TaskNotFoundException ? "This task no longer exists." : StoreFailures.MessageOf(thrown);
            },
            _reports.Add);

        Assert.False(done);
        Assert.Same(failure, asked);
        Assert.Equal(["This task no longer exists."], _reports);
    }

    [Fact]
    public void A_failure_the_function_gives_no_message_for_passes_on_unchanged_and_reports_nothing()
    {
        var failure = new NotSupportedException("Unexpected.");

        var thrown = Assert.Throws<NotSupportedException>(
            () => StoreFailures.Run(() => throw failure, StoreFailures.MessageOf, _reports.Add));

        Assert.Same(failure, thrown);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_failure_the_function_gives_no_message_for_passes_on_even_when_the_platform_has_one()
    {
        var failure = new IOException("The disk is full.");

        var thrown = Assert.Throws<IOException>(() => StoreFailures.Run(() => throw failure, _ => null, _reports.Add));

        Assert.Same(failure, thrown);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Rejects_a_missing_argument_and_runs_no_call()
    {
        var calls = 0;

        Assert.Equal("call", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(null!, StoreFailures.MessageOf, _reports.Add)).ParamName);
        Assert.Equal("messageOf", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(() => calls++, null!, _reports.Add)).ParamName);
        Assert.Equal("report", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(() => calls++, StoreFailures.MessageOf, null!)).ParamName);
        Assert.Equal(0, calls);
    }
}
