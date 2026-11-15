using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A shortcut's text: the message when keys break a rule, the text the user reads and Windows settings keep, and the
/// parser that reads only that exact text back. The message wording is part of the spec.
/// </summary>
[Unit]
public class ShortcutTextTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    private const string OneModifier = "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus one other key.";

    [Theory]
    [InlineData((ModifierKeys)0x10 | Ctrl | Alt, Key.K, "Use only Ctrl, Alt, Shift, and Win as modifier keys.")]
    [InlineData(Ctrl | Alt, (Key)int.MaxValue, "Pick a key on the keyboard.")]
    [InlineData(Ctrl | Alt, Key.System, "Pick a key on the keyboard.")]
    [InlineData(Ctrl | Alt, Key.F12, "F12 is kept by Windows for debuggers.")]
    [InlineData(Ctrl | Alt, Key.None, "Add a key besides the modifier keys.")]
    [InlineData(Ctrl | Alt, Key.RightShift, "Add a key besides the modifier keys.")]
    [InlineData(Ctrl, Key.C, OneModifier)]
    [InlineData(ModifierKeys.None, Key.K, OneModifier)]
    public void Check_says_which_rule_the_keys_break(ModifierKeys modifiers, Key key, string message)
    {
        var broken = Shortcut.Check(modifiers, key, out var shortcut);

        Assert.Equal(message, broken);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(Ctrl, Key.F12, "F12 is kept by Windows for debuggers.")]
    [InlineData(Ctrl, Key.LeftAlt, "Add a key besides the modifier keys.")]
    [InlineData((ModifierKeys)0x10 | Ctrl, Key.F12, "Use only Ctrl, Alt, Shift, and Win as modifier keys.")]
    public void Check_reports_the_first_broken_rule_when_several_are_broken(
        ModifierKeys modifiers, Key key, string message)
    {
        Assert.Equal(message, Shortcut.Check(modifiers, key, out _));
    }

    [Fact]
    public void Check_returns_no_message_and_the_shortcut_for_valid_keys()
    {
        var broken = Shortcut.Check(Ctrl | Alt, Key.K, out var shortcut);

        Assert.Null(broken);
        Assert.Equal(TestShortcut.A, shortcut);
    }

    [Theory]
    [InlineData(Win | Alt, Key.N, "Win+Alt+N")]
    [InlineData(Shift | Alt | Ctrl | Win, Key.F11, "Win+Ctrl+Alt+Shift+F11")]
    [InlineData(Ctrl | Shift, Key.D1, "Ctrl+Shift+D1")]
    public void Display_text_names_the_modifiers_in_the_order_Win_Ctrl_Alt_Shift_then_the_key(
        ModifierKeys modifiers, Key key, string text)
    {
        Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.Equal(text, shortcut!.ToDisplayText());
    }

    [Theory]
    [InlineData("Win+Alt+N")]
    [InlineData("Ctrl+Shift+K")]
    [InlineData("Win+Ctrl+Alt+Shift+F11")]
    public void Parse_reads_the_text_the_display_text_wrote(string text)
    {
        var shortcut = Shortcut.Parse(text);

        Assert.NotNull(shortcut);
        Assert.Equal(text, shortcut.ToDisplayText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("+K")]
    public void Parse_rejects_text_that_is_not_modifiers_plus_a_key(string? text)
    {
        Assert.Null(Shortcut.Parse(text));
    }

    [Theory]
    [InlineData("Alt+Ctrl+K")]
    [InlineData("ctrl+alt+k")]
    [InlineData("Control+Ctrl+Alt+K")]
    [InlineData("Ctrl+Ctrl+Alt+K")]
    [InlineData("Ctrl+Alt+K+")]
    public void Parse_rejects_the_same_keys_in_a_different_spelling_or_order(string text)
    {
        Assert.Null(Shortcut.Parse(text));
    }

    [Theory]
    [InlineData("Ctrl+K")]
    [InlineData("Ctrl+Alt+F12")]
    [InlineData("Ctrl+Alt+LeftCtrl")]
    [InlineData("Ctrl+Alt+NoSuchKey")]
    [InlineData("Hyper+Ctrl+Alt+K")]
    public void Parse_rejects_text_that_breaks_a_rule_or_names_nothing(string text)
    {
        Assert.Null(Shortcut.Parse(text));
    }

    [Fact]
    public void Parse_rejects_a_key_written_as_its_number()
    {
        Assert.Null(Shortcut.Parse($"Ctrl+Alt+{(int)Key.K}"));
    }
}
