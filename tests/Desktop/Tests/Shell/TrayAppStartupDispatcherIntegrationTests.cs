using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The real app starts Shell before the message pump runs, while Sync publishes the loaded setup on a worker thread.
/// This builds that situation on its own UI thread with a real WPF dispatcher, to check Shell's real UI-thread wiring.
/// </summary>
[Integration]
public class TrayAppStartupDispatcherIntegrationTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Setup_published_by_a_worker_thread_opens_the_window_on_the_UI_thread()
    {
        var done = new TaskCompletionSource<(ShellHarness App, int UiThread)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ui = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var app = new ShellHarness(startedAtSignIn: false, realUiThread: () => new WpfUiThread());
                app.Sync.HoldStartupPublication = true;
                app.Start();
                PublishFromAnotherThread(app);
                dispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
                Dispatcher.Run();
                done.SetResult((app, Environment.CurrentManagedThreadId));
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        })
        { IsBackground = true };
        ui.Start();

        var run = await done.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(["Task View"], run.App.Shown);
            Assert.Equal([run.UiThread], run.App.ViewTasks.ShownOnThreads);
        }
        finally
        {
            await run.App.Lifetime.StopAsync(CancellationToken.None);
            Assert.True(ui.Join(Limit), "The UI thread did not end.");
        }
    }

    private static void PublishFromAnotherThread(ShellHarness app)
    {
        ExceptionDispatchInfo? failure = null;
        var publisher = new Thread(() =>
        {
            try
            {
                app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
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
