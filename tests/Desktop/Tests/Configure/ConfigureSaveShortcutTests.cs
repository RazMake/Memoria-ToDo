using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Save row in Configure's window, and the platform's Save keys it sets. The Save rules, messages, and
/// settings changes are unit-tested (<see cref="SaveSettingTests"/>, <see cref="ShortcutSaveTests"/>); this checks the
/// wiring: the row, the other rows refusing its keys, the keys the app's windows get, and the shared settings.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureSaveShortcutTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    [WpfFact]
    public void The_Save_keys_are_Ctrl_plus_Enter_as_soon_as_Configure_is_created()
    {
        using var screen = new ConfigureScreen();

        Assert.True(screen.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void The_Save_box_shows_the_Save_keys()
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetSaveShortcut("Ctrl+Alt+K"));
        screen.Open();

        Assert.Equal("Ctrl+Alt+K", screen.SaveField.Text);
        Assert.Equal("", screen.SaveMessage);
    }

    [WpfFact]
    public void Stored_Save_text_is_applied_to_the_Save_keys_when_Configure_is_created()
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetSaveShortcut("Ctrl+Alt+K"));

        Assert.True(screen.SaveKeys.Matches(Ctrl | Alt, Key.K));
        Assert.False(screen.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void A_valid_Save_pick_is_saved_in_shared_settings_shown_and_given_to_the_Save_keys_at_once()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.SaveField, Key.LeftCtrl, Key.LeftAlt, Key.K);

        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.SaveShortcut);
        Assert.Equal("Ctrl+Alt+K", screen.SaveField.Text);
        Assert.True(screen.SaveKeys.Matches(Ctrl | Alt, Key.K));
        Assert.False(screen.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void A_Save_pick_that_breaks_a_rule_shows_the_rule_and_keeps_the_keys()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.SaveField, Key.LeftShift, Key.A);

        Assert.Equal(TestSaveSetting.ShiftAloneMessage, screen.SaveMessage);
        Assert.Null(screen.Store.Settings.SaveShortcut);
        Assert.True(screen.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void Focusing_the_Save_box_releases_the_global_shortcuts_and_leaving_it_restores_them_without_a_Save_registration()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.SaveField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);

        screen.Chord(screen.SaveField, Key.LeftCtrl, Key.LeftAlt, Key.K);
        screen.FocusStartWithWindowsBox();

        Assert.Equal(
            [ShortcutCall.Unregister(), ShortcutCall.Register(TestShortcut.AddNewTaskDefault)],
            screen.AddNewTask.Calls);
        Assert.Equal(
            [ShortcutCall.Unregister(), ShortcutCall.Register(TestShortcut.ManageTasksDefault)],
            screen.ManageTasks.Calls);
    }

    [WpfFact]
    public void A_Save_pick_equal_to_a_global_shortcut_names_that_row_and_keeps_the_keys()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.SaveField, Key.LWin, Key.LeftAlt, Key.N);

        Assert.Equal("Add new task already uses Win+Alt+N. Press a different shortcut.", screen.SaveMessage);
        Assert.True(screen.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void The_Close_window_keys_pressed_in_the_Save_box_close_the_window_instead_of_being_picked()
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetCloseWindowShortcut("Ctrl+Alt+K"));
        screen.Open();

        screen.Chord(screen.SaveField, Key.LeftCtrl, Key.LeftAlt, Key.K);

        Assert.False(screen.IsShowing);
        Assert.Null(screen.Store.Settings.SaveShortcut);
    }

    [WpfTheory]
    [InlineData("Add new task")]
    [InlineData("Task View")]
    [InlineData("Close window")]
    public void Each_other_row_refuses_a_pick_equal_to_the_Save_keys_and_names_Save(string row)
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetSaveShortcut("Ctrl+Alt+K"));
        screen.Open();
        var field = row switch
        {
            "Add new task" => screen.AddField,
            "Task View" => screen.ManageField,
            _ => screen.CloseField,
        };
        Func<string> message = row switch
        {
            "Add new task" => () => screen.AddMessage,
            "Task View" => () => screen.ManageMessage,
            _ => () => screen.CloseMessage,
        };

        screen.Chord(field, Key.LeftCtrl, Key.LeftAlt, Key.K);

        Assert.Equal("Save already uses Ctrl+Alt+K. Press a different shortcut.", message());
        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.SaveShortcut);
    }

    [WpfFact]
    public async Task A_Save_change_in_the_settings_applies_to_the_Save_keys_while_the_window_is_closed()
    {
        using var screen = new ConfigureScreen();

        await Task.Run(() => screen.Store.Settings.SetSaveShortcut("Ctrl+Alt+K"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.True(screen.SaveKeys.Matches(Ctrl | Alt, Key.K));
        screen.Open();
        Assert.Equal("Ctrl+Alt+K", screen.SaveField.Text);
    }

    [WpfFact]
    public async Task An_invalid_Save_change_in_the_settings_keeps_the_previous_keys_and_is_not_repaired()
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetSaveShortcut("Ctrl+Alt+K"));

        await Task.Run(() => screen.Store.Settings.SetSaveShortcut("not a Windows shortcut"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.True(screen.SaveKeys.Matches(Ctrl | Alt, Key.K));
        screen.Open();
        Assert.Contains("not a Windows shortcut", screen.SaveMessage);
        Assert.Equal("not a Windows shortcut", screen.Store.Settings.SaveShortcut);
    }

    [WpfFact]
    public void Invalid_stored_Save_text_keeps_Ctrl_plus_Enter_says_why_and_is_not_repaired()
    {
        using var screen = new ConfigureScreen(
            initialSettings: settings => settings.SetSaveShortcut("not a Windows shortcut"));
        screen.Open();

        Assert.True(screen.SaveKeys.Matches(Ctrl, Key.Enter));
        Assert.Contains("not a Windows shortcut", screen.SaveMessage);
        Assert.Equal("not a Windows shortcut", screen.Store.Settings.SaveShortcut);
    }
}
