using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
public class ConfigureSharedSettingsTests
{
    [WpfFact]
    public void Stored_shortcut_text_is_applied_when_shortcut_actions_connect()
    {
        var add = new FakeShortcutAction();
        using var screen = new ConfigureScreen(addNewTask: add,
            initialSettings: settings => settings.SetAddNewTaskShortcut("Ctrl+Alt+K"));
        screen.Open();

        Assert.Equal("Ctrl+Alt+K", screen.AddField.Text);
        Assert.Equal(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K), add.Registered);
        Assert.True(screen.Connected);
    }

    [WpfFact]
    public void A_valid_local_shortcut_pick_is_saved_in_shared_settings()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);
        screen.FocusStartWithWindowsBox();

        Assert.Equal("Ctrl+Shift+K", screen.Store.Settings.AddNewTaskShortcut);
    }

    [WpfFact]
    public void Imported_shortcuts_apply_even_while_Configure_is_closed()
    {
        using var screen = new ConfigureScreen();

        screen.Store.Settings.SetManageTasksShortcut("Ctrl+Alt+K");
        Settle();

        Assert.Equal(ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K)),
            screen.ManageTasks.Calls.Last());
    }

    [WpfFact]
    public void Invalid_imported_text_is_preserved_and_keeps_the_previous_valid_registration()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetAddNewTaskShortcut("Ctrl+Alt+K"));

        screen.Store.Settings.SetAddNewTaskShortcut("not a Windows shortcut");
        Settle();
        screen.Open();

        Assert.Equal("not a Windows shortcut", screen.Store.Settings.AddNewTaskShortcut);
        Assert.Equal("Ctrl+Alt+K", screen.AddField.Text);
        Assert.Equal(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K), screen.AddNewTask.Registered);
        Assert.NotEmpty(screen.AddMessage);
        Assert.DoesNotContain(screen.AddNewTask.Calls, call => call.IsUnregister);
    }

    [WpfFact]
    public void An_unrelated_settings_change_keeps_a_rejected_pick_message()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);
        Assert.NotEmpty(screen.AddMessage);

        screen.Store.Settings.SetDeletionDelayDays(10);
        Settle();

        Assert.NotEmpty(screen.AddMessage);
    }

    [WpfFact]
    public void A_valid_import_that_matches_the_active_shortcut_clears_the_invalid_text_message()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetAddNewTaskShortcut("Ctrl+Alt+K"));
        screen.Store.Settings.SetAddNewTaskShortcut("not a Windows shortcut");
        Settle();
        screen.Open();
        Assert.NotEmpty(screen.AddMessage);

        screen.Store.Settings.SetAddNewTaskShortcut("Ctrl+Alt+K");
        Settle();

        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void A_notice_during_shortcut_editing_does_not_register_released_keys()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        screen.AddNewTask.ClearCalls();

        screen.Store.Settings.SetAddNewTaskShortcut("Ctrl+Alt+K");
        Settle();

        Assert.DoesNotContain(screen.AddNewTask.Calls, call => !call.IsUnregister);
        screen.FocusStartWithWindowsBox();
        Assert.Equal(ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K)),
            screen.AddNewTask.Calls.Last());
    }

    [WpfFact]
    public void A_failed_shortcut_save_restores_its_prior_text_and_registration()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetAddNewTaskShortcut("Ctrl+Alt+K"));
        screen.Open();
        screen.Files.FailWritesUnder(@"C:\Configure tests\clone");

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.J);
        screen.FocusStartWithWindowsBox();

        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.AddNewTaskShortcut);
        Assert.Equal("Ctrl+Alt+K", screen.AddField.Text);
        Assert.Equal(ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K)),
            screen.AddNewTask.Calls.Last());
        Assert.NotEmpty(screen.AddMessage);
    }

    [WpfFact]
    public void Editing_the_deletion_delay_saves_it_in_shared_settings()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Sync settings");
        var field = screen.Box("Deletion delay (days)");
        ConfigureScreen.Focus(field);
        field.Text = "30";

        screen.FocusStartWithWindowsBox();

        Assert.Equal(30, screen.Store.Settings.DeletionDelayDays);
    }

    [WpfTheory]
    [InlineData("0")]
    [InlineData("366")]
    [InlineData("many")]
    public void Invalid_deletion_delays_leave_the_stored_value_unchanged(string text)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Sync settings");
        var field = screen.Box("Deletion delay (days)");
        ConfigureScreen.Focus(field);
        field.Text = text;

        screen.FocusStartWithWindowsBox();

        Assert.Equal(7, screen.Store.Settings.DeletionDelayDays);
        Assert.Equal("7", field.Text);
    }

    [WpfFact]
    public async Task Settings_notices_from_a_worker_thread_update_shortcuts()
    {
        using var screen = new ConfigureScreen();

        await Task.Run(() => screen.Store.Settings.SetManageTasksShortcut("Ctrl+Alt+K"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.Equal(ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K)),
            screen.ManageTasks.Calls.Last());
    }

    [WpfFact]
    public void Disposing_Configure_detaches_settings_notices()
    {
        var screen = new ConfigureScreen();
        screen.Dispose();
        screen.AddNewTask.ClearCalls();
        screen.ManageTasks.ClearCalls();

        screen.Store.Settings.SetManageTasksShortcut("Ctrl+Alt+K");
        Settle();

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Empty(screen.ManageTasks.Calls);
    }

    private static void Settle() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
}
