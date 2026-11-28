using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The one way the app writes a shortcut for the user: modifiers in a fixed order, then the key.</summary>
[Unit]
public class ShortcutTextTests
{
    [Theory]
    [InlineData(ModifierKeys.Control, Key.Enter, "Ctrl+Enter")]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Alt, Key.N, "Win+Alt+N")]
    [InlineData(ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Windows, Key.Space, "Win+Ctrl+Alt+Shift+Space")]
    [InlineData(ModifierKeys.Shift, Key.Escape, "Shift+Escape")]
    [InlineData(ModifierKeys.Control, Key.F5, "Ctrl+F5")]
    public void Writes_the_modifiers_in_a_fixed_order_then_the_key(ModifierKeys modifiers, Key key, string expected)
    {
        Assert.Equal(expected, ShortcutText.Of(modifiers, key));
    }

    [Fact]
    public void Writes_Enter_as_Enter_not_Return()
    {
        Assert.Equal("Alt+Enter", ShortcutText.Of(ModifierKeys.Alt, Key.Return));
    }

    [Fact]
    public void Writes_a_key_with_no_modifier_as_the_key_alone()
    {
        Assert.Equal("F9", ShortcutText.Of(ModifierKeys.None, Key.F9));
    }
}
