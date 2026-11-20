using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The tray menu's Logs item: it closes every other window and opens the Logs window, whatever the setup, and the window stays
/// out of the switching rules, the "any window open" answer, and Close windows, since it holds nothing to save.
/// </summary>
[Unit]
public class TrayAppLogsItemTests
{
    private static ShellHarness Started(bool available = true)
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = available;
        app.Start();
        return app;
    }

    [Fact]
    public void The_menu_puts_Logs_after_Configure_and_before_Exit()
    {
        var app = Started(available: false);

        Assert.Equal(["Configure", "Logs", "Exit"], app.TrayIcon.MenuTexts.TakeLast(3));
    }

    [Fact]
    public void The_Logs_item_closes_the_other_windows_and_shows_the_Logs_window()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.Equal(ShellHarness.LogsShown, app.Shown);
        Assert.Equal(1, app.Configure.CloseCount);
        Assert.Equal(1, app.Logs.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public void Each_choice_of_the_Logs_item_shows_the_window_again()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Logs");
        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.Equal(2, app.Logs.ShowCount);
    }

    [Fact]
    public async Task The_Logs_item_on_an_unconfigured_device_shows_Logs_and_does_not_open_Configure()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        app.Shown.Clear();

        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.Equal(ShellHarness.LogsShown, app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_Logs_item_used_while_setup_loads_shows_Logs_and_suppresses_the_later_automatic_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Start();

        app.TrayIcon.ChooseMenuItem("Logs");
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(ShellHarness.LogsShown, app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void The_Logs_item_closes_every_open_window_and_shows_Logs()
    {
        var app = Started();
        app.Add.IsOpen = true;
        app.ViewTasks.IsOpen = true;
        app.Contacts.IsOpen = true;
        app.Configure.IsOpen = true;

        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.False(app.Add.IsOpen);
        Assert.False(app.ViewTasks.IsOpen);
        Assert.False(app.Contacts.IsOpen);
        Assert.False(app.Configure.IsOpen);
        Assert.Equal(1, app.Logs.ShowCount);
    }

    [Fact]
    public void The_Logs_item_shows_Logs_even_when_a_window_stays_open_because_the_user_cancelled_its_close()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;
        app.Contacts.IsOpen = true;
        app.Contacts.CloseAnswer = false;

        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.Equal(1, app.Logs.ShowCount);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    [InlineData(ShortcutKind.Contacts)]
    [InlineData(ShortcutKind.Configure)]
    public void A_shortcut_after_the_Logs_window_was_shown_shows_only_its_window_and_closes_none(ShortcutKind kind)
    {
        var app = Started();
        app.ActionOf(kind).Register(TestShortcut.A);
        app.TrayIcon.ChooseMenuItem("Logs");
        app.Shown.Clear();

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal([ShellHarness.WindowOf(kind)], app.Shown);
    }

    [Fact]
    public void No_window_is_open_after_the_Logs_window_was_shown()
    {
        var app = Started();

        app.TrayIcon.ChooseMenuItem("Logs");

        Assert.False(app.Updates.Exit.AnyWindowOpen);
    }

    [Fact]
    public void Close_windows_after_the_Logs_window_was_shown_closes_only_Task_View_and_Contacts()
    {
        var app = Started();
        app.TrayIcon.ChooseMenuItem("Logs");
        app.Shown.Clear();

        var closed = app.Updates.Exit.CloseWindows();

        Assert.True(closed);
        Assert.Equal(["Close Task View", "Close Contacts"], app.Shown);
        Assert.Equal(1, app.Logs.ShowCount);
    }
}
