using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppMenuTests
{
    private static ShellHarness Started()
    {
        var app = new ShellHarness();
        app.Start();
        return app;
    }

    [Fact]
    public void Add_new_Task_shows_only_Add()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Add new Task");

        Assert.Equal(1, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void View_Tasks_shows_only_View_Tasks()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("View Tasks");

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(1, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void Configure_shows_only_Configure()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Configure");

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(1, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void Exit_ends_the_app_and_shows_no_window()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Exit");

        Assert.Equal(1, app.EndAppCalls);
        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void A_left_click_shows_only_Add()
    {
        var app = Started();

        app.TrayIcon.LeftClick();

        Assert.Equal(1, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void Each_choice_shows_the_window_again()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Add new Task");
        app.TrayIcon.LeftClick();

        Assert.Equal(2, app.Add.ShowCount);
    }
}
