using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The third global shortcut, the one that opens Configure, by the shell design's switching table: it closes Add or
/// Task View first, and does nothing while Configure is open or while both of the others are.
/// </summary>
[Unit]
public class TrayAppConfigureShortcutTests
{
    private static ShellHarness Started()
    {
        var app = new ShellHarness();
        app.Start();
        app.ActionOf(ShortcutKind.Configure).Register(TestShortcut.A);
        return app;
    }

    [Fact]
    public void With_nothing_open_shows_only_Configure()
    {
        var app = Started();

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Configure"], app.Shown);
    }

    [Fact]
    public void With_Add_open_closes_Add_then_shows_Configure()
    {
        var app = Started();
        app.Add.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Add", "Configure"], app.Shown);
    }

    [Fact]
    public void With_Task_View_open_closes_it_and_shows_Configure_when_it_closed()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View", "Configure"], app.Shown);
    }

    [Fact]
    public void With_Task_View_open_does_not_show_Configure_when_Task_View_stays_open()
    {
        var app = Started();
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View"], app.Shown);
    }

    [Fact]
    public void While_Configure_is_open_brings_it_forward_and_closes_nothing()
    {
        var app = Started();
        app.Configure.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Configure"], app.Shown);
    }

    [Fact]
    public void While_both_Add_and_Task_View_are_open_does_nothing()
    {
        var app = Started();
        app.Add.IsOpen = true;
        app.ViewTasks.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Empty(app.Shown);
    }

    [Fact]
    public async Task On_an_unconfigured_device_opens_Configure_and_closes_no_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        app.ActionOf(ShortcutKind.Configure).Register(TestShortcut.A);
        app.Shown.Clear();

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Used_while_setup_loads_suppresses_the_automatic_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        Assert.True(app.ActionOf(ShortcutKind.Configure).Register(TestShortcut.A));

        app.GlobalShortcut.Press(TestShortcut.A);
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_unregisters_the_Configure_shortcut_too()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        app.Configure.ConfigureAction.Register(TestShortcut.ConfigureDefault);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public async Task A_failed_release_of_the_Configure_shortcut_still_releases_the_others_and_passes_on_the_failure()
    {
        var app = new ShellHarness();
        app.Start();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ConfigureAction.Register(TestShortcut.ConfigureDefault);
        app.GlobalShortcut.FailUnregister(TestShortcut.ConfigureDefault, new InvalidOperationException("Windows won't release C."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("Windows won't release C.", error.Message);
        Assert.Equal([TestShortcut.ConfigureDefault], app.GlobalShortcut.Registered);
        Assert.False(app.TrayIcon.IsShown);
    }
}
