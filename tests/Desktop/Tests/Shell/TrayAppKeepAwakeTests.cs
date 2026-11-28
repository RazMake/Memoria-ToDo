using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>Shell starts Keep awake right after Updates, in the instance that holds the claim, and stops it first in the UI-thread part of the shutdown.</summary>
[Unit]
public class TrayAppKeepAwakeTests
{
    [Fact]
    public void Create_does_not_start_Keep_awake()
    {
        var app = new ShellHarness();

        Assert.Equal(0, app.KeepAwake.StartCount);
    }

    [Fact]
    public void Start_starts_Keep_awake_once_on_the_calling_UI_thread()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.Equal(1, app.KeepAwake.StartCount);
        Assert.Equal([Environment.CurrentManagedThreadId], app.KeepAwake.StartThreads);
    }

    [Fact]
    public void Start_starts_Keep_awake_after_Updates_have_started()
    {
        var app = new ShellHarness();
        var updatesStarted = false;
        app.KeepAwake.OnStart = () => updatesStarted = app.Updates.StartCount == 1;

        app.Start();

        Assert.True(updatesStarted);
    }

    [Fact]
    public void Start_starts_Keep_awake_before_Sync_has_published_anything()
    {
        var app = new ShellHarness();
        app.Sync.HoldStartupPublication = true;

        app.Start();

        Assert.Equal(1, app.KeepAwake.StartCount);
        Assert.Equal(0, app.McpServer.StartCount);
    }

    [Fact]
    public void A_later_instance_does_not_start_Keep_awake()
    {
        var app = new ShellHarness();
        app.OneInstance.AnotherInstanceRuns = true;

        app.Start();

        Assert.Equal(0, app.KeepAwake.StartCount);
    }

    [Fact]
    public async Task Shutdown_stops_Keep_awake_once()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.KeepAwake.StopCount);
    }

    [Fact]
    public async Task Shutdown_stops_Keep_awake_once_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.KeepAwake.StopCount);
    }

    [Fact]
    public async Task Shutdown_stops_Keep_awake_before_it_waits_for_the_Local_API_the_MCP_server_Sync_or_Updates()
    {
        var app = new ShellHarness();
        app.Start();
        var localApiStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.LocalApi.StopOperation = () => localApiStopped.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(stopping.IsCompleted);
        Assert.Equal(1, app.KeepAwake.StopCount);
        Assert.Equal(0, app.McpServer.StopCount);
        Assert.Equal(0, app.Sync.StopCount);
        Assert.Equal(0, app.Updates.StopCount);

        localApiStopped.SetResult();
        await stopping;
    }

    [Fact]
    public async Task Shutdown_does_not_stop_the_Local_API_before_it_has_stopped_Keep_awake()
    {
        var app = new ShellHarness();
        app.Start();
        var keepAwakeStopped = -1;
        app.LocalApi.StopOperation = () =>
        {
            keepAwakeStopped = app.KeepAwake.StopCount;
            return Task.CompletedTask;
        };

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, keepAwakeStopped);
    }

    [Fact]
    public async Task Shutdown_stops_Keep_awake_in_the_same_synchronous_part_that_releases_the_tray_and_the_shortcuts()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.LocalApi.StopOperation = () => release.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.KeepAwake.StopCount);
        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);

        release.SetResult();
        await stopping;
    }

    [Fact]
    public async Task A_failed_Keep_awake_stop_still_releases_the_tray_and_stops_everything_else_and_passes_the_failure_on()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.KeepAwake.OnStop = () => throw new InvalidOperationException("Windows kept the request.");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("Windows kept the request.", error.Message);
        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);
        Assert.Equal(1, app.LocalApi.StopCount);
        Assert.Equal(1, app.McpServer.StopCount);
        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task A_failed_Keep_awake_stop_is_kept_with_the_other_cleanup_failures()
    {
        var app = new ShellHarness();
        app.Start();
        var keepAwakeFailure = new InvalidOperationException("Windows kept the request.");
        var mcpFailure = new IOException("The server would not stop.");
        app.KeepAwake.OnStop = () => throw keepAwakeFailure;
        app.McpServer.StopOperation = () => Task.FromException(mcpFailure);

        var error = await Assert.ThrowsAsync<AggregateException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equivalent(new Exception[] { keepAwakeFailure, mcpFailure }, error.InnerExceptions);
    }

    [Fact]
    public async Task A_failed_Keep_awake_stop_is_logged_as_an_error()
    {
        var app = new ShellHarness();
        app.Start();
        var failure = new InvalidOperationException("Windows kept the request.");
        app.KeepAwake.OnStop = () => throw failure;

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Contains(app.Log.Events, entry => entry.Level == LogEventLevel.Error && ReferenceEquals(entry.Exception, failure));
    }

    [Fact]
    public async Task A_failed_Keep_awake_stop_is_not_repeated_by_a_second_shutdown_call()
    {
        var app = new ShellHarness();
        app.Start();
        app.KeepAwake.OnStop = () => throw new InvalidOperationException("Windows kept the request.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal(1, app.KeepAwake.StopCount);
    }
}
