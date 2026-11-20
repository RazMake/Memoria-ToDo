using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog;
using Serilog.Core;
using Serilog.Events;
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

        var done = StoreFailures.Run(() => calls++, StoreFailures.MessageOf, _reports.Add, Logger.None);

        Assert.True(done);
        Assert.Equal(1, calls);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_known_failure_is_reported_once_with_its_message_and_returns_false()
    {
        var done = StoreFailures.Run(
            () => throw new IOException("The disk is full."), StoreFailures.MessageOf, _reports.Add, Logger.None);

        Assert.False(done);
        Assert.Equal(["The disk is full."], _reports);
    }

    [Fact]
    public void A_missing_repository_folder_is_reported_with_the_platforms_message()
    {
        var done = StoreFailures.Run(
            () => throw new NoRepositoryFolderException(), StoreFailures.MessageOf, _reports.Add, Logger.None);

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
            _reports.Add,
            Logger.None);

        Assert.False(done);
        Assert.Same(failure, asked);
        Assert.Equal(["This task no longer exists."], _reports);
    }

    [Fact]
    public void A_failure_the_function_gives_no_message_for_passes_on_unchanged_and_reports_nothing()
    {
        var failure = new NotSupportedException("Unexpected.");

        var thrown = Assert.Throws<NotSupportedException>(
            () => StoreFailures.Run(() => throw failure, StoreFailures.MessageOf, _reports.Add, Logger.None));

        Assert.Same(failure, thrown);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_failure_the_function_gives_no_message_for_passes_on_even_when_the_platform_has_one()
    {
        var failure = new IOException("The disk is full.");

        var thrown = Assert.Throws<IOException>(() => StoreFailures.Run(() => throw failure, _ => null, _reports.Add, Logger.None));

        Assert.Same(failure, thrown);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Rejects_a_missing_argument_and_runs_no_call()
    {
        var calls = 0;

        Assert.Equal("call", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(null!, StoreFailures.MessageOf, _reports.Add, Logger.None)).ParamName);
        Assert.Equal("messageOf", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(() => calls++, null!, _reports.Add, Logger.None)).ParamName);
        Assert.Equal("report", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(() => calls++, StoreFailures.MessageOf, null!, Logger.None)).ParamName);
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(
            () => StoreFailures.Run(() => calls++, StoreFailures.MessageOf, _reports.Add, null!)).ParamName);
        Assert.Equal(0, calls);
    }
}

/// <summary>What running a store call logs: the failure it turns into a message, once, at Error, so no window needs to.</summary>
[Unit]
public class StoreFailuresLoggingTests
{
    private readonly InMemoryLogSink _logs = new();
    private readonly List<string> _reports = [];

    [Fact]
    public void A_failure_it_reports_is_logged_once_at_Error_with_its_exception()
    {
        var failure = new IOException("The disk is full.");

        StoreFailures.Run(() => throw failure, StoreFailures.MessageOf, _reports.Add, _logs.CreateLogger());

        var entry = Assert.Single(_logs.Events);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.True(entry.HasException(failure));
    }

    [Fact]
    public void A_failure_a_window_words_itself_is_logged_too_since_the_run_reports_it()
    {
        var failure = new TaskNotFoundException();

        StoreFailures.Run(() => throw failure, _ => "This task no longer exists.", _reports.Add, _logs.CreateLogger());

        Assert.True(Assert.Single(_logs.Events, e => e.Level == LogEventLevel.Error).HasException(failure));
    }

    [Fact]
    public void A_call_that_succeeds_logs_nothing()
    {
        StoreFailures.Run(() => { }, StoreFailures.MessageOf, _reports.Add, _logs.CreateLogger());

        Assert.Empty(_logs.Events);
    }

    [Fact]
    public void A_failure_that_passes_on_is_not_logged_since_whoever_ends_it_logs_it()
    {
        Assert.Throws<NotSupportedException>(
            () => StoreFailures.Run(() => throw new NotSupportedException("Unexpected."), StoreFailures.MessageOf, _reports.Add, _logs.CreateLogger()));

        Assert.Empty(_logs.Events);
    }
}
