using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Close window setting: loaded when Configure is created, kept current from the shared settings, and changed by
/// picks. It hands each active keys to the app's windows; invalid stored text is reported and never repaired.
/// </summary>
[Unit]
public class CloseWindowSettingTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    private const string InvalidText = "not a Windows shortcut";
    private const string InvalidMessage = "The saved Close window shortcut \"not a Windows shortcut\" isn't a valid shortcut.";

    private static readonly Shortcut CtrlAltK = TestCloseWindowSetting.Make(Ctrl | Alt, Key.K);

    private readonly TestCloseWindowSetting row = new();
    private readonly TestShortcutSetting add = TestShortcutSetting.AddNewTask().Connected();
    private readonly TestShortcutSetting manage = TestShortcutSetting.ManageTasks().Connected();

    private void Pick(ModifierKeys modifiers, Key key) => row.Setting.Pick(modifiers, key, add.Setting, manage.Setting);

    // Creating it.

    [Fact]
    public void Starts_with_Shift_plus_Esc_and_applies_it_when_nothing_is_stored()
    {
        row.Create();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal([(Shift, Key.Escape)], row.Applied);
        Assert.Null(row.Setting.Message);
        Assert.Equal("Close window", row.Setting.Label);
    }

    [Fact]
    public void Applies_the_stored_keys_instead_of_the_default()
    {
        row.Stored = "Ctrl+Alt+K";

        row.Create();

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.Equal([(Ctrl | Alt, Key.K)], row.Applied);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Accepts_stored_keys_that_only_Close_window_may_use()
    {
        row.Stored = "Ctrl+Alt+F12";

        row.Create();

        Assert.Equal([(Ctrl | Alt, Key.F12)], row.Applied);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        row.Stored = InvalidText;

        row.Create();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal([(Shift, Key.Escape)], row.Applied);
        Assert.Equal(InvalidMessage, row.Setting.Message);
        Assert.Empty(row.Saved);
        Assert.Equal(InvalidText, row.Stored);
    }

    [Fact]
    public void Uses_the_default_when_the_stored_text_breaks_the_Close_window_rules()
    {
        row.Stored = "Ctrl+K";

        row.Create();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal("The saved Close window shortcut \"Ctrl+K\" isn't a valid shortcut.", row.Setting.Message);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_cannot_be_read()
    {
        row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        row.Create();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal([(Shift, Key.Escape)], row.Applied);
        Assert.Equal(
            "The saved Close window shortcut couldn't be read: The settings file is damaged.", row.Setting.Message);
    }

    // The shared settings change.

    [Fact]
    public void Applies_changed_stored_keys()
    {
        row.Create();
        row.Stored = "Ctrl+Alt+K";

        row.Setting.SettingsChanged();

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.Equal((Ctrl | Alt, Key.K), row.Applied[^1]);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Applies_the_default_when_the_stored_text_is_removed()
    {
        row.Stored = "Ctrl+Alt+K";
        row.Create();
        row.Stored = null;

        row.Setting.SettingsChanged();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal((Shift, Key.Escape), row.Applied[^1]);
    }

    [Fact]
    public void Keeps_the_previous_keys_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        row.Stored = "Ctrl+Alt+K";
        row.Create();
        row.Applied.Clear();
        row.Stored = InvalidText;

        row.Setting.SettingsChanged();

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.DoesNotContain(row.Applied, keys => keys != (Ctrl | Alt, Key.K));
        Assert.Equal(InvalidMessage, row.Setting.Message);
        Assert.Empty(row.Saved);
        Assert.Equal(InvalidText, row.Stored);
    }

    [Fact]
    public void Keeps_the_previous_keys_and_says_why_when_the_stored_text_cannot_be_read()
    {
        row.Create();
        row.Applied.Clear();
        row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        row.Setting.SettingsChanged();

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.DoesNotContain(row.Applied, keys => keys != (Shift, Key.Escape));
        Assert.Equal(
            "The saved Close window shortcut couldn't be read: The settings file is damaged.", row.Setting.Message);
    }

    [Fact]
    public void A_later_valid_stored_text_clears_the_message_about_the_stored_text()
    {
        row.Stored = InvalidText;
        row.Create();
        row.Stored = "Ctrl+Alt+K";

        row.Setting.SettingsChanged();

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Keeps_the_message_about_a_rejected_pick()
    {
        row.Create();
        Pick(Ctrl, Key.K);

        row.Stored = "Ctrl+Alt+K";
        row.Setting.SettingsChanged();

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.Equal(TestCloseWindowSetting.RuleMessage, row.Setting.Message);
    }

    // A valid pick.

    [Fact]
    public void A_valid_pick_is_applied_saved_as_its_display_text_and_becomes_active()
    {
        row.Create();

        Pick(Ctrl | Alt, Key.K);

        Assert.Equal(CtrlAltK, row.Setting.Active);
        Assert.Equal((Ctrl | Alt, Key.K), row.Applied[^1]);
        Assert.Equal(["Ctrl+Alt+K"], row.Saved);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void A_pick_of_one_modifier_plus_a_function_key_is_valid()
    {
        row.Create();

        Pick(Ctrl, Key.F5);

        Assert.Equal(["Ctrl+F5"], row.Saved);
        Assert.Equal((Ctrl, Key.F5), row.Applied[^1]);
    }

    [Fact]
    public void A_valid_pick_clears_the_message_about_an_earlier_rejected_pick()
    {
        row.Create();
        Pick(Ctrl, Key.K);

        Pick(Ctrl | Alt, Key.K);

        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void A_valid_pick_does_not_touch_the_global_shortcuts()
    {
        row.Create();

        Pick(Ctrl | Alt, Key.K);

        Assert.Empty(add.Action.Calls);
        Assert.Empty(manage.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Empty(manage.Saved);
    }

    // A pick that breaks a rule.

    [Theory]
    [InlineData(ModifierKeys.None, Key.K, TestCloseWindowSetting.RuleMessage)]
    [InlineData(Ctrl, Key.K, TestCloseWindowSetting.RuleMessage)]
    [InlineData(ModifierKeys.None, Key.Escape, TestCloseWindowSetting.RuleMessage)]
    [InlineData(Ctrl | Alt, Key.LeftShift, "Add a key besides the modifier keys.")]
    public void A_pick_that_breaks_a_rule_shows_which_one_and_changes_nothing(
        ModifierKeys modifiers, Key key, string message)
    {
        row.Create();
        row.Applied.Clear();

        Pick(modifiers, key);

        Assert.Equal(message, row.Setting.Message);
        Assert.Empty(row.Applied);
        Assert.Empty(row.Saved);
        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
    }

    // A pick another row uses, or its own keys.

    [Fact]
    public void A_pick_equal_to_the_Add_new_task_shortcut_names_that_row_and_changes_nothing()
    {
        row.Create();
        row.Applied.Clear();

        Pick(Win | Alt, Key.N);

        Assert.Equal("Add new task already uses Win+Alt+N. Press a different shortcut.", row.Setting.Message);
        Assert.Empty(row.Applied);
        Assert.Empty(row.Saved);
        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
    }

    [Fact]
    public void A_pick_equal_to_the_Task_View_shortcut_names_that_row()
    {
        row.Create();

        Pick(Win | Alt, Key.V);

        Assert.Equal("Task View already uses Win+Alt+V. Press a different shortcut.", row.Setting.Message);
    }

    [Fact]
    public void A_pick_equal_to_a_global_shortcut_the_user_changed_is_refused()
    {
        row.Create();
        add.Setting.Pick(Ctrl | Shift, Key.K, manage.Setting);

        Pick(Ctrl | Shift, Key.K);

        Assert.Equal("Add new task already uses Ctrl+Shift+K. Press a different shortcut.", row.Setting.Message);
    }

    [Fact]
    public void A_pick_equal_to_a_row_that_has_no_shortcut_is_not_refused()
    {
        var empty = TestShortcutSetting.AddNewTask();
        empty.Action.Answer = _ => false;
        empty.Connected();
        row.Create();

        row.Setting.Pick(Win | Alt, Key.N, empty.Setting, manage.Setting);

        Assert.Equal(TestCloseWindowSetting.Make(Win | Alt, Key.N), row.Setting.Active);
    }

    [Fact]
    public void A_pick_equal_to_its_own_keys_changes_nothing_and_leaves_its_message_as_it_was()
    {
        row.Create();
        Pick(Ctrl, Key.K);
        row.Applied.Clear();

        Pick(Shift, Key.Escape);

        Assert.Empty(row.Applied);
        Assert.Empty(row.Saved);
        Assert.Equal(TestCloseWindowSetting.RuleMessage, row.Setting.Message);
    }

    // A pick that can't be saved.

    [Fact]
    public void A_pick_that_cannot_be_saved_goes_back_to_the_previous_keys_and_says_what_failed()
    {
        row.Create();
        row.SaveFailure = new IOException("The disk is full.");

        Pick(Ctrl | Alt, Key.K);

        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
        Assert.Equal((Shift, Key.Escape), row.Applied[^1]);
        Assert.Equal("Ctrl+Alt+K wasn't saved for Close window: The disk is full.", row.Setting.Message);
        Assert.Empty(row.Saved);
    }
}
