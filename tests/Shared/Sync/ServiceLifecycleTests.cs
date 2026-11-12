using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>Creating, stopping, and disposing the service.</summary>
[Unit]
public sealed class ServiceLifecycleTests
{
    private readonly SyncFixture f = new();

    [Fact]
    public void Rejects_a_non_positive_periodic_interval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Create(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Create(TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void Rejects_a_blank_default_device_name_or_relative_device_folder()
    {
        Assert.ThrowsAny<ArgumentException>(() => SyncService.Create(f.Tasks, f.Files, f.Keys, f.Selection, f.Time, f.DeviceFolder, " ", null, f.Git));
        Assert.ThrowsAny<ArgumentException>(() => SyncService.Create(f.Tasks, f.Files, f.Keys, f.Selection, f.Time, "relative", "Laptop", null, f.Git));
    }

    [Fact]
    public void Creates_without_any_io_timer_or_pass()
    {
        var sync = f.Create(TimeSpan.FromMinutes(30));

        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(0, f.Time.ActiveTimerCount);
        Assert.Equal(0, f.Git.ConnectionsOpened);
        GC.KeepAlive(sync);
    }

    [Fact]
    public async Task Stopping_cancels_the_running_pass_and_completes_after_it_ends()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        var passEnded = false;
        var stopCompletedAfterPassEnded = false;
        try
        {
            var stop = sync.StopAsync(CancellationToken.None);
            var observed = stop.ContinueWith(_ => stopCompletedAfterPassEnded = passEnded, TaskScheduler.Default);
            await held.WasCancelled;

            passEnded = true;
            held.EndCancelled();
            await stop.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await observed;
        }
        finally
        {
            held.EndCancelled();
        }

        await Record.ExceptionAsync(() => running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.True(stopCompletedAfterPassEnded);
    }

    [Fact]
    public async Task Stopping_with_a_cancelled_token_only_abandons_the_wait()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        _ = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        using var cancel = new CancellationTokenSource();
        var againCompletedAfterPassEnded = false;
        try
        {
            var stop = sync.StopAsync(cancel.Token);
            await held.WasCancelled;

            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

            var passEnded = false;
            var again = sync.StopAsync(CancellationToken.None);
            var observed = again.ContinueWith(_ => againCompletedAfterPassEnded = passEnded, TaskScheduler.Default);
            passEnded = true;
            held.EndCancelled();
            await again.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await observed;
        }
        finally
        {
            held.EndCancelled();
        }

        Assert.True(againCompletedAfterPassEnded);
    }

    [Fact]
    public async Task Disposing_and_repeated_stopping_join_the_same_shutdown()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        var sync = f.Create();
        _ = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        try
        {
            var stop = sync.StopAsync(CancellationToken.None);
            var dispose = sync.DisposeAsync().AsTask();
            await held.WasCancelled;
            held.EndCancelled();

            await Task.WhenAll(stop, dispose).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            held.EndCancelled();
        }
    }

    [Fact]
    public async Task Refuses_every_operation_once_stopped()
    {
        f.Ready();
        var sync = f.Create();
        await sync.StopAsync(CancellationToken.None);

        Assert.Throws<ObjectDisposedException>(() => sync.Start());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.SyncNowAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.Setup.GenerateKeyAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Stopping_cancels_a_pending_debounce_and_retry_timers()
    {
        var sync = await f.StartedAndSettledAsync(TimeSpan.FromMinutes(10));
        f.Tasks.RaiseLocalSaved();
        await f.Time.WaitForActiveTimerAsync();

        await sync.StopAsync(CancellationToken.None);

        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Does_not_open_the_key_or_start_the_backend_pass_when_a_stop_is_requested_as_the_pass_starts_working()
    {
        f.Ready();
        await using var sync = f.Create();
        Task? stopping = null;
        var keyOpensAtStop = -1;
        var requested = 0;
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.State == SyncState.Working && Interlocked.Exchange(ref requested, 1) == 0)
            {
                keyOpensAtStop = f.Keys.OpenCalls;
                stopping = sync.StopAsync(CancellationToken.None);
            }
        };

        var bound = TimeSpan.FromSeconds(10);
        await Assert.ThrowsAnyAsync<ObjectDisposedException>(() => sync.SyncNowAsync(CancellationToken.None).WaitAsync(bound, TestContext.Current.CancellationToken));
        await Assert.IsAssignableFrom<Task>(stopping).WaitAsync(bound, TestContext.Current.CancellationToken);

        Assert.Equal(keyOpensAtStop, f.Keys.OpenCalls);
        Assert.Equal(0, f.Git.PassesStarted);
        Assert.NotEqual(SyncState.Working, sync.Status.State);
    }
}
