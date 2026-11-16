using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Save setting: what <see cref="WindowKeysSettingTests"/> says every window-only shortcut does, with Save's
/// default and rules.
/// </summary>
[Unit]
public class SaveSettingTests : WindowKeysSettingTests
{
    private readonly TestSaveSetting row = new();
    private readonly TestCloseWindowSetting close = new();

    private protected override TestWindowKeysSetting Row => row;
    private protected override TestWindowKeysSetting Sibling => close;
    protected override string Name => "Save";
    protected override Shortcut Default => TestSaveSetting.Default;
    protected override Shortcut Make(ModifierKeys modifiers, Key key) => TestSaveSetting.Make(modifiers, key);
    protected override string RuleBreakingText => "Shift+A";
    protected override (ModifierKeys Modifiers, Key Key, string Message) RuleBreakingPick =>
        (Shift, Key.A, TestSaveSetting.ShiftAloneMessage);

    [Fact]
    public void Starts_with_Ctrl_plus_Enter()
    {
        row.Create();

        Assert.Equal([(Ctrl, Key.Enter)], row.Applied);
    }

    [Fact]
    public void Accepts_stored_keys_that_only_Save_may_use()
    {
        row.Stored = "Ctrl+F5";

        row.Create();

        Assert.Equal([(Ctrl, Key.F5)], row.Applied);
        Assert.Null(row.Setting.Message);
    }

    [Theory]
    [InlineData(ModifierKeys.None, Key.K, TestSaveSetting.NoModifierMessage)]
    [InlineData(ModifierKeys.None, Key.F5, TestSaveSetting.NoModifierMessage)]
    [InlineData(Shift, Key.D1, TestSaveSetting.ShiftAloneMessage)]
    [InlineData(Ctrl | Alt, Key.LeftShift, "Add a key besides the modifier keys.")]
    public void A_pick_that_breaks_a_Save_rule_shows_which_one_and_changes_nothing(
        ModifierKeys modifiers, Key key, string message)
    {
        row.Create();
        row.Applied.Clear();

        row.Setting.Pick(modifiers, key);

        Assert.Equal(message, row.Setting.Message);
        Assert.Empty(row.Applied);
        Assert.Empty(row.Saved);
        Assert.Equal(TestSaveSetting.Default, row.Setting.Active);
    }
}
