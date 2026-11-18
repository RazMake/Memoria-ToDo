using Memoria.ToDo.Shared.Fakes;
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
        Assert.ThrowsAny<ArgumentException>(() => SyncService.Create(f.Repository, f.Tasks, f.Files, f.Keys, f.Selection, f.Time, f.DeviceFolder, " ", null, f.Git));
        Assert.ThrowsAny<ArgumentException>(() => SyncService.Create(f.Repository, f.Tasks, f.Files, f.Keys, f.Selection, f.Time, "relative", "Laptop", null, f.Git));
    }

    [Fact]
    public void Rejects_a_missing_synced_repository()
    {
        Assert.Throws<ArgumentNullException>(() => SyncService.Create(null!, f.Tasks, f.Files, f.Keys, f.Selection, f.Time, f.DeviceFolder, "Laptop", null, f.Git));
    }

    [Fact]
    public async Task Creates_without_any_io_timer_or_pass()
    {
        await using var sync = f.Create(TimeSpan.FromMinutes(30));

        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(0, f.Time.ActiveTimerCount);
        Assert.Equal(0, f.Git.ConnectionsOpened);
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
            await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
            await observed.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        }
        finally
        {
            held.EndCancelled();
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.True(stopCompletedAfterPassEnded);
    }

    [Theory]
    [InlineData(SyncFailure.Unexpected)]
    [InlineData(SyncFailure.Connection)]
    [InlineData(SyncFailure.LocalStorage)]
    public async Task Treats_any_pass_failure_after_stopping_began_as_a_stop_publishing_no_error_arming_no_retry_and_ending_the_caller_as_a_stopped_service_does(SyncFailure kind)
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var failures = new System.Collections.Concurrent.ConcurrentQueue<SyncFailure>();
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is { } failure)
                failures.Enqueue(failure);
        };
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        var stop = sync.StopAsync(CancellationToken.None);
        await held.WasCancelled;
        held.Fail(new GitFailureException(kind, "The transport was torn down.", new ObjectDisposedException("Transport")));
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.Empty(failures);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Treats_a_malformed_saved_folder_found_by_the_real_location_check_after_stopping_began_as_a_stop()
    {
        f.Ready();
        await using var sync = f.Create();
        var failures = new System.Collections.Concurrent.ConcurrentQueue<SyncFailure>();
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is { } failure)
                failures.Enqueue(failure);
        };
        var checking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Files.OnPhysicalCheck = () =>
        {
            checking.TrySetResult();
            Assert.True(release.Wait(TestWait.ForSignal));
        };
        f.Files.FailPhysicalChecks(new ArgumentException("The folder is malformed."));
        var running = sync.SyncNowAsync(CancellationToken.None);
        await checking.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        release.Set();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.Empty(failures);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Never_publishes_a_repository_failure_when_stopping_begins_while_the_location_check_is_held_and_then_finds_the_clone_contains_the_device_folder()
    {
        f.Ready();
        f.Tasks.RepositoryFolder = Path.GetDirectoryName(f.DeviceFolder)!;
        await using var sync = f.Create();
        var before = sync.Status;
        var seen = new System.Collections.Concurrent.ConcurrentQueue<SyncFailure?>();
        sync.StatusChanged += (_, _) => seen.Enqueue(sync.Status.Failure);
        var checking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Files.OnPhysicalCheck = () =>
        {
            checking.TrySetResult();
            Assert.True(release.Wait(TestWait.ForSignal));
        };
        var running = sync.SyncNowAsync(CancellationToken.None);
        await checking.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        release.Set();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(seen, failure => failure is not null);
        Assert.Equal(before.State, sync.Status.State);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Git.ConnectionsOpened);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Treats_a_failure_whose_description_is_still_being_read_when_stopping_begins_as_a_stop()
    {
        f.Ready();
        await using var sync = f.Create();
        var before = sync.Status;
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Files.OnPhysicalCheck = () => throw new SlowMessageException(reading, release);
        var running = sync.SyncNowAsync(CancellationToken.None);
        await reading.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        release.Set();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.Equal(before.State, sync.Status.State);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    private sealed class SlowMessageException(TaskCompletionSource reading, ManualResetEventSlim release) : IOException
    {
        public override string Message
        {
            get
            {
                reading.TrySetResult();
                Assert.True(release.Wait(TestWait.ForSignal));
                return "The disk is unreadable.";
            }
        }
    }

    [Fact]
    public async Task Never_publishes_the_git_failure_of_a_pass_when_stopping_begins_while_the_setup_refresh_after_it_is_held_and_then_succeeds()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = sync.Status;
        var seen = new System.Collections.Concurrent.ConcurrentQueue<SyncFailure?>();
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is { } failure)
                seen.Enqueue(failure);
        };
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.OnReadOrigin = () =>
        {
            refreshing.TrySetResult();
            Assert.True(release.Wait(TestWait.ForSignal));
        };
        held.Fail(new IOException("The disk is full."));
        await refreshing.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        release.Set();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.Empty(seen);
        Assert.Equal(before.State, sync.Status.State);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Restores_the_status_from_before_the_pass_when_stopping_begins_while_the_setup_refresh_after_a_successful_pass_is_held_and_then_fails()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = sync.Status;
        var failures = new System.Collections.Concurrent.ConcurrentQueue<SyncFailure>();
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is { } failure)
                failures.Enqueue(failure);
        };
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.OnReadOrigin = () =>
        {
            refreshing.TrySetResult();
            Assert.True(release.Wait(TestWait.ForSignal));
            throw new IOException("Unreadable clone.");
        };
        held.Complete();
        await refreshing.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        release.Set();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        Assert.Equal(before.State, sync.Status.State);
        Assert.Empty(failures);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Gives_a_clone_cancelled_by_stopping_the_cancellation_together_with_the_cleanup_failure_and_its_warning_about_partial_files()
    {
        f.WithKey();
        var cloning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var undo = new IOException("A file is locked.");
        f.Git.WhileCloning = async token =>
        {
            cloning.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException cancelled)
            {
                throw new IOException(
                    "The operation was cancelled, but undoing its changes failed, so some files may remain in the folder: A file is locked.",
                    new AggregateException(cancelled, undo));
            }
        };
        await using var sync = f.Create();
        var clone = sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);
        await cloning.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<IOException>(() => clone.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Contains("some files may remain", failure.Message);
        var both = Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions;
        Assert.Contains(both, e => e is OperationCanceledException);
        Assert.Contains(undo, both);
    }

    [Fact]
    public async Task Ends_a_clone_cancelled_by_stopping_with_no_cleanup_failure_as_a_stopped_service_does()
    {
        f.WithKey();
        var cloning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Git.WhileCloning = async token =>
        {
            cloning.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        await using var sync = f.Create();
        var clone = sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);
        await cloning.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        var stop = sync.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => clone.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
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
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>             stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

            var passEnded = false;
            var again = sync.StopAsync(CancellationToken.None);
            var observed = again.ContinueWith(_ => againCompletedAfterPassEnded = passEnded, TaskScheduler.Default);
            passEnded = true;
            held.EndCancelled();
            await again.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
            await observed.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
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

            await Task.WhenAll(stop, dispose).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
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

        await Assert.ThrowsAnyAsync<ObjectDisposedException>(() => sync.SyncNowAsync(CancellationToken.None).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));
        await Assert.IsAssignableFrom<Task>(stopping).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(keyOpensAtStop, f.Keys.OpenCalls);
        Assert.Equal(0, f.Git.PassesStarted);
        Assert.NotEqual(SyncState.Working, sync.Status.State);
    }
}
