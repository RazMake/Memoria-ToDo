using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>What Shell logs, by the logging design's table: each use at Information, a shortcut another app holds at Warning.</summary>
[Unit]
public class TrayAppLoggingTests
{
    private static ShellHarness Started(bool available = true)
    {
        var app = new ShellHarness();
        app.Contacts.IsAvailable = available;
        app.Start();
        return app;
    }

    [Theory]
    [InlineData("Add new task")]
    [InlineData("Task View")]
    [InlineData("Contacts")]
    [InlineData("Configure")]
    [InlineData("Logs")]
    [InlineData("Exit")]
    public void Choosing_a_tray_menu_item_logs_it_at_Information(string item)
    {
        var app = Started();
        var before = app.Log.Events.Count;

        app.TrayIcon.ChooseMenuItem(item);

        var entries = app.Log.After(before);
        entries.SingleAt(LogEventLevel.Information, item);
        entries.AssertNoProblems();
    }

    [Fact]
    public void A_left_click_on_the_icon_logs_at_Information()
    {
        var app = Started();
        var before = app.Log.Events.Count;

        app.TrayIcon.LeftClick();

        var entries = app.Log.After(before);
        Assert.Single(entries.At(LogEventLevel.Information));
        entries.AssertNoProblems();
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    [InlineData(ShortcutKind.Configure)]
    [InlineData(ShortcutKind.Contacts)]
    public void Using_a_global_shortcut_logs_it_at_Information(ShortcutKind kind)
    {
        var app = Started();
        app.ActionOf(kind).Register(TestShortcut.A);
        var before = app.Log.Events.Count;

        app.GlobalShortcut.Press(TestShortcut.A);

        var entries = app.Log.After(before);
        Assert.Single(entries.At(LogEventLevel.Information));
        entries.AssertNoProblems();
    }

    [Fact]
    public void A_later_start_handing_over_to_the_running_app_logs_at_Information()
    {
        var app = Started();
        var before = app.Log.Events.Count;

        app.OneInstance.StartLaterInstance();

        Assert.NotEmpty(app.Log.After(before).At(LogEventLevel.Information));
        app.Log.After(before).AssertNoProblems();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Contacts_becoming_unavailable_or_available_logs_each_change_at_Information(bool becomesAvailable)
    {
        var app = Started(available: !becomesAvailable);
        var before = app.Log.Events.Count;

        app.Contacts.IsAvailable = becomesAvailable;

        Assert.Single(app.Log.After(before).At(LogEventLevel.Information));
        app.Log.After(before).AssertNoProblems();
    }

    [Fact]
    public void Each_sync_notification_shown_logs_at_Information_with_what_it_says()
    {
        var app = Started();
        var before = app.Log.Events.Count;

        app.Sync.Publish(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Authentication, Details = "Key rejected by the host"
        });

        Assert.Single(app.TrayIcon.Notifications);
        app.Log.After(before).SingleAt(LogEventLevel.Information, "Key rejected by the host");
    }

    [Fact]
    public void A_sync_error_that_shows_no_new_notification_logs_no_second_notification_entry()
    {
        var app = Started();
        var error = new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Authentication, Details = "Key rejected by the host"
        };
        app.Sync.Publish(error);
        var before = app.Log.Events.Count;

        app.Sync.Publish(error);

        Assert.Single(app.TrayIcon.Notifications);
        Assert.DoesNotContain(app.Log.After(before), entry => entry.Names("Key rejected by the host"));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    [InlineData(ShortcutKind.Configure)]
    [InlineData(ShortcutKind.Contacts)]
    public void A_shortcut_another_app_holds_logs_a_Warning(ShortcutKind kind)
    {
        var app = Started();
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);
        var before = app.Log.Events.Count;

        var registered = app.ActionOf(kind).Register(TestShortcut.A);

        Assert.False(registered);
        Assert.Single(app.Log.After(before).At(LogEventLevel.Warning));
    }

    [Fact]
    public void A_shortcut_that_registers_logs_no_Warning()
    {
        var app = Started();
        var before = app.Log.Events.Count;

        app.Configure.AddNewTask.Register(TestShortcut.A);

        app.Log.After(before).AssertNoProblems();
    }

    [Fact]
    public void The_Contacts_shortcut_another_app_holds_when_contacts_become_available_logs_a_Warning()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);
        var before = app.Log.Events.Count;

        app.Contacts.IsAvailable = true;

        Assert.Single(app.Log.After(before).At(LogEventLevel.Warning));
    }
}
