using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppStopTests
{
    [Fact]
    public async Task Stop_removes_the_tray_icon()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.TrayIcon.IsShown);
    }

    [Fact]
    public async Task Stop_unregisters_both_shortcuts()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public async Task Stop_gives_up_the_claim()
    {
        var app = new ShellHarness();
        app.Start();
        Assert.True(app.OneInstance.IsClaimed);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_stops_Updates_after_Sync_has_shut_down_and_before_the_claim_is_given_up()
    {
        var app = new ShellHarness();
        app.Start();
        var syncStoppedAndDisposed = false;
        var claimedDuringUpdatesStop = false;
        app.Updates.StopOperation = () =>
        {
            syncStoppedAndDisposed = app.Sync.StopCount == 1 && app.Sync.DisposeCount == 1;
            claimedDuringUpdatesStop = app.OneInstance.IsClaimed;
            return Task.CompletedTask;
        };

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.Updates.StopCount);
        Assert.True(syncStoppedAndDisposed);
        Assert.True(claimedDuringUpdatesStop);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_does_not_stop_Updates_while_Sync_is_still_shutting_down()
    {
        var app = new ShellHarness();
        app.Start();
        var syncStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Sync.StopOperation = _ => syncStopped.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(0, app.Updates.StopCount);

        syncStopped.SetResult();
        await stopping;

        Assert.Equal(1, app.Updates.StopCount);
    }

    [Fact]
    public async Task Stop_waits_for_Updates_to_stop_before_giving_up_the_claim()
    {
        var app = new ShellHarness();
        app.Start();
        var updatesStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Updates.StopOperation = () => updatesStopped.Task;

        var stopping = app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(stopping.IsCompleted);
        Assert.True(app.OneInstance.IsClaimed);

        updatesStopped.SetResult();
        await stopping;

        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task A_failed_Updates_stop_still_gives_up_the_claim_and_passes_the_failure_on()
    {
        var app = new ShellHarness();
        app.Start();
        app.Updates.StopOperation = () => Task.FromException(new InvalidOperationException("The updater would not start."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("The updater would not start.", error.Message);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_stops_Updates_once_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(1, app.Updates.StopCount);
    }

    [Fact]
    public async Task Stop_works_after_Start_failed_and_gives_up_the_claim()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");
        Assert.Throws<InvalidOperationException>(app.Start);
        Assert.True(app.OneInstance.IsClaimed);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.OneInstance.IsClaimed);
        Assert.False(app.TrayIcon.IsShown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public async Task A_failed_release_of_one_shortcut_still_releases_the_other_stops_Updates_removes_the_icon_and_gives_up_the_claim(
        ShortcutKind failing)
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        var failingShortcut = failing == ShortcutKind.AddNewTask ? TestShortcut.A : TestShortcut.B;
        var otherShortcut = failing == ShortcutKind.AddNewTask ? TestShortcut.B : TestShortcut.A;
        app.GlobalShortcut.FailUnregister(failingShortcut, new InvalidOperationException("Windows won't release."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([failingShortcut], app.GlobalShortcut.Registered);
        Assert.DoesNotContain(otherShortcut, app.GlobalShortcut.Registered);
        Assert.Equal(1, app.Updates.StopCount);
        Assert.False(app.TrayIcon.IsShown);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Failed_releases_of_both_shortcuts_pass_on_both_failures_after_the_icon_is_removed_and_the_claim_given_up()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        var addFailure = new InvalidOperationException("Windows won't release A.");
        var manageFailure = new InvalidOperationException("Windows won't release B.");
        app.GlobalShortcut.FailUnregister(TestShortcut.A, addFailure);
        app.GlobalShortcut.FailUnregister(TestShortcut.B, manageFailure);

        var error = await Assert.ThrowsAsync<AggregateException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equivalent(new[] { addFailure, manageFailure }, error.InnerExceptions);
        Assert.False(app.TrayIcon.IsShown);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_works_when_Start_never_ran()
    {
        var app = new ShellHarness();

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public async Task Stop_works_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        await app.Lifetime.StopAsync(CancellationToken.None);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_works_in_a_later_instance_that_ended_at_Start()
    {
        var app = new ShellHarness();
        app.OneInstance.AnotherInstanceRuns = true;
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.False(app.TrayIcon.IsShown);
    }
}
