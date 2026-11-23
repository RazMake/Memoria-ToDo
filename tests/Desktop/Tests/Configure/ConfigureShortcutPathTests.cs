using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Configure screen's shortcut paths: picking a shortcut, closing with the Close window keys and opening again, Esc, and
/// the Save and Close window keys. Each test follows one whole flow. The rules, messages and registration order are
/// unit-tested on the shortcut settings (ShortcutSetting*Tests, ShortcutKeyInputTests, ShortcutFocusTests).
/// </summary>
[UX("Configure")]
[Collection(ScreenTests.Collection)]
public class ConfigureShortcutPathTests
{
    [WpfFact]
    public void Picking_a_new_shortcut_releases_the_global_shortcuts_while_the_box_has_the_focus_and_registers_the_pick_when_it_leaves()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.AddField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ConfigureAction.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ContactsAction.Calls);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);
        Assert.Contains(
            ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K)), screen.AddNewTask.Calls);
        Assert.Equal("", screen.AddMessage);

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.C);
        Assert.Contains("two or more modifier keys", screen.ManageMessage);
        Assert.Equal("Win+Alt+V", screen.ManageField.Text);

        screen.Chord(screen.ContactsField, Key.LeftCtrl, Key.LeftShift);
        Assert.Contains("Windows or another app uses that shortcut", screen.ContactsMessage);

        screen.Chord(screen.ConfigureField, Key.LWin, Key.LeftAlt, Key.O);
        Assert.Contains("Contacts", screen.ConfigureMessage);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);

        screen.FocusStartWithWindowsBox();

        Assert.Equal("Ctrl+Shift+K", screen.Store.Settings.AddNewTaskShortcut);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ContactsDefault), screen.ContactsAction.Calls[^1]);
        screen.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void The_Close_window_keys_close_Configure_from_anywhere_and_opening_it_again_registers_the_shortcuts_again()
    {
        var contacts = new FakeShortcutAction();
        using var screen = new ConfigureScreen(contacts: contacts);
        screen.Open();
        screen.FocusStartWithWindowsBox();

        SimulatedKey.Chord(screen.StartWithWindowsBox, Key.LeftShift, Key.Escape);
        Assert.False(screen.IsShowing);

        screen.Show();
        ConfigureScreen.Focus(screen.CloseField);
        var window = screen.Window;
        screen.Chord(screen.CloseField, Key.LeftShift, Key.Escape);

        Assert.False(screen.IsShowing);
        Assert.False(window.IsVisible);
        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ContactsDefault), contacts.Calls[^1]);

        contacts.Answer = _ => false;
        screen.Show();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
        Assert.Equal("", screen.ContactsField.Text);
        Assert.NotEqual("", screen.ContactsMessage);
    }

    [WpfFact]
    public void Esc_leaves_a_shortcut_box_first_then_closes_and_closes_a_menu_first_and_drops_what_was_typed_and_lets_a_sync_go_on()
    {
        using var screen = new ConfigureScreen();
        screen.Sync.SyncOperation = _ => new TaskCompletionSource<Memoria.ToDo.Shared.Sync.ISyncStatus>().Task;
        screen.Open();
        screen.FocusStartWithWindowsBox();

        screen.PressEscape(screen.StartWithWindowsBox);
        Assert.False(screen.IsShowing);

        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        screen.PressEscape(screen.AddField);

        Assert.True(screen.IsShowing);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.False(screen.AddField.IsKeyboardFocused);
        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        screen.PressEscape(Keyboard.FocusedElement as UIElement ?? screen.Window);
        Assert.False(screen.IsShowing);

        screen.Open();
        var menu = screen.OpenRepositoryMenu();
        screen.PressEscape(menu);
        Assert.False(menu.IsOpen);
        Assert.True(screen.IsShowing);
        OpenWindows.PressEscape(screen.Window);
        Assert.False(screen.IsShowing);

        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.ChooseRepositoryAction("Clone a repo");
        var address = screen.Box("Repository address");
        ConfigureScreen.Focus(address);
        address.Text = "git@example.com:tasks.git";
        screen.PressEscape(address);
        Assert.False(screen.IsShowing);

        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.ChooseRepositoryAction("Clone a repo");
        Assert.Equal("", screen.Box("Repository address").Text);
        Assert.Empty(screen.Sync.SetupControl.Clones);
        screen.Click("Sync");
        Assert.Equal(1, screen.Sync.SyncCount);
        OpenWindows.PressEscape(screen.Window);

        Assert.False(screen.IsShowing);
        Assert.Equal(0, screen.Sync.StopCount);
        Assert.Equal(0, screen.Sync.DisposeCount);
    }

    [WpfFact]
    public void Picking_Save_and_Close_window_keys_saves_and_shows_them_and_the_new_Close_window_keys_close_Configure_from_the_Save_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        Assert.Equal("Ctrl+Enter", screen.SaveField.Text);

        ConfigureScreen.Focus(screen.SaveField);
        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);

        screen.Chord(screen.SaveField, Key.LeftShift, Key.A);
        Assert.Equal(TestSaveSetting.ShiftAloneMessage, screen.SaveMessage);
        Assert.True(screen.SaveKeys.Matches(ModifierKeys.Control, Key.Enter));

        screen.Chord(screen.SaveField, Key.LeftCtrl, Key.LeftAlt, Key.K);
        Assert.Equal("Ctrl+Alt+K", screen.SaveField.Text);
        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.SaveShortcut);
        Assert.True(screen.SaveKeys.Matches(ModifierKeys.Control | ModifierKeys.Alt, Key.K));
        Assert.False(screen.SaveKeys.Matches(ModifierKeys.Control, Key.Enter));

        screen.FocusStartWithWindowsBox();
        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);

        screen.Chord(screen.CloseField, Key.LeftCtrl, Key.LeftAlt, Key.J);
        screen.FocusStartWithWindowsBox();
        Assert.Equal("Ctrl+Alt+J", screen.Store.Settings.CloseWindowShortcut);
        Assert.Equal("Ctrl+Alt+J", screen.CloseField.Text);

        screen.Chord(screen.SaveField, Key.LeftCtrl, Key.LeftAlt, Key.J);

        Assert.False(screen.IsShowing);
        screen.Log.Events.AssertNoProblems();
    }
}
