using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
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
        Assert.Equal(0, app.Updates.StartCount);
    }

    [Fact]
    public void Start_begins_Updates_once_after_the_tray_icon_is_shown_and_the_shortcuts_are_connected()
    {
        var app = new ShellHarness();
        bool? iconShown = null;
        int? connectCount = null;
        app.Updates.OnStart = _ =>
        {
            iconShown = app.TrayIcon.IsShown;
            connectCount = app.Configure.ConnectCount;
        };

        app.Start();

        Assert.Equal(1, app.Updates.StartCount);
        Assert.True(iconShown);
        Assert.Equal(1, connectCount);
    }

    [Fact]
    public void Start_begins_Updates_with_the_apps_exit()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.IsAssignableFrom<IAppExit>(app.Updates.Exit);
    }

    [Fact]
    public void Start_offers_Add_new_task_Task_View_Contacts_Configure_Logs_and_Exit_in_that_order_under_the_apps_name()
    {
        var app = new ShellHarness();

        app.Start();

        Assert.Equal("Memoria-ToDo", app.TrayIcon.ToolTip);
        Assert.Equal(["Add new task", "Task View", "Contacts", "Configure", "Logs", "Exit"], app.TrayIcon.MenuTexts);
    }

    [Fact]
    public void Create_rejects_a_missing_Contacts_window()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), null!, new FakeConfigureWindow(),
            new FakeLogsWindow(), new FakeUpdateLifetime(), new FakeLocalApiLifetime(), new FakeMcpServerLifetime(), new FakeKeepAwakeLifetime(), new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_Local_API_lifetime()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), new FakeContactsWindow(), new FakeConfigureWindow(),
            new FakeLogsWindow(), new FakeUpdateLifetime(), null!, new FakeMcpServerLifetime(), new FakeKeepAwakeLifetime(), new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), Logger.None));

        Assert.Equal("localApi", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_MCP_server()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), new FakeContactsWindow(), new FakeConfigureWindow(),
            new FakeLogsWindow(), new FakeUpdateLifetime(), new FakeLocalApiLifetime(), null!, new FakeKeepAwakeLifetime(), new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), Logger.None));

        Assert.Equal("mcpServer", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_Keep_awake()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), new FakeContactsWindow(), new FakeConfigureWindow(),
            new FakeLogsWindow(), new FakeUpdateLifetime(), new FakeLocalApiLifetime(), new FakeMcpServerLifetime(), null!, new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), Logger.None));

        Assert.Equal("keepAwake", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_Logs_window()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), new FakeContactsWindow(), new FakeConfigureWindow(),
            null!, new FakeUpdateLifetime(), new FakeLocalApiLifetime(), new FakeMcpServerLifetime(), new FakeKeepAwakeLifetime(), new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), Logger.None));

        Assert.Equal("logs", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_logger()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => TrayApp.Create(
            new FakeTrayIcon(), new FakeAddWindow(), new FakeViewTasksWindow(), new FakeContactsWindow(), new FakeConfigureWindow(),
            new FakeLogsWindow(), new FakeUpdateLifetime(), new FakeLocalApiLifetime(), new FakeMcpServerLifetime(), new FakeKeepAwakeLifetime(), new FakeGlobalShortcut(), new FakeOneInstance(), true,
            new FakeSyncService(), null!));

        Assert.Equal("logger", failure.ParamName);
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
    public void An_unconfigured_start_shows_Configure_once_when_a_shortcut_did_not_register_and_sync_publishes_during_Start()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Configure.ConnectAnswer = false;

        app.Start();

        Assert.Equal(["Configure"], app.Shown);
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
    public void A_later_start_of_the_running_app_shows_Task_View_and_never_Configure()
    {
        var app = new ShellHarness();
        app.Configure.ConnectAnswer = false;
        app.Start();
        var configureShows = app.Configure.ShowCount;

        app.OneInstance.StartLaterInstance();

        Assert.Equal(configureShows, app.Configure.ShowCount);
        Assert.Equal(["Configure", "Task View"], app.Shown);
    }

    [Fact]
    public void A_start_by_hand_shows_only_Task_View()
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
    public void Start_fails_before_beginning_Updates_when_Windows_cannot_add_the_icon()
    {
        var app = new ShellHarness();
        app.TrayIcon.ShowFailure = new InvalidOperationException("No taskbar.");

        Assert.Throws<InvalidOperationException>(app.Start);

        Assert.Equal(0, app.Updates.StartCount);
    }
}
