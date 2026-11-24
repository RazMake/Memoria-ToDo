using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>Shell starts the MCP server once the clone has loaded, and stops it first among the services it shuts down.</summary>
[Unit]
public class TrayAppMcpServerTests
{
    private static ShellHarness StartedWithSetupHeld(bool startedAtSignIn = true)
    {
        var app = new ShellHarness(startedAtSignIn);
        app.Sync.HoldStartupPublication = true;
        app.Start();
        return app;
    }

    [Fact]
    public void The_MCP_server_does_not_start_before_local_setup_has_loaded()
    {
        var app = StartedWithSetupHeld();

        Assert.Equal(0, app.McpServer.StartCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_MCP_server_starts_when_local_setup_loads(bool configured)
    {
        var app = StartedWithSetupHeld();

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = configured });

        Assert.Equal(1, app.McpServer.StartCount);
    }

    [Fact]
    public void The_MCP_server_starts_when_the_initial_publication_is_a_failure()
    {
        var app = StartedWithSetupHeld();

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Unexpected, Details = "Failed" });
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        Assert.Equal(1, app.McpServer.StartCount);
    }

    [Fact]
    public void The_MCP_server_starts_only_once_however_often_setup_is_published()
    {
        var app = StartedWithSetupHeld();

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(1, app.McpServer.StartCount);
    }

    [Fact]
    public void The_MCP_server_starts_even_when_the_user_already_opened_a_window_by_hand()
    {
        var app = StartedWithSetupHeld(startedAtSignIn: false);
        app.TrayIcon.ChooseMenuItem("Task View");

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(1, app.McpServer.StartCount);
    }

    [Fact]
    public void The_MCP_server_starts_on_the_UI_thread_not_on_the_thread_that_published_the_setup()
    {
        var app = StartedWithSetupHeld();
        app.UiThread.IsCurrent = false;

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        Assert.Equal(0, app.McpServer.StartCount);

        app.UiThread.RunQueued();

        Assert.Equal(1, app.McpServer.StartCount);
    }

    [Fact]
    public async Task Shutdown_before_the_posted_start_runs_cancels_it()
    {
        var app = StartedWithSetupHeld();
        app.UiThread.IsCurrent = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        await app.Lifetime.StopAsync(CancellationToken.None);

        app.UiThread.RunQueued();

        Assert.Equal(0, app.McpServer.StartCount);
    }

    [Fact]
    public async Task Setup_published_after_shutdown_started_does_not_start_the_MCP_server()
    {
        var app = StartedWithSetupHeld();
        await app.Lifetime.StopAsync(CancellationToken.None);

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(0, app.McpServer.StartCount);
    }

    [Fact]
    public void A_later_instance_does_not_start_the_MCP_server()
    {
        var app = new ShellHarness();
        app.OneInstance.AnotherInstanceRuns = true;

        app.Start();

        Assert.Equal(0, app.McpServer.StartCount);
    }

    [Fact]
    public async Task Shutdown_stops_the_MCP_server_before_Sync_is_stopped_or_disposed_and_before_Updates_stop()
    {
        var app = new ShellHarness();
        app.Start();
        var syncTouched = true;
        var updatesStopped = true;
        var claimed = false;
        app.McpServer.StopOperation = () =>
        {
            syncTouched = app.Sync.StopCount > 0 || app.Sync.DisposeCount > 0;
            updatesStopped = app.Updates.StopCount > 0;
            claimed = app.OneInstance.IsClaimed;
            return Task.CompletedTask;
        };

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.McpServer.StopCount);
        Assert.False(syncTouched);
        Assert.False(updatesStopped);
        Assert.True(claimed);
    }

    [Fact]
    public async Task Shutdown_releases_the_shortcuts_and_the_tray_before_it_stops_the_MCP_server()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        var trayShown = true;
        var registered = -1;
        app.McpServer.StopOperation = () =>
        {
            trayShown = app.TrayIcon.IsShown;
            registered = app.GlobalShortcut.Registered.Count();
            return Task.CompletedTask;
        };

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(trayShown);
        Assert.Equal(0, registered);
    }

    [Fact]
    public async Task Shutdown_waits_for_the_MCP_server_to_stop_before_it_stops_Sync_and_Updates_and_gives_up_the_claim()
    {
        var app = new ShellHarness();
        app.Start();
        var mcpStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.McpServer.StopOperation = () => mcpStopped.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(stopping.IsCompleted);
        Assert.Equal(0, app.Sync.StopCount);
        Assert.Equal(0, app.Sync.DisposeCount);
        Assert.Equal(0, app.Updates.StopCount);
        Assert.True(app.OneInstance.IsClaimed);

        mcpStopped.SetResult();
        await stopping;

        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task A_failed_MCP_server_stop_still_stops_Sync_and_Updates_and_gives_up_the_claim_and_passes_the_failure_on()
    {
        var app = new ShellHarness();
        app.Start();
        app.McpServer.StopOperation = () => Task.FromException(new InvalidOperationException("The server would not stop."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("The server would not stop.", error.Message);
        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
        Assert.False(app.TrayIcon.IsShown);
    }

    [Fact]
    public async Task A_throwing_MCP_server_stop_is_handled_like_a_failed_one()
    {
        var app = new ShellHarness();
        app.Start();
        app.McpServer.StopOperation = () => throw new InvalidOperationException("The server threw.");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("The server threw.", error.Message);
        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Failures_of_the_MCP_server_and_Sync_stops_are_both_passed_on()
    {
        var app = new ShellHarness();
        app.Start();
        var mcpFailure = new InvalidOperationException("The server would not stop.");
        var syncFailure = new IOException("Could not close transport");
        app.McpServer.StopOperation = () => Task.FromException(mcpFailure);
        app.Sync.StopOperation = _ => Task.FromException(syncFailure);

        var error = await Assert.ThrowsAsync<AggregateException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equivalent(new Exception[] { mcpFailure, syncFailure }, error.InnerExceptions);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Shutdown_stops_the_MCP_server_once_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.McpServer.StopCount);
    }
}
