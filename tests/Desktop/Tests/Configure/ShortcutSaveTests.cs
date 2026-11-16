using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The rules of the Save shortcut, which never registers with Windows: one or more modifier keys plus any other key,
/// but never Shift alone with a key that types a character.
/// </summary>
[Unit]
public class ShortcutSaveTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    [Fact]
    public void The_default_is_Ctrl_plus_Enter()
    {
        var shortcut = Shortcut.SaveDefault;

        Assert.Equal(Ctrl, shortcut.Modifiers);
        Assert.Equal(Key.Enter, shortcut.Key);
        Assert.Equal(shortcut, Shortcut.ParseSave(shortcut.ToDisplayText()));
    }

    [Theory]
    [InlineData(Ctrl, Key.Enter)]
    [InlineData(Ctrl, Key.K)]
    [InlineData(Alt, Key.S)]
    [InlineData(Win, Key.D1)]
    [InlineData(Ctrl | Alt, Key.F12)]
    [InlineData(Ctrl | Shift, Key.A)]
    [InlineData(Shift | Alt, Key.OemComma)]
    [InlineData(Ctrl | Alt | Shift | Win, Key.Space)]
    public void Accepts_one_or_more_modifiers_plus_any_other_key(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckSave(modifiers, key, out var shortcut);

        Assert.Null(message);
        Assert.Equal(modifiers, shortcut!.Modifiers);
        Assert.Equal(key, shortcut.Key);
    }

    [Theory]
    [InlineData(Shift, Key.F5)]
    [InlineData(Shift, Key.Escape)]
    [InlineData(Shift, Key.Delete)]
    [InlineData(Shift, Key.Left)]
    public void Accepts_Shift_alone_with_a_key_that_does_not_type_a_character(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckSave(modifiers, key, out var shortcut);

        Assert.Null(message);
        Assert.NotNull(shortcut);
    }

    [Theory]
    [InlineData(ModifierKeys.None, Key.K)]
    [InlineData(ModifierKeys.None, Key.Enter)]
    [InlineData(ModifierKeys.None, Key.F5)]
    [InlineData(ModifierKeys.None, Key.Escape)]
    public void Rejects_no_modifier(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckSave(modifiers, key, out var shortcut);

        Assert.Equal(TestSaveSetting.NoModifierMessage, message);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(Shift, Key.A)]
    [InlineData(Shift, Key.Z)]
    [InlineData(Shift, Key.D1)]
    [InlineData(Shift, Key.NumPad5)]
    [InlineData(Shift, Key.OemPlus)]
    public void Rejects_Shift_alone_with_a_key_that_types_a_character(ModifierKeys modifiers, Key key)
    {
        var message = Shortcut.CheckSave(modifiers, key, out var shortcut);

        Assert.Equal(TestSaveSetting.ShiftAloneMessage, message);
        Assert.Null(shortcut);
    }

    [Theory]
    [MemberData(nameof(ShortcutKeyData.NotRealKeys), MemberType = typeof(ShortcutKeyData))]
    public void Rejects_keys_that_are_not_real_keys_or_modifiers_that_are_not_Ctrl_Alt_Shift_or_Win(
        ModifierKeys modifiers, Key key, string expected)
    {
        var message = Shortcut.CheckSave(modifiers, key, out var shortcut);

        Assert.Equal(expected, message);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData("Ctrl+Enter")]
    [InlineData("Ctrl+K")]
    [InlineData("Shift+F5")]
    [InlineData("Win+Ctrl+Alt+Shift+Space")]
    public void Parse_reads_the_text_the_display_text_wrote(string text)
    {
        var shortcut = Shortcut.ParseSave(text);

        Assert.NotNull(shortcut);
        Assert.Equal(text, shortcut.ToDisplayText());
    }

    [Fact]
    public void Parse_reads_Return_as_Enter()
    {
        var shortcut = Shortcut.ParseSave("Ctrl+Return");

        Assert.NotNull(shortcut);
        Assert.Equal("Ctrl+Enter", shortcut.ToDisplayText());
    }

    [Theory]
    [MemberData(nameof(ShortcutKeyData.NotShortcutText), MemberType = typeof(ShortcutKeyData))]
    [InlineData("K")]
    [InlineData("Shift+A")]
    public void Parse_rejects_text_that_breaks_a_rule_or_is_not_the_display_text(string? text)
    {
        Assert.Null(Shortcut.ParseSave(text));
    }

    [Fact]
    public void Global_shortcuts_and_Close_window_keep_their_own_rules()
    {
        Assert.False(Shortcut.TryCreate(Ctrl, Key.Enter, out _));
        Assert.NotNull(Shortcut.CheckCloseWindow(Ctrl, Key.Enter, out _));
        Assert.Null(Shortcut.CheckSave(Ctrl, Key.Enter, out _));
    }
}
