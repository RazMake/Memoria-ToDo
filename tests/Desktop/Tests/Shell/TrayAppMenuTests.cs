using Memoria.ToDo.Shared.Fakes;
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
    public void Add_new_task_shows_only_Add()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Add new task");

        Assert.Equal(1, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Task_View_and_a_left_click_each_show_only_Task_View(bool leftClick)
    {
        var app = Started();

        if (leftClick)
        {
            app.TrayIcon.LeftClick();
        }
        else
        {
            app.TrayIcon.ChooseMenuItem("Task View");
        }

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
    public void Each_choice_shows_the_window_again()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Task View");
        app.TrayIcon.LeftClick();

        Assert.Equal(2, app.ViewTasks.ShowCount);
    }

    [Fact]
    public void Contacts_shows_only_Contacts()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Contacts");

        Assert.Equal(["Contacts"], app.Shown);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void Contacts_with_Task_View_open_shows_Contacts_without_closing_Task_View()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;

        app.TrayIcon.ChooseMenuItem("Contacts");

        Assert.Equal(["Contacts"], app.Shown);
    }

    [Fact]
    public void The_menu_has_no_Contacts_item_when_contacts_are_unavailable_at_start()
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = false;
        app.Start();

        Assert.Equal(["Add new task", "Task View", "Configure", "Logs", "Exit"], app.TrayIcon.MenuTexts);
    }

    [Fact]
    public void Contacts_becoming_unavailable_takes_the_Contacts_item_out_of_the_menu()
    {
        var app = Started();

        app.Contacts.IsAvailable = false;

        Assert.Equal(["Add new task", "Task View", "Configure", "Logs", "Exit"], app.TrayIcon.MenuTexts);
    }

    [Fact]
    public void Contacts_becoming_available_puts_the_Contacts_item_after_Task_View()
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = false;
        app.Start();

        app.Contacts.IsAvailable = true;

        Assert.Equal(["Add new task", "Task View", "Contacts", "Configure", "Logs", "Exit"], app.TrayIcon.MenuTexts);
    }

    [Fact]
    public void The_updated_menu_still_opens_each_window_and_ends_the_app()
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = false;
        app.Start();
        app.Contacts.IsAvailable = true;

        app.TrayIcon.ChooseMenuItem("Contacts");
        app.TrayIcon.ChooseMenuItem("Task View");
        app.TrayIcon.ChooseMenuItem("Exit");

        Assert.Equal(["Contacts", "Task View"], app.Shown);
        Assert.Equal(1, app.EndAppCalls);
    }

    [Fact]
    public void Each_change_of_availability_hands_the_new_menu_to_the_tray_icon_once()
    {
        var app = Started();
        var before = app.TrayIcon.MenuUpdateCount;

        app.Contacts.IsAvailable = false;
        app.Contacts.IsAvailable = true;

        Assert.Equal(before + 2, app.TrayIcon.MenuUpdateCount);
    }

    [Fact]
    public async Task Contacts_changing_after_Stop_changes_no_menu()
    {
        var app = Started();
        await app.Lifetime.StopAsync(CancellationToken.None);
        var before = app.TrayIcon.MenuUpdateCount;

        app.Contacts.IsAvailable = false;

        Assert.Equal(before, app.TrayIcon.MenuUpdateCount);
    }

    [Fact]
    public async Task The_Contacts_item_on_an_unconfigured_device_opens_Configure()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        app.Shown.Clear();

        app.TrayIcon.ChooseMenuItem("Contacts");

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_Contacts_item_used_while_setup_loads_suppresses_the_automatic_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();

        app.TrayIcon.ChooseMenuItem("Contacts");
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }
}
