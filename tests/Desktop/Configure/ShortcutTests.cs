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
    [InlineData(Alt, Key.K)]
    [InlineData(Shift, Key.A)]
    [InlineData(Win, Key.T)]
    public void Rejects_one_modifier(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(ModifierKeys.None, Key.K)]
    [InlineData(ModifierKeys.None, Key.F5)]
    public void Rejects_no_modifier(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(Ctrl | Alt, Key.LeftCtrl)]
    [InlineData(Ctrl | Alt, Key.RightShift)]
    [InlineData(Ctrl | Shift, Key.LeftAlt)]
    [InlineData(Ctrl | Alt, Key.LWin)]
    [InlineData(ModifierKeys.None, Key.LeftCtrl)]
    [InlineData(Ctrl | Alt, Key.None)]
    public void Rejects_modifiers_alone(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData(Ctrl | Alt, Key.F12)]
    [InlineData(Ctrl | Alt | Shift | Win, Key.F12)]
    public void Rejects_F12(ModifierKeys modifiers, Key key)
    {
        var accepted = Shortcut.TryCreate(modifiers, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Fact]
    public void Rejects_a_modifier_bit_other_than_Ctrl_Alt_Shift_and_Win()
    {
        var accepted = Shortcut.TryCreate((ModifierKeys)0x10 | Ctrl | Alt, Key.K, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Theory]
    [InlineData((Key)int.MaxValue)]
    [InlineData(Key.System)]
    [InlineData(Key.ImeProcessed)]
    [InlineData(Key.DeadCharProcessed)]
    public void Rejects_a_key_that_is_not_a_real_key(Key key)
    {
        var accepted = Shortcut.TryCreate(Ctrl | Alt, key, out var shortcut);

        Assert.False(accepted);
        Assert.Null(shortcut);
    }

    [Fact]
    public void Shortcuts_with_the_same_keys_are_equal()
    {
        Shortcut.TryCreate(Ctrl | Alt, Key.K, out var first);
        Shortcut.TryCreate(Ctrl | Alt, Key.K, out var second);
        Shortcut.TryCreate(Ctrl | Alt, Key.J, out var other);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Has_no_public_constructor_so_every_shortcut_passed_the_rules()
    {
        Assert.Empty(typeof(Shortcut).GetConstructors());
    }
}
