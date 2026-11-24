// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>What the Sync calls answer: the status, Sync now, and the stream of status changes.</summary>
[Unit]
public class LocalApiSyncTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset LastSync = new(2026, 11, 23, 9, 30, 0, TimeSpan.Zero);

    public static readonly TheoryData<SyncState, V1.SyncState> States = new()
    {
        { SyncState.Idle, V1.SyncState.Idle },
        { SyncState.Working, V1.SyncState.Working },
        { SyncState.Offline, V1.SyncState.Offline },
        { SyncState.Error, V1.SyncState.Error },
    };

    public static readonly TheoryData<SyncFailure, V1.SyncFailure> Failures = new()
    {
        { SyncFailure.Connection, V1.SyncFailure.Connection },
        { SyncFailure.Authentication, V1.SyncFailure.Authentication },
        { SyncFailure.UnknownHostKey, V1.SyncFailure.UnknownHostKey },
        { SyncFailure.ChangedHostKey, V1.SyncFailure.ChangedHostKey },
        { SyncFailure.Repository, V1.SyncFailure.Repository },
        { SyncFailure.PushRejected, V1.SyncFailure.PushRejected },
        { SyncFailure.InvalidFile, V1.SyncFailure.InvalidFile },
        { SyncFailure.LocalStorage, V1.SyncFailure.LocalStorage },
        { SyncFailure.Unexpected, V1.SyncFailure.Unexpected },
    };

    [Fact]
    public async Task GetStatus_returns_every_part_of_the_current_status()
    {
        await using var api = new ApiHarness();
        api.Sync.Publish(new FakeSyncStatus
        {
            State = SyncState.Error,
            Failure = SyncFailure.InvalidFile,
            Details = "One file couldn't be read.",
            LastSuccessfulSync = LastSync,
            HasPendingChanges = true,
            SkippedFiles = [new FakeSkippedFile("autocomplete/contacts/broken.json", "Not JSON.", RequiresUpdate: true)],
        });

        var status = await api.SyncCalls.GetStatusAsync();

        Assert.Equal(V1.SyncState.Error, status.State);
        Assert.Equal(V1.SyncFailure.InvalidFile, status.Failure);
        Assert.Equal("One file couldn't be read.", status.Details);
        Assert.Equal(Timestamp.FromDateTimeOffset(LastSync), status.LastSuccessfulSync);
        Assert.True(status.HasPendingChanges);
        var skipped = Assert.Single(status.SkippedFiles);
        Assert.Equal("autocomplete/contacts/broken.json", skipped.RelativePath);
        Assert.Equal("Not JSON.", skipped.Reason);
        Assert.True(skipped.RequiresUpdate);
    }

    [Fact]
    public async Task GetStatus_leaves_out_what_the_status_does_not_have()
    {
        await using var api = new ApiHarness();
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Idle });

        var status = await api.SyncCalls.GetStatusAsync();

        Assert.Equal(V1.SyncFailure.Unspecified, status.Failure);
        Assert.Equal("", status.Details);
        Assert.Null(status.LastSuccessfulSync);
        Assert.False(status.HasPendingChanges);
        Assert.Empty(status.SkippedFiles);
    }

    [Theory]
    [MemberData(nameof(States))]
    public async Task GetStatus_names_each_state(SyncState state, V1.SyncState expected)
    {
        await using var api = new ApiHarness();
        api.Sync.Publish(new FakeSyncStatus { State = state });

        var status = await api.SyncCalls.GetStatusAsync();

        Assert.Equal(expected, status.State);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task GetStatus_names_each_failure(SyncFailure failure, V1.SyncFailure expected)
    {
        await using var api = new ApiHarness();
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = failure });

        var status = await api.SyncCalls.GetStatusAsync();

        Assert.Equal(expected, status.Failure);
    }

    [Fact]
    public async Task SyncNow_waits_for_the_sync_pass_and_returns_its_status()
    {
        await using var api = new ApiHarness();
        TaskCompletionSource<ISyncStatus> pass = new(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Sync.SyncOperation = _ => pass.Task;

        var call = api.SyncCalls.SyncNowAsync();

        Assert.False(call.IsCompleted);
        pass.SetResult(new FakeSyncStatus { State = SyncState.Idle, LastSuccessfulSync = LastSync });
        var status = await call.WaitAsync(Bound);
        Assert.Equal(V1.SyncState.Idle, status.State);
        Assert.Equal(Timestamp.FromDateTimeOffset(LastSync), status.LastSuccessfulSync);
        Assert.Equal(1, api.Sync.SyncCount);
    }

    [Fact]
    public async Task SyncNow_returns_a_failed_pass_as_a_reply_not_a_failure()
    {
        await using var api = new ApiHarness();
        api.Sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus { State = SyncState.Offline, Failure = SyncFailure.Connection });

        var status = await api.SyncCalls.SyncNowAsync();

        Assert.Equal(V1.SyncState.Offline, status.State);
        Assert.Equal(V1.SyncFailure.Connection, status.Failure);
    }

    [Fact]
    public async Task Cancelling_SyncNow_stops_only_the_callers_wait()
    {
        await using var api = new ApiHarness();
        TaskCompletionSource<ISyncStatus> pass = new(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Sync.SyncOperation = _ => pass.Task;
        using var cancel = new CancellationTokenSource();
        var cancelled = api.SyncCalls.SyncNowAsync(cancel.Token);

        await cancel.CancelAsync();

        var failure = await CallOutcome.FailsAsync(cancelled);
        Assert.Equal(StatusCode.Cancelled, failure.StatusCode);
        Assert.False(pass.Task.IsCompleted);
        Assert.Equal(0, api.Sync.StopCount);
        var again = api.SyncCalls.SyncNowAsync();
        pass.SetResult(new FakeSyncStatus { State = SyncState.Idle });
        Assert.Equal(V1.SyncState.Idle, (await again.WaitAsync(Bound)).State);
    }

    [Fact]
    public async Task SyncNow_fails_with_FAILED_PRECONDITION_and_the_message_when_Sync_is_not_ready()
    {
        await using var api = new ApiHarness();
        api.Sync.NotReady = new SyncNotReadyException("No repository is selected.");

        var failure = await CallOutcome.FailsAsync(api.SyncCalls.SyncNowAsync());

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Contains("No repository is selected.", failure.Status.Detail);
    }

    [Fact]
    public async Task SyncNow_failing_with_a_plain_InvalidOperationException_with_a_not_ready_message_ends_with_INTERNAL()
    {
        await using var api = new ApiHarness();
        api.Sync.SyncOperation = _ => Task.FromException<ISyncStatus>(new InvalidOperationException("No repository is selected."));

        var failure = await CallOutcome.FailsAsync(api.SyncCalls.SyncNowAsync());

        Assert.Equal(StatusCode.Internal, failure.StatusCode);
    }

    [Fact]
    public async Task SyncNow_failing_with_an_unexpected_InvalidOperationException_ends_with_INTERNAL_and_logs_an_Error_with_it()
    {
        await using var api = new ApiHarness();
        var broken = new InvalidOperationException("The sync worker accepts one work item at a time.");
        api.Sync.SyncOperation = _ => Task.FromException<ISyncStatus>(broken);

        var failure = await CallOutcome.FailsAsync(api.SyncCalls.SyncNowAsync());

        Assert.Equal(StatusCode.Internal, failure.StatusCode);
        var entry = await api.Log.EntryAsync(LogEventLevel.Error, "SyncNow");
        Assert.True(entry.HasException(broken));
    }

    [Fact]
    public async Task A_SyncNow_call_logs_one_Information_entry_with_the_method_and_the_status()
    {
        await using var api = new ApiHarness();

        await api.SyncCalls.SyncNowAsync();

        var entry = await api.Log.EntryAsync(LogEventLevel.Information, "SyncNow");
        Assert.True(entry.NamesStatus(StatusCode.OK));
        Assert.Single(api.Log.Events, e => e.Names("SyncNow"));
    }

    [Fact]
    public async Task WatchStatus_sends_the_current_status_at_once()
    {
        await using var api = new ApiHarness();
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Offline, HasPendingChanges = true });
        var writer = new RecordingStreamWriter<V1.SyncStatus>();
        using var close = new CancellationTokenSource();
        var watching = api.SyncCalls.WatchAsync(writer, close.Token);

        var first = await writer.NextAsync();

        Assert.Equal(V1.SyncState.Offline, first.State);
        Assert.True(first.HasPendingChanges);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task WatchStatus_sends_each_new_status()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.SyncStatus>();
        using var close = new CancellationTokenSource();
        var watching = api.SyncCalls.WatchAsync(writer, close.Token);
        await writer.NextAsync();

        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Working });
        var working = await writer.NextAsync();
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Connection, Details = "No route." });
        var failed = await writer.NextAsync();

        Assert.Equal(V1.SyncState.Working, working.State);
        Assert.Equal(V1.SyncState.Error, failed.State);
        Assert.Equal(V1.SyncFailure.Connection, failed.Failure);
        Assert.Equal("No route.", failed.Details);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
    }

    [Fact]
    public async Task A_slow_WatchStatus_reader_gets_only_the_newest_status()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.SyncStatus>(holdWrites: true);
        using var close = new CancellationTokenSource();
        var watching = api.SyncCalls.WatchAsync(writer, close.Token);
        await writer.EnteredAsync();

        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Working });
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Offline });
        api.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.PushRejected });
        writer.ReleaseWrite();
        await writer.EnteredAsync();
        writer.ReleaseWrite();

        Assert.Equal(V1.SyncState.Idle, (await writer.NextAsync()).State);
        var behind = await writer.NextAsync();
        Assert.Equal(V1.SyncState.Error, behind.State);
        Assert.Equal(V1.SyncFailure.PushRejected, behind.Failure);
        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);
        Assert.Equal(2, writer.Entered);
    }

    [Fact]
    public async Task WatchStatus_stops_listening_when_the_stream_ends()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.SyncStatus>();
        using var close = new CancellationTokenSource();
        var watching = api.SyncCalls.WatchAsync(writer, close.Token);
        await writer.NextAsync();
        Assert.Equal(1, api.Sync.StatusSubscriberCount);

        await close.CancelAsync();
        await CallOutcome.EndedAsync(watching);

        Assert.Equal(0, api.Sync.StatusSubscriberCount);
    }

    [Fact]
    public async Task Stopping_the_API_ends_an_open_WatchStatus_stream()
    {
        await using var api = new ApiHarness();
        var writer = new RecordingStreamWriter<V1.SyncStatus>();
        using var never = new CancellationTokenSource();
        var watching = api.SyncCalls.WatchAsync(writer, never.Token);
        await writer.NextAsync();

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        await CallOutcome.EndedAsync(watching);
    }
}
