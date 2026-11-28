using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The rules every shortcut passes: two or more of Ctrl, Alt, Shift, and Win, plus one other key, and never F12.</summary>
[Unit]
public class ShortcutTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    [Theory]
    [InlineData(Ctrl | Alt, Key.K)]
    [InlineData(Ctrl | Shift, Key.F5)]
    [InlineData(Win | Alt, Key.T)]
    [InlineData(Alt | Shift, Key.D1)]
    [InlineData(Ctrl | Alt | Shift, Key.Space)]
    [InlineData(Ctrl | Alt | Shift | Win, Key.F11)]
    public void Accepts_two_or_more_modifiers_plus_one_other_key(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.True(accepted);
        Assert.NotNull(shortcut);
        Assert.Equal(modifiers, shortcut.Modifiers);
        Assert.Equal(key, shortcut.Key);
    }

    [Theory]
    [InlineData(Ctrl, Key.C)]
    [InlineData(Win, Key.T)]
    [InlineData(ModifierKeys.None, Key.F5)]
    [InlineData(Ctrl | Alt, Key.LeftCtrl)]
    [InlineData(Ctrl | Alt, Key.RWin)]
    [InlineData(Ctrl | Alt, Key.None)]
    [InlineData(Ctrl | Alt, Key.F12)]
    [InlineData(Ctrl | Alt | Shift | Win, Key.F12)]
    [InlineData((ModifierKeys)0x10 | Ctrl | Alt, Key.K)]
    [InlineData(Ctrl | Alt, (Key)int.MaxValue)]
    [InlineData(Ctrl | Alt, Key.System)]
    [InlineData(Ctrl | Alt, Key.ImeProcessed)]
    [InlineData(Ctrl | Alt, Key.DeadCharProcessed)]
    public void Rejects_keys_that_break_a_rule(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Fact]
    public void Shortcuts_with_the_same_keys_are_equal_and_others_are_not()
    {
        Shortcut.TryCreate(Ctrl | Alt, Key.K, out var first);
        Shortcut.TryCreate(Ctrl | Alt, Key.K, out var second);
        Shortcut.TryCreate(Ctrl | Alt, Key.J, out var other);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }
}
