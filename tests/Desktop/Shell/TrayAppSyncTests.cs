using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppSyncTests
{
    [Fact]
    public async Task Startup_waits_for_local_setup_to_load_before_choosing_a_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        app.Start();

        Assert.Empty(app.Shown);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        Assert.Equal(["View Tasks"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Loaded_unconfigured_setup_opens_Configure_without_waiting_for_network()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;

        app.Start();
        Assert.Empty(app.Shown);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        Assert.Equal(["Configure"], app.Shown);
        Assert.Equal(0, app.Sync.SyncCount);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Manual_navigation_while_setup_loads_suppresses_the_later_automatic_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();

        app.TrayIcon.LeftClick();
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public async Task A_shortcut_used_while_setup_loads_also_suppresses_the_automatic_window(ShortcutKind kind)
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Start();
        Assert.True(app.ActionOf(kind).Register(TestShortcut.A));

        app.GlobalShortcut.Press(TestShortcut.A);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(true, new[] { "View Tasks", "Configure" })]
    [InlineData(false, new[] { "Configure" })]
    public async Task A_failed_shortcut_registration_makes_one_startup_choice_after_local_setup_loads(
        bool setupConfigured, string[] expected)
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Configure.ConnectAnswer = false;
        app.Start();
        Assert.Empty(app.Shown);

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = setupConfigured });
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = setupConfigured });

        Assert.Equal(expected, app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_sign_in_start_opens_Configure_once_when_setup_is_unconfigured()
    {
        var app = new ShellHarness(startedAtSignIn: true);
        app.Sync.HoldStartupPublication = true;
        app.Start();

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_initialization_opens_Configure_once_only_after_its_failed_setup_publication(bool startedAtSignIn)
    {
        var app = new ShellHarness(startedAtSignIn);
        app.Sync.HoldStartupPublication = true;
        app.Start();

        app.Sync.Publish(Error(SyncFailure.Unexpected));
        Assert.Empty(app.Shown);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_first_shutdown_stops_listening_to_sync_status()
    {
        var app = new ShellHarness();
        app.Start();
        Assert.True(app.Sync.StatusSubscriberCount > 0);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Equal(0, app.Sync.StatusSubscriberCount);
    }

    [Fact]
    public async Task A_manual_route_while_setup_loads_suppresses_the_failed_shortcut_Configure()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Configure.ConnectAnswer = false;
        app.Start();

        app.TrayIcon.ChooseMenuItem("Manage tasks");
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["View Tasks"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Shutdown_while_setup_loads_suppresses_the_failed_shortcut_Configure()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Configure.ConnectAnswer = false;
        app.Start();

        await app.Lifetime.StopAsync(CancellationToken.None);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Empty(app.Shown);
    }

    [Fact]
    public async Task A_sync_error_notification_opening_Configure_suppresses_the_later_automatic_window()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Start();
        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Authentication });
        Assert.Empty(app.Shown);

        app.TrayIcon.ClickNotification();
        app.Configure.IsOpen = false;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Only_the_primary_instance_starts_and_stops_sync()
    {
        var primary = new ShellHarness();
        primary.Start();
        Assert.Equal(1, primary.Sync.StartCount);
        await primary.Lifetime.StopAsync(CancellationToken.None);
        await primary.Lifetime.StopAsync(CancellationToken.None);
        Assert.Equal(1, primary.Sync.StopCount);
        Assert.Equal(1, primary.Sync.DisposeCount);

        var later = new ShellHarness();
        later.OneInstance.AnotherInstanceRuns = true;
        later.Start();
        await later.Lifetime.StopAsync(CancellationToken.None);
        Assert.Equal(0, later.Sync.StartCount);
        Assert.Equal(0, later.Sync.StopCount);
        Assert.Equal(0, later.Sync.DisposeCount);
    }

    [Fact]
    public async Task An_unconfigured_device_opens_Configure_instead_of_tasks()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });

        app.Start();
        app.TrayIcon.LeftClick();
        app.TrayIcon.ChooseMenuItem("Manage tasks");

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.True(app.Configure.ShowCount > 0);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(SyncState.Offline, TraySyncBadge.Offline)]
    [InlineData(SyncState.Error, TraySyncBadge.Warning)]
    [InlineData(SyncState.Idle, TraySyncBadge.None)]
    public async Task The_tray_shows_the_current_sync_badge(SyncState state, TraySyncBadge badge)
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(new FakeSyncStatus { State = state, Details = "Test sync status" });

        Assert.Equal(badge, app.TrayIcon.Badge);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task An_error_notification_opens_Configure_and_offline_is_quiet()
    {
        var app = new ShellHarness();
        app.Start();
        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Offline });
        Assert.Empty(app.TrayIcon.Notifications);

        app.Sync.Publish(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Authentication, Details = "Key rejected"
        });
        Assert.Single(app.TrayIcon.Notifications);
        app.TrayIcon.ClickNotification();
        Assert.Equal(1, app.Configure.ShowCount);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Shutdown_awaits_sync_cleanup_and_repeated_callers_join_it()
    {
        var app = new ShellHarness();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Sync.StopOperation = _ => cleanup.Task;
        app.Start();

        var first = app.Lifetime.StopAsync(CancellationToken.None);
        var second = app.Lifetime.StopAsync(CancellationToken.None);
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            cleanup.TrySetResult();
        }
        await Task.WhenAll(first, second);

        Assert.Equal(1, app.Sync.StopCount);
        Assert.Equal(1, app.Sync.DisposeCount);
        Assert.Equal(1, app.Configure.DisposeCount);
        Assert.False(app.OneInstance.IsClaimed);
    }

    [Fact]
    public async Task A_kind_of_error_notifies_once_until_a_sync_succeeds()
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(Error(SyncFailure.Authentication));
        app.Sync.Publish(Error(SyncFailure.Authentication));

        Assert.Single(app.TrayIcon.Notifications);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(SyncState.Working)]
    [InlineData(SyncState.Offline)]
    public async Task Working_and_offline_do_not_let_the_same_error_notify_again(SyncState between)
    {
        var app = new ShellHarness();
        app.Start();
        app.Sync.Publish(Error(SyncFailure.Authentication));

        app.Sync.Publish(new FakeSyncStatus { State = between });
        app.Sync.Publish(Error(SyncFailure.Authentication));

        Assert.Single(app.TrayIcon.Notifications);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_different_kind_of_error_notifies_again()
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(Error(SyncFailure.Authentication));
        app.Sync.Publish(Error(SyncFailure.Connection));

        Assert.Equal(2, app.TrayIcon.Notifications.Count);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_successful_sync_lets_the_same_error_notify_again()
    {
        var app = new ShellHarness();
        app.Start();
        app.Sync.Publish(Error(SyncFailure.Authentication));

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Idle });
        app.Sync.Publish(Error(SyncFailure.Authentication));

        Assert.Equal(2, app.TrayIcon.Notifications.Count);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_tooltip_shows_the_app_name_then_the_last_successful_sync_in_local_time_then_a_long_error()
    {
        var app = new ShellHarness();
        app.Start();
        var last = new DateTimeOffset(2026, 3, 4, 10, 20, 0, TimeSpan.FromHours(14));
        var details = "Key rejected: " + new string('x', 300);

        app.Sync.Publish(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Connection, Details = details, LastSuccessfulSync = last
        });

        Assert.Equal(
            string.Join(Environment.NewLine, "Memoria-ToDo", $"Last synced {last.ToLocalTime():g}", details),
            app.TrayIcon.ToolTip);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_tooltip_describes_an_error_without_details_like_its_notification()
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Connection, Details = null });

        Assert.Equal(
            string.Join(Environment.NewLine, "Memoria-ToDo", "Sync failed: Connection."), app.TrayIcon.ToolTip);
        Assert.Equal("Sync failed: Connection.", Assert.Single(app.TrayIcon.Notifications));
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_tooltip_shows_offline_details_and_no_last_sync_line_before_the_first_success()
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Offline, Details = "No network" });

        Assert.Equal(string.Join(Environment.NewLine, "Memoria-ToDo", "No network"), app.TrayIcon.ToolTip);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_tooltip_of_a_working_sync_adds_no_details_line()
    {
        var app = new ShellHarness();
        app.Start();

        app.Sync.Publish(new FakeSyncStatus { State = SyncState.Working, Details = "Pulling" });

        Assert.Equal("Memoria-ToDo", app.TrayIcon.ToolTip);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    private static FakeSyncStatus Error(SyncFailure failure) =>
        new() { State = SyncState.Error, Failure = failure, Details = $"Failed: {failure}" };

    [Fact]
    public async Task A_sync_shutdown_failure_does_not_skip_other_cleanup()
    {
        var app = new ShellHarness();
        app.Sync.StopOperation = _ => Task.FromException(new IOException("Could not close transport"));
        app.Start();

        await Assert.ThrowsAsync<IOException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.False(app.TrayIcon.IsShown);
        Assert.False(app.OneInstance.IsClaimed);
        Assert.Equal(1, app.Configure.DisposeCount);
    }
}
