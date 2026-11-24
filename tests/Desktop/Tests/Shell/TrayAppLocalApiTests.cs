using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>Shell starts and stops the Local API: design in docs/architecture/local-api.md and desktop/shell.md.</summary>
[Unit]
public class TrayAppLocalApiTests
{
    [Fact]
    public async Task Start_starts_the_Local_API_once_the_claim_is_won()
    {
        var app = new ShellHarness();
        var claimedWhenStarted = false;
        app.LocalApi.OnStart = () => claimedWhenStarted = app.OneInstance.IsClaimed;

        app.Start();

        Assert.Equal(1, app.LocalApi.StartCount);
        Assert.True(claimedWhenStarted);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_starts_the_Local_API_only_after_Sync_started_and_published_its_local_setup()
    {
        var app = new ShellHarness();
        var syncStartedWhenApiStarted = -1;
        app.LocalApi.OnStart = () => syncStartedWhenApiStarted = app.Sync.StartCount;

        app.Start();

        Assert.Equal(1, syncStartedWhenApiStarted);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_Local_API_waits_for_the_local_setup_to_load()
    {
        var app = new ShellHarness();
        app.Sync.HoldStartupPublication = true;

        app.Start();
        Assert.Equal(0, app.LocalApi.StartCount);

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(1, app.LocalApi.StartCount);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_failed_initialization_still_starts_the_Local_API_after_its_setup_publication()
    {
        var app = new ShellHarness();
        app.Sync.HoldStartupPublication = true;
        app.Start();

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Unexpected });
        Assert.Equal(0, app.LocalApi.StartCount);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        Assert.Equal(1, app.LocalApi.StartCount);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Later_setup_publications_do_not_start_the_Local_API_again()
    {
        var app = new ShellHarness();
        app.Sync.HoldStartupPublication = true;
        app.Start();

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(1, app.LocalApi.StartCount);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void A_later_instance_never_starts_the_Local_API()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.OneInstance.AnotherInstanceRuns = true;

        app.Start();
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(0, app.LocalApi.StartCount);
    }

    [Fact]
    public async Task Stopping_before_the_local_setup_loaded_leaves_the_Local_API_unstarted()
    {
        var app = new ShellHarness();
        app.Sync.HoldStartupPublication = true;
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(0, app.LocalApi.StartCount);
    }

    [Fact]
    public async Task Stop_stops_the_Local_API_before_Sync_shuts_down_and_while_the_claim_is_held()
    {
        var app = new ShellHarness();
        app.Start();
        var syncUntouched = false;
        var claimed = false;
        app.LocalApi.StopOperation = () =>
        {
            syncUntouched = app.Sync.StopCount == 0 && app.Sync.DisposeCount == 0;
            claimed = app.OneInstance.IsClaimed;
            return Task.CompletedTask;
        };

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.LocalApi.StopCount);
        Assert.True(syncUntouched);
        Assert.True(claimed);
    }

    [Fact]
    public async Task Stop_does_not_stop_Sync_while_the_Local_API_is_still_stopping()
    {
        var app = new ShellHarness();
        app.Start();
        var apiStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.LocalApi.StopOperation = () => apiStopped.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(stopping.IsCompleted);
        Assert.Equal(0, app.Sync.StopCount);
        Assert.Equal(0, app.Sync.DisposeCount);

        apiStopped.SetResult();
        await stopping;

        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
    }

    [Fact]
    public async Task A_failed_Local_API_stop_still_stops_Sync_and_gives_up_the_claim_and_passes_the_failure_on()
    {
        var app = new ShellHarness();
        app.Start();
        app.LocalApi.StopOperation = () => Task.FromException(new InvalidOperationException("The pipe would not close."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("The pipe would not close.", error.Message);
        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_stops_the_Local_API_once_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.LocalApi.StopCount);
    }
}
