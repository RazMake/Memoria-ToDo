using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using System.IO;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>Reading the log into the model: the loading state, the order, the empty log, and a failed read. A fake source stands in for the files.</summary>
[Unit]
public class LogsModelLoadingTests
{
    private readonly LogsServices _services = new();

    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string[] Messages(LogsModel model) => [.. model.Rows.Select(row => row.Message)];

    [Fact]
    public void Reads_nothing_until_it_is_asked_to_load()
    {
        var model = _services.With(LogsServices.Entry("2026-03-14 14:00")).Model();

        Assert.Equal(0, _services.Source.ReadCount);
        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task Says_Loading_in_the_list_while_it_reads_and_shows_the_entries_when_done()
    {
        _services.With(LogsServices.Entry("2026-03-14 14:00", message: "Started"));
        var release = _services.Source.Hold();
        var model = _services.Model();

        var load = model.LoadAsync(Cancel);

        Assert.True(model.IsLoading);
        Assert.Equal("Loading…", model.ListMessage);
        Assert.Empty(model.Rows);

        release();
        await load;

        Assert.False(model.IsLoading);
        Assert.Null(model.ListMessage);
        Assert.Equal(["Started"], Messages(model));
    }

    [Fact]
    public void Reads_the_source_on_another_thread_than_the_caller()
    {
        var model = _services.Model();
        var callerThread = 0;
        var load = Task.CompletedTask;

        // A dedicated caller thread, kept alive until the load ends, can never be the pool thread that does the read.
        var caller = new Thread(() =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            load = model.LoadAsync(Cancel);
            load.GetAwaiter().GetResult();
        });
        caller.Start();
        caller.Join();

        Assert.True(load.IsCompletedSuccessfully);
        Assert.Equal(1, _services.Source.ReadCount);
        Assert.DoesNotContain(callerThread, _services.Source.ReadThreads);
    }

    [Fact]
    public async Task Lists_the_entries_newest_first_whatever_order_the_source_gives()
    {
        _services.With(
            LogsServices.Entry("2026-03-14 10:00", message: "Middle"),
            LogsServices.Entry("2026-03-14 09:00", message: "Oldest"),
            LogsServices.Entry("2026-03-14 11:00", message: "Newest"));

        var model = await _services.LoadedAsync(Cancel);

        Assert.Equal(["Newest", "Middle", "Oldest"], Messages(model));
    }

    [Fact]
    public async Task Says_there_are_no_log_entries_yet_when_the_log_holds_none()
    {
        var model = await _services.LoadedAsync(Cancel);

        Assert.Empty(model.Rows);
        Assert.Equal("No log entries yet.", model.ListMessage);
    }

    [Fact]
    public async Task Shows_an_empty_list_and_says_why_in_the_status_line_when_the_folder_cannot_be_listed()
    {
        _services.Source.Failure = new IOException("The log folder is locked away");

        var model = await _services.LoadedAsync(Cancel);

        Assert.Empty(model.Rows);
        Assert.False(model.IsLoading);
        Assert.Contains("The log folder is locked away", model.StatusText);
    }

    [Fact]
    public async Task Logs_an_Error_with_the_exception_when_the_folder_cannot_be_listed()
    {
        var failure = new IOException("The log folder is locked away");
        _services.Source.Failure = failure;

        await _services.LoadedAsync(Cancel);

        Assert.True(_services.Sink.Events.SingleAt(LogEventLevel.Error).HasException(failure));
    }

    [Fact]
    public async Task Shows_the_entries_when_a_later_load_succeeds_after_a_failed_one()
    {
        _services.Source.Failure = new IOException("Locked");
        var model = await _services.LoadedAsync(Cancel);
        _services.Source.Failure = null;
        _services.With(LogsServices.Entry("2026-03-14 14:00", message: "Back"));

        await model.LoadAsync(Cancel);

        Assert.Equal(["Back"], Messages(model));
        Assert.DoesNotContain("Locked", model.StatusText);
    }
}
