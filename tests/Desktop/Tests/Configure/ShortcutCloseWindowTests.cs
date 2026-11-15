using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The rules of the Close window shortcut, which never registers with Windows: two or more modifier keys plus any
/// other key (F12 included), or one modifier key plus Esc or a function key (F1 to F24).
/// </summary>
[Unit]
public class ShortcutCloseWindowTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    [Fact]
    public void The_default_is_Shift_plus_Esc()
    {
        var shortcut = Shortcut.CloseWindowDefault;

        Assert.Equal(Shift, shortcut.Modifiers);
        Assert.Equal(Key.Escape, shortcut.Key);
        Assert.Equal("Shift+Escape", shortcut.ToDisplayText());
    }

    [Theory]
    [InlineData(Ctrl | Alt, Key.K)]
    [InlineData(Ctrl | Shift, Key.F12)]
    [InlineData(Win | Alt, Key.Space)]
    [InlineData(Ctrl | Alt | Shift | Win, Key.Escape)]
    [InlineData(Ctrl | Shift, Key.Escape)]
    public void Accepts_two_or_more_modifiers_plus_any_other_key_F12_included(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckCloseWindow(modifiers, key, out var shortcut);

        Assert.Null(message);
        Assert.Equal(modifiers, shortcut!.Modifiers);
        Assert.Equal(key, shortcut.Key);
    }

    [Theory]
    [InlineData(Shift, Key.Escape)]
    [InlineData(Ctrl, Key.Escape)]
    [InlineData(Win, Key.Escape)]
    [InlineData(Alt, Key.Escape)]
    [InlineData(Ctrl, Key.F1)]
    [InlineData(Shift, Key.F12)]
    [InlineData(Alt, Key.F24)]
    public void Accepts_one_modifier_plus_Esc_or_a_function_key(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckCloseWindow(modifiers, key, out var shortcut);

        Assert.Null(message);
        Assert.NotNull(shortcut);
    }

    [Theory]
    [InlineData(ModifierKeys.None, Key.Escape)]
    [InlineData(ModifierKeys.None, Key.F5)]
    [InlineData(ModifierKeys.None, Key.K)]
    [InlineData(Ctrl, Key.K)]
    [InlineData(Shift, Key.Space)]
    [InlineData(Alt, Key.Tab)]
    [InlineData(Win, Key.D1)]
    public void Rejects_no_modifier_or_one_modifier_with_a_key_that_is_not_Esc_or_a_function_key(
        ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckCloseWindow(modifiers, key, out var shortcut);

        Assert.Equal(TestCloseWindowSetting.RuleMessage, message);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(Ctrl | Alt, Key.LeftCtrl, "Add a key besides the modifier keys.")]
    [InlineData(Ctrl, Key.RWin, "Add a key besides the modifier keys.")]
    [InlineData(Ctrl | Alt, Key.None, "Add a key besides the modifier keys.")]
    [InlineData(Ctrl | Alt, Key.System, "Pick a key on the keyboard.")]
    [InlineData(Ctrl | Alt, Key.ImeProcessed, "Pick a key on the keyboard.")]
    [InlineData(Ctrl | Alt, Key.DeadCharProcessed, "Pick a key on the keyboard.")]
    [InlineData(Ctrl | Alt, (Key)int.MaxValue, "Pick a key on the keyboard.")]
    [InlineData((ModifierKeys)0x10 | Ctrl | Alt, Key.K, "Use only Ctrl, Alt, Shift, and Win as modifier keys.")]
    public void Rejects_keys_that_are_not_real_keys_or_modifiers_that_are_not_Ctrl_Alt_Shift_or_Win(
        ModifierKeys modifiers, Key key, string expected)
    {
        var message = Shortcut.CheckCloseWindow(modifiers, key, out var shortcut);

        Assert.Equal(expected, message);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData("Shift+Escape")]
    [InlineData("Ctrl+F5")]
    [InlineData("Ctrl+Alt+F12")]
    [InlineData("Win+Ctrl+Alt+Shift+Space")]
    public void Parse_reads_the_text_the_display_text_wrote(string text)
    {
        var shortcut = Shortcut.ParseCloseWindow(text);

        Assert.NotNull(shortcut);
        Assert.Equal(text, shortcut.ToDisplayText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+K")]
    [InlineData("Escape")]
    [InlineData("Shift+Esc")]
    [InlineData("Escape+Shift")]
    [InlineData("Shift+escape")]
    [InlineData("not a shortcut")]
    public void Parse_rejects_text_that_breaks_a_rule_or_is_not_the_display_text(string? text)
    {
        Assert.Null(Shortcut.ParseCloseWindow(text));
    }

    [Fact]
    public void Global_shortcuts_still_refuse_one_modifier_plus_Esc()
    {
        Assert.False(Shortcut.TryCreate(Shift, Key.Escape, out _));
        Assert.Null(Shortcut.Parse("Shift+Escape"));
    }
}
