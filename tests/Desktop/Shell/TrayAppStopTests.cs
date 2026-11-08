using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppStopTests
{
    [Fact]
    public void Stop_removes_the_tray_icon()
    {
        var app = new ShellHarness();
        app.Start();

        app.Lifetime.Stop();

        Assert.False(app.TrayIcon.IsShown);
    }

    [Fact]
    public void Stop_unregisters_both_shortcuts()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);

        app.Lifetime.Stop();

        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Stop_gives_up_the_claim()
    {
        var app = new ShellHarness();
        app.Start();
        Assert.True(app.OneInstance.IsClaimed);

        app.Lifetime.Stop();

        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task Stop_ends_the_update_checks()
    {
        var app = new ShellHarness();
        app.Start();
        await app.Updates.WaitForCompletedCallsAsync(1);
        await app.Time.WaitForActiveTimerAsync();

        app.Lifetime.Stop();
        app.Time.Advance(TimeSpan.FromHours(72));

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public void Stop_works_after_Start_failed_and_gives_up_the_claim()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");
        Assert.Throws<InvalidOperationException>(app.Start);
        Assert.True(app.OneInstance.IsClaimed);

        app.Lifetime.Stop();

        Assert.False(app.OneInstance.IsClaimed);
        Assert.False(app.TrayIcon.IsShown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public async Task A_failed_release_of_one_shortcut_still_releases_the_other_stops_the_update_checks_removes_the_icon_and_gives_up_the_claim(
        ShortcutKind failing)
    {
        var app = new ShellHarness();
        app.Start();
        await app.Updates.WaitForCompletedCallsAsync(1);
        await app.Time.WaitForActiveTimerAsync();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        var failingShortcut = failing == ShortcutKind.AddNewTask ? TestShortcut.A : TestShortcut.B;
        var otherShortcut = failing == ShortcutKind.AddNewTask ? TestShortcut.B : TestShortcut.A;
        app.GlobalShortcut.FailUnregister(failingShortcut, new InvalidOperationException("Windows won't release."));

        var error = Assert.Throws<InvalidOperationException>(app.Lifetime.Stop);
        app.Time.Advance(TimeSpan.FromHours(72));

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([failingShortcut], app.GlobalShortcut.Registered);
        Assert.DoesNotContain(otherShortcut, app.GlobalShortcut.Registered);
        Assert.Equal(1, app.Updates.CallCount);
        Assert.False(app.TrayIcon.IsShown);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public void Failed_releases_of_both_shortcuts_pass_on_both_failures_after_the_icon_is_removed_and_the_claim_given_up()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        var addFailure = new InvalidOperationException("Windows won't release A.");
        var manageFailure = new InvalidOperationException("Windows won't release B.");
        app.GlobalShortcut.FailUnregister(TestShortcut.A, addFailure);
        app.GlobalShortcut.FailUnregister(TestShortcut.B, manageFailure);

        var error = Assert.Throws<AggregateException>(app.Lifetime.Stop);

        Assert.Equivalent(new[] { addFailure, manageFailure }, error.InnerExceptions);
        Assert.False(app.TrayIcon.IsShown);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public void Stop_works_when_Start_never_ran()
    {
        var app = new ShellHarness();

        app.Lifetime.Stop();

        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Stop_works_when_repeated()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        app.Lifetime.Stop();

        app.Lifetime.Stop();

        Assert.False(app.TrayIcon.IsShown);
        Assert.Empty(app.GlobalShortcut.Registered);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public void Stop_works_in_a_later_instance_that_ended_at_Start()
    {
        var app = new ShellHarness();
        app.OneInstance.AnotherInstanceRuns = true;
        app.Start();

        app.Lifetime.Stop();

        Assert.False(app.TrayIcon.IsShown);
    }
}
