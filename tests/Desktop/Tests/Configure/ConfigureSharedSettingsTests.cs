using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Configure on top of the shared settings: the common paths and the dispatcher wiring. The shortcut rules are
/// unit-tested on ShortcutSetting (ShortcutSettingSettingsChangedTests, ShortcutSettingPickTests).
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
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
    public void A_valid_Close_window_pick_is_saved_in_shared_settings_and_shown()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.CloseField, Key.LeftCtrl, Key.LeftAlt, Key.K);
        screen.FocusStartWithWindowsBox();

        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.CloseWindowShortcut);
        Assert.Equal("Ctrl+Alt+K", screen.CloseField.Text);
    }

    [WpfFact]
    public void Stored_Close_window_text_is_loaded_when_Configure_is_created()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetCloseWindowShortcut("Ctrl+Alt+K"));
        screen.Open();

        Assert.Equal("Ctrl+Alt+K", screen.CloseField.Text);
        screen.Chord(screen.CloseField, Key.LeftCtrl, Key.LeftAlt, Key.K);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public async Task A_Close_window_change_in_the_settings_applies_while_the_window_is_closed()
    {
        using var screen = new ConfigureScreen();

        await Task.Run(() => screen.Store.Settings.SetCloseWindowShortcut("Ctrl+Alt+K"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        screen.Open();

        Assert.Equal("Ctrl+Alt+K", screen.CloseField.Text);
        screen.Chord(screen.CloseField, Key.LeftCtrl, Key.LeftAlt, Key.K);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Invalid_stored_Close_window_text_keeps_the_default_says_why_and_is_not_repaired()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetCloseWindowShortcut("not a Windows shortcut"));
        screen.Open();

        Assert.Equal("Shift+Escape", screen.CloseField.Text);
        Assert.Contains("not a Windows shortcut", screen.CloseMessage);
        Assert.Equal("not a Windows shortcut", screen.Store.Settings.CloseWindowShortcut);
    }

    [WpfFact]
    public void Editing_the_deletion_delay_saves_it_in_shared_settings()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.ClickTitle("Sync settings");
        var field = screen.Box("Deletion delay (days)");
        ConfigureScreen.Focus(field);
        field.Text = "30";

        screen.FocusStartWithWindowsBox();

        Assert.Equal(30, screen.Store.Settings.DeletionDelayDays);
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
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Empty(screen.ManageTasks.Calls);
    }
}
