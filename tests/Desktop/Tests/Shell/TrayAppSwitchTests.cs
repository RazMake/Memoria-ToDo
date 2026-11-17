using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// What a shortcut does by the window that's open: it switches between Add and Task View, and a window's own shortcut
/// does nothing. The rule is in the shell design's switching table.
/// </summary>
[Unit]
public class TrayAppSwitchTests
{
    private static ShellHarness Started(ShortcutKind kind)
    {
        var app = new ShellHarness();
        app.Start();
        app.ActionOf(kind).Register(TestShortcut.A);
        return app;
    }

    [Fact]
    public void Task_View_with_Add_open_closes_Add_then_shows_Task_View()
    {
        var app = Started(ShortcutKind.ManageTasks);
        app.Add.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Add", "Task View"], app.Shown);
    }

    [Fact]
    public void Add_new_task_with_Task_View_open_closes_it_and_shows_Add_when_it_closed()
    {
        var app = Started(ShortcutKind.AddNewTask);
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View", "Add"], app.Shown);
    }

    [Fact]
    public void Add_new_task_with_Task_View_open_does_not_show_Add_when_Task_View_stays_open()
    {
        var app = Started(ShortcutKind.AddNewTask);
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View"], app.Shown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_windows_own_shortcut_while_it_is_open_brings_it_forward_and_closes_nothing(ShortcutKind kind)
    {
        var app = Started(kind);
        app.Add.IsOpen = kind == ShortcutKind.AddNewTask;
        app.ViewTasks.IsOpen = kind == ShortcutKind.ManageTasks;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal([kind == ShortcutKind.AddNewTask ? "Add" : "Task View"], app.Shown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_shortcut_while_both_Add_and_Task_View_are_open_brings_its_own_window_forward(ShortcutKind kind)
    {
        var app = Started(kind);
        app.Add.IsOpen = true;
        app.ViewTasks.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal([kind == ShortcutKind.AddNewTask ? "Add" : "Task View"], app.Shown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_shortcut_with_nothing_open_shows_only_its_window_and_closes_none(ShortcutKind kind)
    {
        var app = Started(kind);

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal([kind == ShortcutKind.AddNewTask ? "Add" : "Task View"], app.Shown);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public async Task A_shortcut_on_an_unconfigured_device_opens_Configure_and_closes_no_window(ShortcutKind kind)
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        app.ActionOf(kind).Register(TestShortcut.A);
        app.Shown.Clear();

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void The_Add_menu_item_with_Task_View_open_shows_Add_without_closing_Task_View()
    {
        var app = new ShellHarness();
        app.Start();
        app.ViewTasks.IsOpen = true;

        app.TrayIcon.ChooseMenuItem("Add new task");

        Assert.Equal(["Add"], app.Shown);
    }

    [Fact]
    public void A_left_click_with_Add_open_shows_Task_View_without_closing_Add()
    {
        var app = new ShellHarness();
        app.Start();
        app.Add.IsOpen = true;

        app.TrayIcon.LeftClick();

        Assert.Equal(["Task View"], app.Shown);
    }

    [Fact]
    public void The_Task_View_menu_item_with_Add_open_shows_Task_View_without_closing_Add()
    {
        var app = new ShellHarness();
        app.Start();
        app.Add.IsOpen = true;

        app.TrayIcon.ChooseMenuItem("Task View");

        Assert.Equal(["Task View"], app.Shown);
    }
}
