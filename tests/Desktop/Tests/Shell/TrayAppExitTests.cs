using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>The app exit Shell gives Updates, so an update can close the windows and end the app.</summary>
[Unit]
public class TrayAppExitTests
{
    private static ShellHarness Started()
    {
        var app = new ShellHarness();
        app.Start();
        return app;
    }

    [Fact]
    public void No_window_is_open_when_none_of_Add_Task_View_and_Configure_is()
    {
        var app = Started();

        Assert.False(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void A_window_is_open_while_Add_is_open()
    {
        var app = Started();
        app.Add.IsOpen = true;

        Assert.True(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void A_window_is_open_while_Task_View_is_open()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;

        Assert.True(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void A_window_is_open_while_Configure_is_open()
    {
        var app = Started();
        app.Configure.IsOpen = true;

        Assert.True(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void A_window_is_open_while_Contacts_is_open()
    {
        var app = Started();
        app.Contacts.IsOpen = true;

        Assert.True(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void Close_windows_closes_Task_View_first_then_Contacts_and_answers_true_when_both_closed()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.Contacts.IsOpen = true;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.True(closed);
        Assert.Equal(["Close Task View", "Close Contacts"], app.Shown);
        Assert.False(app.Contacts.IsOpen);
    }

    [Fact]
    public void Close_windows_closes_Contacts_when_Task_View_is_not_open()
    {
        var app = Started();
        app.Contacts.IsOpen = true;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.True(closed);
        Assert.Equal(1, app.Contacts.CloseCount);
        Assert.False(app.Contacts.IsOpen);
    }

    [Fact]
    public void Close_windows_answers_false_without_closing_Contacts_when_Task_View_stays_open()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;
        app.Contacts.IsOpen = true;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.False(closed);
        Assert.Equal(0, app.Contacts.CloseCount);
        Assert.True(app.Contacts.IsOpen);
    }

    [Fact]
    public void Close_windows_answers_false_when_Contacts_stays_open_after_its_prompt_about_a_changed_form()
    {
        var app = Started();
        app.Contacts.IsOpen = true;
        app.Contacts.CloseAnswer = false;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.False(closed);
        Assert.True(app.Contacts.IsOpen);
    }

    [Fact]
    public void No_window_is_open_once_the_open_one_closed()
    {
        var app = Started();
        app.Add.IsOpen = true;
        app.Add.IsOpen = false;

        Assert.False(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void Close_windows_closes_Task_View_and_answers_true_when_it_closed()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.True(closed);
        Assert.Equal(1, app.ViewTasks.CloseCount);
        Assert.False(app.ViewTasks.IsOpen);
    }

    [Fact]
    public void Close_windows_answers_false_when_Task_View_stays_open_after_its_prompt_about_a_changed_edit()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;

        var closed = app.Updates.Exit.CloseWindows();

        Assert.False(closed);
        Assert.True(app.ViewTasks.IsOpen);
    }

    [Fact]
    public void Close_windows_answers_true_when_Task_View_is_not_open()
    {
        var app = Started();

        Assert.True(app.Updates.Exit.CloseWindows());
    }

    [Fact]
    public void Close_windows_leaves_Add_and_Configure_open_for_the_exit_to_close()
    {
        var app = Started();
        app.Add.IsOpen = true;
        app.Configure.IsOpen = true;

        app.Updates.Exit.CloseWindows();

        Assert.Equal(0, app.Add.CloseCount);
        Assert.True(app.Add.IsOpen);
        Assert.True(app.Configure.IsOpen);
    }

    [Fact]
    public void End_ends_the_app_once_as_Exit_in_the_tray_menu_does()
    {
        var app = Started();

        app.Updates.Exit.End();

        Assert.Equal(1, app.EndAppCalls);
    }

    [Fact]
    public void Exit_in_the_tray_menu_ends_the_app_without_stopping_Updates_itself()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Exit");

        Assert.Equal(1, app.EndAppCalls);
        Assert.Equal(0, app.Updates.StopCount);
    }
}
