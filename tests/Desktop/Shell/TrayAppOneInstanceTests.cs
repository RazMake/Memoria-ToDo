using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppOneInstanceTests
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private static ShellHarness LaterInstance(bool startedAtSignIn = false)
    {
        var app = new ShellHarness(startedAtSignIn);
        app.OneInstance.AnotherInstanceRuns = true;
        app.Start();
        return app;
    }

    [Fact]
    public void A_later_instance_ends_the_app()
    {
        var app = LaterInstance();

        Assert.Equal(1, app.EndAppCalls);
    }

    [Fact]
    public void A_later_instance_adds_no_tray_icon()
    {
        var app = LaterInstance();

        Assert.False(app.TrayIcon.IsShown);
        Assert.Equal(0, app.TrayIcon.ShowCount);
    }

    [Fact]
    public void A_later_instance_neither_connects_nor_registers_a_shortcut()
    {
        var app = LaterInstance();

        Assert.Equal(0, app.Configure.ConnectCount);
        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void A_later_instance_starts_no_update_checks()
    {
        var app = LaterInstance();
        app.Time.Advance(Day * 3);

        Assert.Equal(0, app.Updates.CallCount);
    }

    [Fact]
    public void A_later_instance_shows_no_window_even_when_started_by_hand()
    {
        var app = LaterInstance(startedAtSignIn: false);

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void The_first_instance_adds_the_tray_icon_and_does_not_end_the_app()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.True(app.TrayIcon.IsShown);
        Assert.Equal(0, app.EndAppCalls);
        Assert.True(app.OneInstance.IsClaimed);
    }

    [Fact]
    public void The_first_instance_connects_the_shortcuts_once()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.Equal(1, app.Configure.ConnectCount);
    }

    [Fact]
    public async Task The_first_instance_begins_the_update_checks()
    {
        var app = new ShellHarness();

        app.Start();
        await app.Updates.WaitForCallsAsync(1);

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public void Each_later_start_shows_View_Tasks_in_the_running_instance()
    {
        var app = new ShellHarness();
        app.Start();

        app.OneInstance.StartLaterInstance();
        app.OneInstance.StartLaterInstance();

        Assert.Equal(2, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }
}
