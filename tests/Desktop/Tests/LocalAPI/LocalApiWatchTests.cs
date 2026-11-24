// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Grpc.Core;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// WatchTasks and WatchContacts: an empty notice at once, one after each change, and one pending notice, never a queue, for a reader
/// that falls behind. Each test drives the service method through the call interceptor with a fake stream writer.
/// </summary>
[Unit]
public class LocalApiWatchTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static void ChangeContacts(ApiHarness api, int count)
    {
        for (var i = 0; i < count; i++)
            api.Contacts.Save("team", new Contact("n" + i, "New " + i));
    }

    [Fact]
    public async Task WatchTasks_sends_a_notice_at_once()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var close = new CancellationTokenSource();
        var watching = api.Tasks.WatchAsync(writer, close.Token);

        var first = await writer.NextAsync();

        Assert.Equal(new V1.ChangeNotice(), first);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task WatchTasks_sends_one_notice_after_each_change()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var close = new CancellationTokenSource();
        var watching = api.Tasks.WatchAsync(writer, close.Token);
        await writer.NextAsync();

        api.Store.RaiseChanged();
        await writer.NextAsync();
        api.Store.RaiseChanged();
        await writer.NextAsync();

        Assert.Equal(3, writer.Entered);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task A_WatchTasks_reader_that_falls_behind_gets_one_pending_notice_not_a_queue()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>(holdWrites: true);
        using var close = new CancellationTokenSource();
        var watching = api.Tasks.WatchAsync(writer, close.Token);
        await writer.EnteredAsync();

        for (var i = 0; i < 5; i++)
            api.Store.RaiseChanged();
        writer.ReleaseWrite();
        await writer.EnteredAsync();
        writer.ReleaseWrite();

        await writer.NextAsync();
        await writer.NextAsync();
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
        Assert.Equal(2, writer.Entered);
    }

    [Fact]
    public async Task A_stream_logs_when_it_opens_and_when_it_closes_never_once_per_notice()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var close = new CancellationTokenSource();
        var watching = api.Tasks.WatchAsync(writer, close.Token);
        await writer.NextAsync();
        await api.Log.EntryAsync(LogEventLevel.Information, "WatchTasks");
        for (var i = 0; i < 3; i++)
        {
            api.Store.RaiseChanged();
            await writer.NextAsync();
        }

        Assert.Single(api.Log.Events, e => e.Names("WatchTasks"));
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);

        var entries = api.Log.Events.Where(e => e.Names("WatchTasks")).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(LogEventLevel.Information, entry.Level));
    }

    [Fact]
    public async Task Stopping_the_API_ends_an_open_WatchTasks_stream()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var never = new CancellationTokenSource();
        var watching = api.Tasks.WatchAsync(writer, never.Token);
        await writer.NextAsync();

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task A_WatchTasks_stream_opened_after_Stop_gets_UNAVAILABLE_and_no_notice()
    {
        await using var api = new ApiHarness();
        await api.Lifetime.StopAsync().WaitAsync(Bound);
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();

        var failure = await CallOutcome.FailsAsync(api.Tasks.WatchAsync(writer, CancellationToken.None));

        Assert.Equal(StatusCode.Unavailable, failure.StatusCode);
        Assert.Equal(0, writer.Entered);
    }

    [Fact]
    public async Task WatchContacts_sends_a_notice_at_once()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var close = new CancellationTokenSource();
        var watching = api.ContactCalls.WatchAsync(writer, close.Token);

        var first = await writer.NextAsync();

        Assert.Equal(new V1.ChangeNotice(), first);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task WatchContacts_sends_one_notice_after_each_change()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var close = new CancellationTokenSource();
        var watching = api.ContactCalls.WatchAsync(writer, close.Token);
        await writer.NextAsync();

        ChangeContacts(api, 1);
        await writer.NextAsync();
        await api.ContactCalls.DeleteContactAsync("team", "2");
        await writer.NextAsync();

        Assert.Equal(3, writer.Entered);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task A_WatchContacts_reader_that_falls_behind_gets_one_pending_notice_not_a_queue()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>(holdWrites: true);
        using var close = new CancellationTokenSource();
        var watching = api.ContactCalls.WatchAsync(writer, close.Token);
        await writer.EnteredAsync();

        ChangeContacts(api, 5);
        writer.ReleaseWrite();
        await writer.EnteredAsync();
        writer.ReleaseWrite();

        await writer.NextAsync();
        await writer.NextAsync();
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
        Assert.Equal(2, writer.Entered);
    }

    [Fact]
    public async Task Stopping_the_API_ends_an_open_WatchContacts_stream()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var writer = new RecordingStreamWriter<V1.ChangeNotice>();
        using var never = new CancellationTokenSource();
        var watching = api.ContactCalls.WatchAsync(writer, never.Token);
        await writer.NextAsync();

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        await CallOutcome.EndedAsync(watching);
    }
}
