using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The real app starts Shell before the message pump runs, so no synchronization context is set yet, while Sync
/// publishes the loaded setup on a worker thread. These tests build that situation on their own UI thread: it has a
/// WPF dispatcher that is not running yet, and no synchronization context. The publication comes from another
/// thread; the queued dispatcher work then runs on the UI thread, and the test awaits only the results.
/// </summary>
[Unit]
public class TrayAppStartupThreadTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(true, "View Tasks")]
    [InlineData(false, "Configure")]
    public async Task The_automatic_window_opens_on_the_UI_thread_when_setup_is_published_by_another_thread(
        bool configured, string window)
    {
        var run = await RunOnUiThreadAsync(configured, beforeQueuedWorkRuns: null);
        try
        {
            Assert.Equal([window], run.App.Shown);
            Assert.Equal(
                [run.UiThread],
                window == "Configure" ? run.App.Configure.ShownOnThreads : run.App.ViewTasks.ShownOnThreads);
        }
        finally
        {
            await run.App.Lifetime.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_manual_route_taken_before_the_queued_automatic_window_runs_still_wins()
    {
        var run = await RunOnUiThreadAsync(configured: true, app =>
        {
            app.TrayIcon.ChooseMenuItem("Manage tasks");
            return null;
        });
        try
        {
            Assert.Equal(["View Tasks"], run.App.Shown);
            Assert.Equal([run.UiThread], run.App.ViewTasks.ShownOnThreads);
        }
        finally
        {
            await run.App.Lifetime.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Shutdown_before_the_queued_automatic_window_runs_opens_no_window()
    {
        var run = await RunOnUiThreadAsync(configured: true, app => app.Lifetime.StopAsync(CancellationToken.None));
        try
        {
            await run.Stop!;
            Assert.Empty(run.App.Shown);
        }
        finally
        {
            await run.App.Lifetime.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Setup_published_after_shutdown_started_opens_no_window()
    {
        var run = await RunOnUiThreadAsync(
            configured: true, app => app.Lifetime.StopAsync(CancellationToken.None), stopBeforePublishing: true);
        try
        {
            await run.Stop!;
            Assert.Empty(run.App.Shown);
        }
        finally
        {
            await run.App.Lifetime.StopAsync(CancellationToken.None);
        }
    }

    private sealed record UiRun(ShellHarness App, int UiThread, Task? Stop);

    // On a new UI thread: starts Shell, publishes setup from another thread, does what the test asks (a manual route
    // or the start of shutdown) while that publication's work is still queued, then runs the queued dispatcher work.
    // With stopBeforePublishing, the shutdown starts first and the publication comes after it.
    private static async Task<UiRun> RunOnUiThreadAsync(
        bool configured,
        Func<ShellHarness, Task?>? beforeQueuedWorkRuns,
        bool stopBeforePublishing = false)
    {
        var done = new TaskCompletionSource<UiRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ui = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                Assert.Null(SynchronizationContext.Current);
                var app = new ShellHarness(startedAtSignIn: false);
                app.Sync.HoldStartupPublication = true;
                app.Start();

                Task? stop = null;
                if (stopBeforePublishing)
                {
                    stop = beforeQueuedWorkRuns?.Invoke(app);
                    PublishFromAnotherThread(app, configured);
                }
                else
                {
                    PublishFromAnotherThread(app, configured);
                    stop = beforeQueuedWorkRuns?.Invoke(app);
                }

                dispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
                Dispatcher.Run();
                done.SetResult(new UiRun(app, Environment.CurrentManagedThreadId, stop));
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        })
        { IsBackground = true };
        ui.Start();

        try
        {
            return await done.Task.WaitAsync(Limit);
        }
        finally
        {
            if (done.Task.IsCompleted)
            {
                Assert.True(ui.Join(Limit), "The UI thread did not end.");
            }
        }
    }

    // A thread of its own, as Sync's worker is; the UI thread waits for it to end, within a limit.
    private static void PublishFromAnotherThread(ShellHarness app, bool configured)
    {
        ExceptionDispatchInfo? failure = null;
        var publisher = new Thread(() =>
        {
            try
            {
                app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = configured });
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        { IsBackground = true };
        publisher.Start();
        Assert.True(publisher.Join(Limit), "The publishing thread did not end.");
        failure?.Throw();
    }
}
