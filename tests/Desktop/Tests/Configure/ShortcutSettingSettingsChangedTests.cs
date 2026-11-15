using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A shortcut setting when the shared settings change, also while the window is closed: the stored text is applied,
/// and text that isn't a usable shortcut is reported and never replaces the shortcut the row has.
/// </summary>
[Unit]
public class ShortcutSettingSettingsChangedTests
{
    private const string InvalidText = "not a Windows shortcut";
    private const string InvalidMessage = "The saved Add new task shortcut \"not a Windows shortcut\" isn't a valid shortcut.";

    private readonly TestShortcutSetting row = TestShortcutSetting.AddNewTask();

    [Fact]
    public void Does_nothing_before_the_action_is_connected()
    {
        row.Stored = "Ctrl+Alt+K";

        var failure = Record.Exception(row.Setting.SettingsChanged);

        Assert.Null(failure);
        Assert.Empty(row.Action.Calls);
        Assert.Null(row.Setting.Active);
    }

    [Fact]
    public void Registers_a_changed_stored_shortcut()
    {
        row.Connected();
        row.Stored = "Ctrl+Alt+K";

        row.Setting.SettingsChanged();

        Assert.Equal([ShortcutCall.Register(TestShortcut.A)], row.Action.Calls);
        Assert.Equal(TestShortcut.A, row.Setting.Active);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Registers_the_default_when_the_stored_text_is_removed()
    {
        row.Stored = "Ctrl+Alt+K";
        row.Connected();
        row.Stored = null;

        row.Setting.SettingsChanged();

        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], row.Action.Calls);
    }

    [Fact]
    public void Registers_nothing_when_the_stored_shortcut_is_already_active()
    {
        row.Connected();
        row.Stored = "Win+Alt+N";

        row.Setting.SettingsChanged();

        Assert.Empty(row.Action.Calls);
    }

    [Fact]
    public void Keeps_the_active_shortcut_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        row.Stored = "Ctrl+Alt+K";
        row.Connected();
        row.Stored = InvalidText;

        row.Setting.SettingsChanged();

        Assert.Equal(TestShortcut.A, row.Setting.Active);
        Assert.Empty(row.Action.Calls);
        Assert.Equal(InvalidMessage, row.Setting.Message);
        Assert.Empty(row.Saved);
    }

    [Fact]
    public void Keeps_the_active_shortcut_and_says_why_when_the_stored_text_cannot_be_read()
    {
        row.Connected();
        row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        row.Setting.SettingsChanged();

        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Empty(row.Action.Calls);
        Assert.Equal(
            "The saved Add new task shortcut couldn't be read: The settings file is damaged.", row.Setting.Message);
    }

    [Fact]
    public void Tries_the_default_when_the_row_has_no_shortcut_and_the_stored_text_is_not_a_shortcut()
    {
        row.Action.Answer = _ => false;
        row.Connected();
        row.Action.Answer = _ => true;
        row.Stored = InvalidText;

        row.Setting.SettingsChanged();

        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], row.Action.Calls);
        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Equal(InvalidMessage, row.Setting.Message);
    }

    [Fact]
    public void Keeps_the_previous_shortcut_and_says_so_when_the_stored_shortcut_does_not_register()
    {
        row.Connected();
        row.Action.Answer = _ => false;
        row.Stored = "Ctrl+Alt+K";

        row.Setting.SettingsChanged();

        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Equal("Windows or another app already uses Ctrl+Alt+K. Press a different shortcut.", row.Setting.Message);
    }

    [Fact]
    public void Keeps_the_message_about_a_rejected_pick()
    {
        row.Connected();
        row.Setting.Pick(ModifierKeys.Control, Key.C, TestShortcutSetting.ManageTasks().Setting);

        row.Setting.SettingsChanged();

        Assert.NotNull(row.Setting.Message);
    }

    [Fact]
    public void Clears_the_message_about_the_stored_text_when_valid_text_matches_the_active_shortcut()
    {
        row.Stored = "Ctrl+Alt+K";
        row.Connected();
        row.Stored = InvalidText;
        row.Setting.SettingsChanged();

        row.Stored = "Ctrl+Alt+K";
        row.Setting.SettingsChanged();

        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Clears_the_message_about_the_stored_text_when_a_valid_new_shortcut_registers()
    {
        row.Connected();
        row.Stored = InvalidText;
        row.Setting.SettingsChanged();

        row.Stored = "Ctrl+Shift+J";
        row.Setting.SettingsChanged();

        Assert.Equal(TestShortcut.B, row.Setting.Active);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Only_activates_the_stored_shortcut_while_a_field_has_released_it_and_registers_it_on_restore()
    {
        row.Connected();
        row.Setting.Release();
        row.Stored = "Ctrl+Alt+K";

        row.Setting.SettingsChanged();

        Assert.Equal([ShortcutCall.Unregister()], row.Action.Calls);
        Assert.Equal(TestShortcut.A, row.Setting.Active);

        row.Setting.Restore();

        Assert.Equal([ShortcutCall.Unregister(), ShortcutCall.Register(TestShortcut.A)], row.Action.Calls);
    }
}
