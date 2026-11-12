using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>When Sync runs a pass: manual, saved edits, periodic, retry after failure, and one pass at a time.</summary>
[Unit]
public sealed class SchedulingTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(5);

    private readonly SyncFixture f = new();

    [Fact]
    public async Task Runs_a_manual_sync_before_the_service_is_started()
    {
        f.Ready();
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(1, f.Git.PassesStarted);
    }

    [Fact]
    public async Task Shares_one_pass_between_concurrent_manual_syncs()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();

        var first = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var second = sync.SyncNowAsync(CancellationToken.None);
        held.Complete();
        await Task.WhenAll(first, second);

        Assert.Equal(1, f.Git.PassesStarted);
    }

    [Fact]
    public async Task Starts_with_an_initial_pass_without_waiting_for_it()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();

        sync.Start();
        await held.HasStarted;

        var joined = sync.SyncNowAsync(CancellationToken.None);
        held.Complete();
        await joined;

        Assert.Equal(1, f.Git.PassesStarted);
    }

    [Fact]
    public async Task Ignores_a_repeated_start_instead_of_requesting_another_pass()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        await held.HasStarted;

        sync.Start();
        var joined = sync.SyncNowAsync(CancellationToken.None);
        held.Complete();
        await joined;

        Assert.Equal(1, f.Git.PassesStarted);
    }

    [Fact]
    public async Task Does_not_arm_the_debounce_for_an_edit_saved_before_the_service_is_started()
    {
        f.Ready();
        await using var sync = f.Create();

        f.Tasks.RaiseLocalSaved();

        Assert.Equal(0, f.Time.ActiveTimerCount);
        Assert.Equal(0, f.Git.PassesStarted);
    }

    [Fact]
    public async Task Picks_up_an_edit_saved_before_the_start_in_the_initial_pass()
    {
        f.Ready();
        await using var sync = f.Create();
        f.Tasks.RaiseLocalSaved();

        sync.Start();

        await f.Git.WaitForPassesStartedAsync(1);
        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Does_not_arm_a_retry_for_a_manual_sync_that_fails_before_the_service_is_started()
    {
        f.Ready();
        f.Git.ScriptPass().Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        await using var sync = f.Create();

        await Record.ExceptionAsync(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Syncs_five_seconds_after_a_saved_edit()
    {
        await using var sync = await f.StartedAndSettledAsync();

        f.Tasks.RaiseLocalSaved();
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(Debounce);

        await f.Git.WaitForPassesStartedAsync(2);
    }

    [Fact]
    public async Task Waits_the_full_debounce_again_after_each_further_edit()
    {
        await using var sync = await f.StartedAndSettledAsync();
        f.Tasks.RaiseLocalSaved();
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(TimeSpan.FromSeconds(3));

        f.Tasks.RaiseLocalSaved();
        f.Time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(1, f.Time.ActiveTimerCount);
        Assert.Equal(1, f.Git.PassesStarted);
        f.Time.Advance(TimeSpan.FromSeconds(2));
        await f.Git.WaitForPassesStartedAsync(2);
    }

    [Fact]
    public async Task Sets_no_timer_when_there_is_no_periodic_interval()
    {
        await using var sync = await f.StartedAndSettledAsync(periodicInterval: null);

        Assert.Equal(0, f.Time.ActiveTimerCount);
    }

    [Fact]
    public async Task Syncs_again_when_the_periodic_interval_elapses()
    {
        var interval = TimeSpan.FromMinutes(10);
        await using var sync = await f.StartedAndSettledAsync(interval);
        await f.Time.WaitForActiveTimerAsync();

        f.Time.Advance(interval - TimeSpan.FromSeconds(1));
        Assert.Equal(1, f.Git.PassesStarted);
        f.Time.Advance(TimeSpan.FromSeconds(1));

        await f.Git.WaitForPassesStartedAsync(2);
    }

    [Fact]
    public async Task Syncs_periodically_when_the_clone_is_selected_after_starting_without_one()
    {
        var interval = TimeSpan.FromMinutes(30);
        f.WithKey();
        var manual = f.Git.ScriptPass();
        await using var sync = f.Create(interval);
        sync.Start();
        await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);
        var joined = sync.SyncNowAsync(CancellationToken.None);
        try
        {
            await manual.HasStarted;
        }
        finally
        {
            manual.Complete();
        }

        await joined;
        var passes = f.Git.PassesStarted;

        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(interval);

        await f.Git.WaitForPassesStartedAsync(passes + 1);
    }

    [Fact]
    public async Task Retries_a_failed_pass_after_30_seconds_doubling_up_to_15_minutes()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        await held.HasStarted;
        var started = 1;

        foreach (var seconds in new[] { 30, 60, 120, 240, 480, 900, 900 })
        {
            var next = f.Git.ScriptPass();
            held.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
            await f.Time.WaitForActiveTimerAsync();

            f.Time.Advance(TimeSpan.FromSeconds(seconds - 1));
            Assert.Equal(started, f.Git.PassesStarted);
            f.Time.Advance(TimeSpan.FromSeconds(1));
            await next.HasStarted;
            started++;
            held = next;
        }

        held.Complete();
    }

    [Fact]
    public async Task Starts_a_pass_at_once_when_the_user_syncs_during_a_retry_wait()
    {
        f.Ready();
        var failing = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        await failing.HasStarted;
        failing.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        await f.Time.WaitForActiveTimerAsync();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(2, f.Git.PassesStarted);
        Assert.Equal(SyncState.Idle, sync.Status.State);
    }

    [Fact]
    public async Task Runs_one_follow_up_after_edits_made_during_a_pass_and_never_overlaps()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        f.Tasks.RaiseLocalSaved();
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(Debounce);
        f.Tasks.RaiseLocalSaved();
        held.Complete();
        await running;
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(Debounce);
        await f.Git.WaitForPassesStartedAsync(2);

        Assert.Equal(1, f.Git.MaxConcurrentPasses);
    }

    [Fact]
    public async Task Cancelling_a_joiner_stops_only_its_wait_and_not_the_running_pass()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var owner = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        using var cancel = new CancellationTokenSource();
        var joiner = sync.SyncNowAsync(cancel.Token);

        try
        {
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => joiner.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

            Assert.Equal(SyncState.Working, sync.Status.State);
        }
        finally
        {
            held.Complete();
        }

        await owner;
        Assert.Equal(SyncState.Idle, sync.Status.State);
    }
}
