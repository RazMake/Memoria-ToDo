using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// Sync publishes the loaded setup on a worker thread, so the automatic window must wait for the UI thread. These
/// tests use a fake UI thread that holds the posted work until the test lets it run.
/// </summary>
[Unit]
public class TrayAppStartupThreadTests
{
    private static ShellHarness StartedWithSetupPublishedByAnotherThread(bool configured)
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Start();
        app.UiThread.IsCurrent = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = configured });
        return app;
    }

    [Theory]
    [InlineData(true, "Task View")]
    [InlineData(false, "Configure")]
    public void The_automatic_window_opens_only_once_the_UI_thread_runs_the_work_posted_by_another_thread(
        bool configured, string window)
    {
        var app = StartedWithSetupPublishedByAnotherThread(configured);
        Assert.Empty(app.Shown);

        app.UiThread.RunQueued();

        Assert.Equal([window], app.Shown);
    }

    [Fact]
    public void A_manual_route_taken_before_the_posted_automatic_window_runs_still_wins()
    {
        var app = StartedWithSetupPublishedByAnotherThread(configured: true);
        app.TrayIcon.ChooseMenuItem("Task View");
        app.ViewTasks.IsOpen = false;

        app.UiThread.RunQueued();

        Assert.Equal(["Task View"], app.Shown);
    }

    [Fact]
    public async Task Shutdown_before_the_posted_automatic_window_runs_opens_no_window()
    {
        var app = StartedWithSetupPublishedByAnotherThread(configured: true);
        await app.Lifetime.StopAsync(CancellationToken.None);

        app.UiThread.RunQueued();

        Assert.Empty(app.Shown);
    }

    [Fact]
    public async Task Setup_published_after_shutdown_started_opens_no_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Start();
        await app.Lifetime.StopAsync(CancellationToken.None);
        app.UiThread.IsCurrent = false;

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        app.UiThread.RunQueued();

        Assert.Empty(app.Shown);
    }
}
