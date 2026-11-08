using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppStartTests
{
    [Fact]
    public void Create_shows_nothing_until_Start()
    {
        var app = new ShellHarness();

        Assert.False(app.TrayIcon.IsShown);
        Assert.Equal(0, app.Updates.CallCount);
    }

    [Fact]
    public void Start_shows_the_tray_icon()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.True(app.TrayIcon.IsShown);
    }

    [Fact]
    public void Start_names_the_app_in_the_tooltip()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.Equal("Memoria-ToDo", app.TrayIcon.ToolTip);
    }

    [Fact]
    public void Start_offers_Add_new_task_Manage_tasks_Configure_and_Exit_in_that_order()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.Equal(["Add new task", "Manage tasks", "Configure", "Exit"], app.TrayIcon.MenuTexts);
    }

    [Fact]
    public void A_start_at_sign_in_shows_no_window()
    {
        var app = new ShellHarness(startedAtSignIn: true);

        app.Start();

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void A_start_by_hand_shows_Configure_after_View_Tasks_when_a_shortcut_did_not_register()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Configure.ConnectAnswer = false;

        app.Start();

        Assert.Equal(["View Tasks", "Configure"], app.Shown);
    }

    [Fact]
    public void A_start_at_sign_in_shows_only_Configure_when_a_shortcut_did_not_register()
    {
        var app = new ShellHarness(startedAtSignIn: true);
        app.Configure.ConnectAnswer = false;

        app.Start();

        Assert.Equal(["Configure"], app.Shown);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Configure_stays_closed_when_both_shortcuts_registered(bool startedAtSignIn)
    {
        var app = new ShellHarness(startedAtSignIn);
        app.Configure.ConnectAnswer = true;

        app.Start();

        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void A_later_start_of_the_running_app_shows_View_Tasks_and_never_Configure()
    {
        var app = new ShellHarness();
        app.Configure.ConnectAnswer = false;
        app.Start();
        var configureShows = app.Configure.ShowCount;

        app.OneInstance.StartLaterInstance();

        Assert.Equal(configureShows, app.Configure.ShowCount);
        Assert.Equal(["Configure", "View Tasks"], app.Shown);
    }

    [Fact]
    public void A_start_by_hand_shows_only_View_Tasks()
    {
        var app = new ShellHarness(startedAtSignIn: false);

        app.Start();

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(1, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void Start_passes_on_the_error_when_Windows_cannot_add_the_icon()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");

        var error = Assert.Throws<InvalidOperationException>(app.Start);

        Assert.Equal("No taskbar.", error.Message);
    }

    [Fact]
    public void Start_fails_before_connecting_the_shortcut_when_Windows_cannot_add_the_icon()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");

        Assert.Throws<InvalidOperationException>(app.Start);

        Assert.Equal(0, app.Configure.ConnectCount);
        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void Start_fails_before_beginning_the_update_checks_when_Windows_cannot_add_the_icon()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");

        Assert.Throws<InvalidOperationException>(app.Start);
        app.Time.Advance(TimeSpan.FromHours(72));

        Assert.Equal(0, app.Updates.CallCount);
    }
}
