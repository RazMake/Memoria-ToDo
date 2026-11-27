using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What every shortcut that never registers with Windows does, Close window and Save alike: loaded when Configure is
/// created, kept current from the shared settings, changed by picks, handed to the app's windows at each change.
/// Invalid stored text is reported and never repaired. Each subclass sets the row's name, default, and rules.
/// </summary>
[Unit]
public abstract class WindowKeysSettingTests
{
    protected const ModifierKeys Ctrl = ModifierKeys.Control;
    protected const ModifierKeys Alt = ModifierKeys.Alt;
    protected const ModifierKeys Shift = ModifierKeys.Shift;
    protected const ModifierKeys Win = ModifierKeys.Windows;

    private const string InvalidText = "not a Windows shortcut";

    private readonly TestShortcutSetting add = TestShortcutSetting.AddNewTask().Connected();
    private readonly TestShortcutSetting manage = TestShortcutSetting.ManageTasks().Connected();
    private bool siblingMade;

    /// <summary>The row under test.</summary>
    private protected abstract TestWindowKeysSetting Row { get; }

    /// <summary>The other window-only row, which a pick must differ from.</summary>
    private protected abstract TestWindowKeysSetting Sibling { get; }

    /// <summary>The row's name.</summary>
    protected abstract string Name { get; }

    /// <summary>The keys the row starts with.</summary>
    protected abstract Shortcut Default { get; }

    /// <summary>Makes a shortcut through the row's rules.</summary>
    protected abstract Shortcut Make(ModifierKeys modifiers, Key key);

    /// <summary>Stored text that is a shortcut as written but breaks the row's rules.</summary>
    protected abstract string RuleBreakingText { get; }

    /// <summary>A pick that breaks the row's rules, and the message that says so.</summary>
    protected abstract (ModifierKeys Modifiers, Key Key, string Message) RuleBreakingPick { get; }

    private Shortcut CtrlAltK => Make(Ctrl | Alt, Key.K);

    private string InvalidMessage => $"The saved {Name} shortcut \"{InvalidText}\" isn't a valid shortcut.";

    private void Pick(ModifierKeys modifiers, Key key) =>
        Row.Keys.Pick(modifiers, key, add.Setting, manage.Setting, SiblingKeys());

    // The other window-only row, made once from the text stored in it now.
    private IShortcutSetting SiblingKeys()
    {
        if (!siblingMade)
        {
            Sibling.Create();
            siblingMade = true;
        }

        return Sibling.Keys;
    }

    // Creating it.

    [Fact]
    public void Starts_with_its_default_and_applies_it_when_nothing_is_stored()
    {
        Row.Create();

        Assert.Equal(Name, Row.Keys.Label);
        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal([(Default.Modifiers, Default.Key)], Row.Applied);
        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void Applies_the_stored_keys_instead_of_the_default()
    {
        Row.Stored = "Ctrl+Alt+K";

        Row.Create();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Equal([(Ctrl | Alt, Key.K)], Row.Applied);
        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        Row.Stored = InvalidText;

        Row.Create();

        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal([(Default.Modifiers, Default.Key)], Row.Applied);
        Assert.Equal(InvalidMessage, Row.Keys.Message);
        Assert.Empty(Row.Saved);
        Assert.Equal(InvalidText, Row.Stored);
    }

    [Fact]
    public void Uses_the_default_when_the_stored_text_breaks_the_rules()
    {
        Row.Stored = RuleBreakingText;

        Row.Create();

        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal($"The saved {Name} shortcut \"{RuleBreakingText}\" isn't a valid shortcut.", Row.Keys.Message);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_cannot_be_read()
    {
        Row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        Row.Create();

        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal([(Default.Modifiers, Default.Key)], Row.Applied);
        Assert.Equal(
            $"The saved {Name} shortcut couldn't be read: The settings file is damaged.", Row.Keys.Message);
    }

    // The shared settings change.

    [Fact]
    public void Applies_changed_stored_keys()
    {
        Row.Create();
        Row.Stored = "Ctrl+Alt+K";

        Row.SettingsChanged();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Equal((Ctrl | Alt, Key.K), Row.Applied[^1]);
        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void Applies_the_default_when_the_stored_text_is_removed()
    {
        Row.Stored = "Ctrl+Alt+K";
        Row.Create();
        Row.Stored = null;

        Row.SettingsChanged();

        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal((Default.Modifiers, Default.Key), Row.Applied[^1]);
    }

    [Fact]
    public void Keeps_the_previous_keys_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        Row.Stored = "Ctrl+Alt+K";
        Row.Create();
        Row.Applied.Clear();
        Row.Stored = InvalidText;

        Row.SettingsChanged();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.DoesNotContain(Row.Applied, keys => keys != (Ctrl | Alt, Key.K));
        Assert.Equal(InvalidMessage, Row.Keys.Message);
        Assert.Empty(Row.Saved);
        Assert.Equal(InvalidText, Row.Stored);
    }

    [Fact]
    public void Keeps_the_previous_keys_when_the_stored_text_breaks_the_rules()
    {
        Row.Stored = "Ctrl+Alt+K";
        Row.Create();
        Row.Applied.Clear();
        Row.Stored = RuleBreakingText;

        Row.SettingsChanged();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.DoesNotContain(Row.Applied, keys => keys != (Ctrl | Alt, Key.K));
    }

    [Fact]
    public void Keeps_the_previous_keys_and_says_why_when_the_stored_text_cannot_be_read()
    {
        Row.Create();
        Row.Applied.Clear();
        Row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        Row.SettingsChanged();

        Assert.Equal(Default, Row.Keys.Active);
        Assert.DoesNotContain(Row.Applied, keys => keys != (Default.Modifiers, Default.Key));
        Assert.Equal(
            $"The saved {Name} shortcut couldn't be read: The settings file is damaged.", Row.Keys.Message);
    }

    [Fact]
    public void A_later_valid_stored_text_clears_the_message_about_the_stored_text()
    {
        Row.Stored = InvalidText;
        Row.Create();
        Row.Stored = "Ctrl+Alt+K";

        Row.SettingsChanged();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void Keeps_the_message_about_a_rejected_pick()
    {
        Row.Create();
        var (modifiers, key, message) = RuleBreakingPick;
        Pick(modifiers, key);

        Row.Stored = "Ctrl+Alt+K";
        Row.SettingsChanged();

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Equal(message, Row.Keys.Message);
    }

    // A valid pick.

    [Fact]
    public void A_valid_pick_is_applied_saved_as_its_display_text_and_becomes_active()
    {
        Row.Create();

        Pick(Ctrl | Alt, Key.K);

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Equal((Ctrl | Alt, Key.K), Row.Applied[^1]);
        Assert.Equal(["Ctrl+Alt+K"], Row.Saved);
        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void A_valid_pick_clears_the_message_about_an_earlier_rejected_pick()
    {
        Row.Create();
        var (modifiers, key, _) = RuleBreakingPick;
        Pick(modifiers, key);

        Pick(Ctrl | Alt, Key.K);

        Assert.Null(Row.Keys.Message);
    }

    [Fact]
    public void A_valid_pick_does_not_touch_the_global_shortcuts()
    {
        Row.Create();

        Pick(Ctrl | Alt, Key.K);

        Assert.Empty(add.Action.Calls);
        Assert.Empty(manage.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Empty(manage.Saved);
    }

    // A pick that breaks a rule.

    [Fact]
    public void A_pick_that_breaks_a_rule_shows_which_one_and_changes_nothing()
    {
        Row.Create();
        Row.Applied.Clear();
        var (modifiers, key, message) = RuleBreakingPick;

        Pick(modifiers, key);

        Assert.Equal(message, Row.Keys.Message);
        Assert.Empty(Row.Applied);
        Assert.Empty(Row.Saved);
        Assert.Equal(Default, Row.Keys.Active);
    }

    // A pick another row uses, or its own keys.

    [Fact]
    public void A_pick_equal_to_the_Add_new_task_shortcut_names_that_row_and_changes_nothing()
    {
        Row.Create();
        Row.Applied.Clear();

        Pick(Win | Alt, Key.N);

        Assert.Equal("Add new task already uses Win+Alt+N. Press a different shortcut.", Row.Keys.Message);
        Assert.Empty(Row.Applied);
        Assert.Empty(Row.Saved);
        Assert.Equal(Default, Row.Keys.Active);
    }

    [Fact]
    public void A_pick_equal_to_the_Task_View_shortcut_names_that_row()
    {
        Row.Create();

        Pick(Win | Alt, Key.V);

        Assert.Equal("Task View already uses Win+Alt+V. Press a different shortcut.", Row.Keys.Message);
    }

    [Fact]
    public void A_pick_equal_to_a_global_shortcut_the_user_changed_is_refused()
    {
        Row.Create();
        add.Setting.Pick(Ctrl | Shift, Key.K, manage.Setting);

        Pick(Ctrl | Shift, Key.K);

        Assert.Equal("Add new task already uses Ctrl+Shift+K. Press a different shortcut.", Row.Keys.Message);
    }

    [Fact]
    public void A_pick_equal_to_the_other_window_only_shortcut_names_that_row_and_changes_nothing()
    {
        Sibling.Stored = "Ctrl+Alt+J";
        Row.Create();
        Row.Applied.Clear();

        Pick(Ctrl | Alt, Key.J);

        Assert.Equal(
            $"{Sibling.Keys.Label} already uses Ctrl+Alt+J. Press a different shortcut.", Row.Keys.Message);
        Assert.Empty(Row.Applied);
        Assert.Empty(Row.Saved);
    }

    [Fact]
    public void A_pick_equal_to_a_row_that_has_no_shortcut_is_not_refused()
    {
        var empty = TestShortcutSetting.AddNewTask();
        empty.Action.Answer = _ => false;
        empty.Connected();
        Row.Create();

        Row.Keys.Pick(Win | Alt, Key.N, empty.Setting, manage.Setting, SiblingKeys());

        Assert.Equal(Make(Win | Alt, Key.N), Row.Keys.Active);
    }

    [Fact]
    public void A_pick_equal_to_its_own_keys_changes_nothing_and_leaves_its_message_as_it_was()
    {
        Row.Create();
        var (modifiers, key, message) = RuleBreakingPick;
        Pick(modifiers, key);
        Row.Applied.Clear();

        Pick(Default.Modifiers, Default.Key);

        Assert.Empty(Row.Applied);
        Assert.Empty(Row.Saved);
        Assert.Equal(message, Row.Keys.Message);
    }

    // A pick that can't be saved.

    [Fact]
    public void A_pick_that_was_kept_before_a_notice_failed_stays_active_and_shows_the_shared_message()
    {
        Row.Create();
        Row.SaveFailure = new NoticeFailedException<TaskItem>(null, [new IOException("Changed failed.")]);

        Pick(Ctrl | Alt, Key.K);

        Assert.Equal(CtrlAltK, Row.Keys.Active);
        Assert.Equal((CtrlAltK.Modifiers, CtrlAltK.Key), Row.Applied[^1]);
        Assert.Equal("Saved, but the app couldn't finish updating: Changed failed.", Row.Keys.Message);
    }

    [Fact]
    public void A_pick_that_cannot_be_saved_goes_back_to_the_previous_keys_and_says_what_failed()
    {
        Row.Create();
        Row.SaveFailure = new IOException("The disk is full.");

        Pick(Ctrl | Alt, Key.K);

        Assert.Equal(Default, Row.Keys.Active);
        Assert.Equal((Default.Modifiers, Default.Key), Row.Applied[^1]);
        Assert.Equal($"Ctrl+Alt+K wasn't saved for {Name}: The disk is full.", Row.Keys.Message);
        Assert.Empty(Row.Saved);
    }
}
