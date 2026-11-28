using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Close window setting: what <see cref="WindowKeysSettingTests"/> says every window-only shortcut does, with
/// Close window's default, rules, and the keys only it may use.
/// </summary>
[Unit]
public class CloseWindowSettingTests : WindowKeysSettingTests
{
    private readonly TestCloseWindowSetting row = new();
    private readonly TestSaveSetting save = new();

    private protected override TestWindowKeysSetting Row => row;
    private protected override TestWindowKeysSetting Sibling => save;
    protected override string Name => "Close window";
    protected override Shortcut Default => TestCloseWindowSetting.Default;
    protected override Shortcut Make(ModifierKeys modifiers, Key key) => TestCloseWindowSetting.Make(modifiers, key);
    protected override string RuleBreakingText => "Ctrl+K";
    protected override (ModifierKeys Modifiers, Key Key, string Message) RuleBreakingPick =>
        (Ctrl, Key.K, TestCloseWindowSetting.RuleMessage);

    [Fact]
    public void Starts_with_Shift_plus_Esc()
    {
        row.Create();

        Assert.Equal([(Shift, Key.Escape)], row.Applied);
    }

    [Fact]
    public void Accepts_stored_keys_that_only_Close_window_may_use()
    {
        row.Stored = "Ctrl+Alt+F12";

        row.Create();

        Assert.Equal([(Ctrl | Alt, Key.F12)], row.Applied);
    }

    [Fact]
    public void A_pick_of_one_modifier_plus_a_function_key_is_valid()
    {
        row.Create();

        row.Setting.Pick(Ctrl, Key.F5);

        Assert.Equal(["Ctrl+F5"], row.Saved);
        Assert.Equal((Ctrl, Key.F5), row.Applied[^1]);
    }

    [Theory]
    [InlineData(ModifierKeys.None, Key.K, TestCloseWindowSetting.RuleMessage)]
    [InlineData(Ctrl, Key.K, TestCloseWindowSetting.RuleMessage)]
    [InlineData(ModifierKeys.None, Key.Escape, TestCloseWindowSetting.RuleMessage)]
    [InlineData(Ctrl | Alt, Key.LeftShift, "Add a key besides the modifier keys.")]
    public void A_pick_that_breaks_a_Close_window_rule_shows_which_one_and_changes_nothing(
        ModifierKeys modifiers, Key key, string message)
    {
        row.Create();
        row.Applied.Clear();

        row.Setting.Pick(modifiers, key);

        Assert.Equal(message, row.Setting.Message);
        Assert.Empty(row.Applied);
        Assert.Empty(row.Saved);
        Assert.Equal(TestCloseWindowSetting.Default, row.Setting.Active);
    }
}
