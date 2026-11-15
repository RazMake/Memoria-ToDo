using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Save keys, what Set accepts and refuses and what Matches answers. Set's rules are Close window keys' rules, shared
/// through one key check; they are tested here on Save keys' own interface, once per rule.
/// </summary>
[Unit]
public class SaveKeysTests
{
    [Fact]
    public void Before_Set_no_key_matches()
    {
        var keys = new SaveKeys();

        Assert.False(keys.Matches(ModifierKeys.Control, Key.Enter));
        Assert.False(keys.Matches(ModifierKeys.None, Key.Enter));
        Assert.False(keys.Matches(ModifierKeys.None, Key.None));
    }

    [Fact]
    public void Before_Set_the_text_is_empty()
    {
        Assert.Equal("", new SaveKeys().Text);
    }

    [Fact]
    public void The_text_shows_the_keys_that_were_set()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.Equal("Ctrl+Enter", keys.Text);
    }

    [Fact]
    public void Set_again_replaces_the_text()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        keys.Set(ModifierKeys.Windows | ModifierKeys.Shift, Key.S);

        Assert.Equal("Win+Shift+S", keys.Text);
    }

    [Fact]
    public void A_refused_Set_keeps_the_previous_text()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control, Key.None));

        Assert.Equal("Ctrl+Enter", keys.Text);
    }

    [Fact]
    public void Matches_the_keys_that_were_set()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.True(keys.Matches(ModifierKeys.Control, Key.Enter));
    }

    [Fact]
    public void Matches_a_key_set_with_several_modifiers()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows, Key.F12);

        Assert.True(keys.Matches(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows, Key.F12));
    }

    [Fact]
    public void Matches_a_key_set_with_no_modifiers()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.None, Key.F9);

        Assert.True(keys.Matches(ModifierKeys.None, Key.F9));
    }

    [Fact]
    public void Does_not_match_when_a_modifier_is_missing()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter);

        Assert.False(keys.Matches(ModifierKeys.Control, Key.Enter));
        Assert.False(keys.Matches(ModifierKeys.None, Key.Enter));
    }

    [Fact]
    public void Does_not_match_when_a_modifier_is_extra()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.False(keys.Matches(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter));
    }

    [Fact]
    public void Does_not_match_a_different_modifier()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.False(keys.Matches(ModifierKeys.Alt, Key.Enter));
    }

    [Fact]
    public void Does_not_match_a_different_key()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.False(keys.Matches(ModifierKeys.Control, Key.S));
    }

    [Fact]
    public void Set_again_replaces_the_keys_so_the_old_keys_stop_matching()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        keys.Set(ModifierKeys.Control, Key.S);

        Assert.False(keys.Matches(ModifierKeys.Control, Key.Enter));
        Assert.True(keys.Matches(ModifierKeys.Control, Key.S));
    }

    [Fact]
    public void Set_rejects_Key_None()
    {
        var keys = new SaveKeys();

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control, Key.None));
    }

    [Theory]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RWin)]
    [InlineData(Key.System)]
    public void Set_rejects_a_modifier_key_as_the_key(Key modifierKey)
    {
        var keys = new SaveKeys();

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.None, modifierKey));
    }

    [Fact]
    public void Set_rejects_modifiers_other_than_Ctrl_Alt_Shift_and_Win()
    {
        var keys = new SaveKeys();

        Assert.Throws<ArgumentException>(() => keys.Set((ModifierKeys)16, Key.Enter));
        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control | (ModifierKeys)16, Key.Enter));
    }

    [Fact]
    public void A_refused_Set_keeps_the_previous_keys()
    {
        var keys = new SaveKeys();
        keys.Set(ModifierKeys.Control, Key.Enter);

        Assert.Throws<ArgumentException>(() => keys.Set(ModifierKeys.Control, Key.None));

        Assert.True(keys.Matches(ModifierKeys.Control, Key.Enter));
    }
}
