using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The logger the composition root makes: what it keeps, the component each module's logger carries, and how it writes
/// through a queue that never holds up a caller. The writer is a scripted stand-in for the log file, so no test
/// touches a disk.
/// </summary>
[Unit]
public sealed class LogPipelineTests
{
    private const int QueueSize = 10_000;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private readonly ScriptedLogDestination _destination = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private LogPipeline Pipeline() => LogPipeline.Create(_destination, _time);

    [Fact]
    public void Refuses_to_be_made_without_a_writer_or_a_clock()
    {
        Assert.Throws<ArgumentNullException>(() => LogPipeline.Create(null!, _time));
        Assert.Throws<ArgumentNullException>(() => LogPipeline.Create(_destination, null!));
    }

    [Fact]
    public void Writes_Information_Warning_Error_and_Fatal_entries()
    {
        using (var pipeline = Pipeline())
        {
            var log = pipeline.ForModule("App");
            log.Information("An operation finished");
            log.Warning("Something was skipped");
            log.Error("An operation failed");
            log.Fatal("The app ends");
        }

        Assert.Equal(
            [LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal],
            _destination.Written.Select(e => e.Level));
    }

    [Fact]
    public void Writes_Debug_entries_and_drops_Verbose_ones_before_they_are_written()
    {
        using (var pipeline = Pipeline())
        {
            var log = pipeline.ForModule("App");
            log.Verbose("A detail");
            log.Debug("A choice");
            log.Information("Kept");
        }

        Assert.Equal(["A choice", "Kept"], _destination.Written.Select(e => e.RenderMessage()));
        Assert.Equal(LogEventLevel.Debug, _destination.Written[0].Level);
    }

    [Fact]
    public void Gives_each_modules_logger_its_module_name_as_the_entrys_component()
    {
        using (var pipeline = Pipeline())
        {
            pipeline.ForModule("Platform").Information("From the platform");
            pipeline.ForModule("Task list").Information("From the task list");
            pipeline.ForModule("Logs").Information("From the logs window");
        }

        Assert.Equal(
            ["Platform", "Task list", "Logs"],
            _destination.Written.Select(e => (string?)e.Property("SourceContext")));
    }

    [Fact]
    public void Keeps_the_values_an_entry_names_so_the_file_can_hold_them_as_fields()
    {
        using (var pipeline = Pipeline())
        {
            pipeline.ForModule("Tasks").Information("Added task {TaskId}", "abc-123");
        }

        Assert.Equal("abc-123", Assert.Single(_destination.Written).Property("TaskId"));
    }

    [Fact]
    public void Writes_entries_in_the_order_they_were_logged()
    {
        using (var pipeline = Pipeline())
        {
            var log = pipeline.ForModule("App");
            for (var number = 0; number < 500; number++)
            {
                log.Information("Entry {Number}", number);
            }
        }

        Assert.Equal(Enumerable.Range(0, 500), _destination.Written.Select(e => (int)e.Property("Number")!));
    }

    [Fact]
    public void Writes_every_entry_still_queued_when_it_is_disposed()
    {
        using (var pipeline = Pipeline())
        {
            var log = pipeline.ForModule("App");
            for (var number = 0; number < 2_000; number++)
            {
                log.Information("Entry {Number}", number);
            }
        }

        Assert.Equal(2_000, _destination.Written.Count);
    }

    [Fact]
    public async Task Stops_waiting_when_disposed_while_the_writer_is_stuck_so_the_app_can_end()
    {
        var pipeline = Pipeline();
        var log = pipeline.ForModule("App");
        _destination.Hold();
        log.Information("Stuck in the writer");
        _destination.WaitForAttempts(1);
        log.Information("Waiting in the queue");

        var disposing = Task.Run(pipeline.Dispose, TestContext.Current.CancellationToken);
        try
        {
            await _time.WaitForActiveTimerAsync();
            _time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(1));
            Assert.Equal(1, _time.ActiveTimerCount);
            Assert.False(disposing.IsCompleted, "Dispose stopped waiting before the two-second limit.");

            _time.Advance(TimeSpan.FromMilliseconds(1));
            await disposing.WaitAsync(Limit, TestContext.Current.CancellationToken);
        }
        finally
        {
            _destination.Release();
        }
    }

    [Fact]
    public async Task Never_makes_a_caller_wait_when_the_writer_is_stuck_and_the_queue_is_full()
    {
        using var pipeline = Pipeline();
        var log = pipeline.ForModule("App");
        _destination.Hold();
        log.Information("Stuck in the writer");
        _destination.WaitForAttempts(1);

        var logging = Task.Run(() =>
        {
            for (var number = 0; number < QueueSize + 500; number++)
            {
                log.Information("Entry {Number}", number);
            }
        }, TestContext.Current.CancellationToken);
        try
        {
            var first = await Task.WhenAny(logging, Task.Delay(Limit, TestContext.Current.CancellationToken));
            Assert.True(first == logging, "A caller waited for the full queue.");
        }
        finally
        {
            _destination.Release();
        }
    }

    [Fact]
    public void Holds_ten_thousand_entries_then_drops_the_rest_and_says_how_many_it_lost_in_a_Warning_for_the_app()
    {
        const int Sent = QueueSize + 500;
        var pipeline = Pipeline();
        var log = pipeline.ForModule("Tasks");
        _destination.Hold();
        log.Information("Stuck in the writer");
        _destination.WaitForAttempts(1);
        for (var number = 0; number < Sent; number++)
        {
            log.Information("Entry {Number}", number);
        }

        _destination.Release();
        _destination.WaitForWritten(1_000); // The queue has room again.
        pipeline.ForModule("Tasks").Information("After the loss");
        pipeline.Dispose();

        var written = _destination.Written;
        var entries = written.Where(e => e.Level == LogEventLevel.Information).ToList();
        var lost = (1 + Sent + 1) - entries.Count;
        Assert.True(entries.Count >= QueueSize, $"Only {entries.Count} entries were kept.");
        Assert.True(lost > 0, "The full queue dropped nothing.");
        var warning = written.SingleAt(LogEventLevel.Warning, lost.ToString());
        Assert.Equal("App", warning.Property("SourceContext"));
        Assert.True(
            written.ToList().IndexOf(warning) < written.ToList().FindIndex(e => e.Names("After the loss")),
            "The Warning came after a later entry instead of right after the first one written once the loss ended.");
    }

    [Fact]
    public void Has_no_write_problem_while_writing_works()
    {
        using var pipeline = Pipeline();

        pipeline.ForModule("App").Information("Fine");

        Assert.Null(pipeline.WriteProblem);
    }

    [Fact]
    public void Never_throws_to_the_caller_when_the_writer_fails()
    {
        using var pipeline = Pipeline();
        _destination.FailWith(new IOException("disk full boom"));

        pipeline.ForModule("App").Information("Cannot be written");
        pipeline.ForModule("App").Error(new InvalidOperationException("Also lost"), "Cannot be written either");
    }

    [Fact]
    public void Shows_the_reason_as_the_write_problem_while_writing_fails()
    {
        using var pipeline = Pipeline();
        _destination.FailWith(new IOException("disk full boom"));
        var log = pipeline.ForModule("App");

        for (var number = 0; number < 3; number++)
        {
            log.Information("Entry {Number}", number);
        }

        _destination.WaitForAttempts(3); // The third write starts only after the second one's failure was dealt with.
        Assert.Contains("disk full boom", pipeline.WriteProblem);
    }

    [Fact]
    public void Writes_a_Warning_with_the_lost_count_since_when_and_why_after_writing_works_again_then_clears_the_problem()
    {
        const int Lost = 47;
        var pipeline = Pipeline();
        _destination.FailNext(new IOException("disk full boom"), Lost);
        var log = pipeline.ForModule("App");
        for (var number = 0; number < Lost; number++)
        {
            log.Information("Lost entry {Number}", number);
        }

        _destination.WaitForAttempts(Lost); // The first failure, which starts the count, was dealt with by now.
        _time.Advance(TimeSpan.FromMinutes(10));
        log.Information("The first entry after the problem");
        _destination.WaitForWritten(2); // The entry and the Warning.
        pipeline.Dispose();

        var written = _destination.Written;
        Assert.DoesNotContain(written, e => e.Names("Lost entry"));
        var warning = written.SingleAt(LogEventLevel.Warning, Lost.ToString(), "disk full boom");
        Assert.Equal("App", warning.Property("SourceContext"));
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Assert.True(
            warning.Properties.Values.Any(v =>
                v is ScalarValue { Value: DateTimeOffset since } && since == start
                || v is ScalarValue { Value: DateTime day } && day.ToUniversalTime() == start.UtcDateTime),
            "The Warning doesn't carry when writing started to fail.");
        Assert.Null(pipeline.WriteProblem);
    }

    [Fact]
    public void Writes_the_lost_entries_Warning_once()
    {
        var pipeline = Pipeline();
        _destination.FailNext(new IOException("disk full boom"), 1);
        var log = pipeline.ForModule("App");
        log.Information("Lost");
        log.Information("Written after");
        log.Information("Written later");
        _destination.WaitForWritten(3); // The two entries and the Warning.
        pipeline.Dispose();

        Assert.Single(_destination.Written, e => e.Level == LogEventLevel.Warning);
    }
}
