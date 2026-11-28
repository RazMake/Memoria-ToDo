using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppShutdownTests
{
    [Theory]
    [InlineData("handover")]
    [InlineData("left-click")]
    [InlineData("Add new task")]
    [InlineData("Task View")]
    [InlineData("Contacts")]
    [InlineData("Configure")]
    [InlineData("Logs")]
    [InlineData("notification")]
    public async Task Actions_queued_before_shutdown_cannot_reopen_windows(string action)
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = true;
        app.Start();
        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Authentication });
        var queued = action switch
        {
            "handover" => app.OneInstance.StartLaterInstance,
            "left-click" => app.TrayIcon.QueueLeftClick(),
            "notification" => app.TrayIcon.ClickNotification,
            _ => app.TrayIcon.QueueMenuItem(action),
        };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.McpServer.StopOperation = () => release.Task;
        var stop = app.Lifetime.StopAsync(CancellationToken.None);
        try
        {
            app.Shown.Clear();
            var before = app.Log.Events.Count;

            queued();

            Assert.Empty(app.Shown);
            Assert.Contains(app.Log.Events.Skip(before), entry => entry.Level == LogEventLevel.Information);
        }
        finally
        {
            release.TrySetResult();
            await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    [InlineData(ShortcutKind.Configure)]
    [InlineData(ShortcutKind.Contacts)]
    public async Task A_shortcut_already_queued_cannot_open_a_window_after_shutdown(ShortcutKind kind)
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = true;
        app.Start();
        app.ActionOf(kind).Register(TestShortcut.A);
        var queued = app.GlobalShortcut.QueuePress(TestShortcut.A);
        await app.Lifetime.StopAsync(CancellationToken.None);
        app.Shown.Clear();
        var before = app.Log.Events.Count;

        queued();

        Assert.Empty(app.Shown);
        Assert.Contains(app.Log.Events.Skip(before), entry => entry.Level == LogEventLevel.Information);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_or_cancelled_cleanup_is_logged_before_waiting_for_the_next_step(bool cancelled)
    {
        var app = new ShellHarness();
        app.Start();
        Exception failure = cancelled ? new OperationCanceledException("cancelled stop") : new InvalidOperationException("failed stop");
        app.McpServer.StopOperation = () => Task.FromException(failure);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Sync.StopOperation = _ => release.Task;
        var stop = app.Lifetime.StopAsync(CancellationToken.None);
        try
        {
            Assert.False(stop.IsCompleted);
            Assert.Contains(app.Log.Events, entry => entry.Level == LogEventLevel.Error && ReferenceEquals(entry.Exception, failure));
            Assert.Equal(1, app.Sync.StopCount);
        }
        finally
        {
            release.TrySetResult();
            await Assert.ThrowsAnyAsync<Exception>(() => stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.OneInstance.IsClaimed);
    }
}
