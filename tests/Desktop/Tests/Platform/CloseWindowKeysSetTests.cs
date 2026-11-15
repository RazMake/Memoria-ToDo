using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Close window keys, what Set and Attach accept and refuse. No window is made.</summary>
[Unit]
public class CloseWindowKeysSetTests
{
    [Theory]
    [InlineData(ModifierKeys.None, Key.Escape)]
    [InlineData(ModifierKeys.Control, Key.W)]
    [InlineData(ModifierKeys.Alt, Key.F4)]
    [InlineData(ModifierKeys.Shift, Key.Escape)]
    [InlineData(ModifierKeys.Windows, Key.Q)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows, Key.F12)]
    public void Set_accepts_a_key_with_any_mix_of_Ctrl_Alt_Shift_and_Win(ModifierKeys modifiers, Key key)
    {
        var keys = new CloseWindowKeys();

        keys.Set(modifiers, key);
    }

    [Fact]
    public void Set_can_be_called_again_to_replace_the_keys()
    {
        var keys = new CloseWindowKeys();
        keys.Set(ModifierKeys.None, Key.Escape);

        keys.Set(ModifierKeys.Control, Key.W);
    }

    [Fact]
    public void Set_rejects_Key_None()
    {
        var keys = new CloseWindowKeys();

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control, Key.None));
    }

    [Theory]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    [InlineData(Key.System)]
    public void Set_rejects_a_modifier_key_as_the_key(Key modifierKey)
    {
        var keys = new CloseWindowKeys();

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.None, modifierKey));
    }

    [Fact]
    public void Set_rejects_modifiers_other_than_Ctrl_Alt_Shift_and_Win()
    {
        var keys = new CloseWindowKeys();

        Assert.Throws<ArgumentException>(() => keys.Set((ModifierKeys)16, Key.Escape));
        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control | (ModifierKeys)16, Key.Escape));
    }

    [Fact]
    public void Attach_rejects_a_null_window()
    {
        var keys = new CloseWindowKeys();

        Assert.Throws<ArgumentNullException>(() => keys.Attach(null!));
    }
}
