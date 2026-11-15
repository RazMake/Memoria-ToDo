using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class KeyMapTests
{
    [Fact]
    public void A_registered_chord_runs_its_action_and_is_handled()
    {
        var ran = 0;
        var keys = new KeyMap().On(ModifierKeys.Control, Key.B, () => ran++);

        Assert.True(keys.TryRun(ModifierKeys.Control, Key.B));

        Assert.Equal(1, ran);
    }

    [Fact]
    public void An_unregistered_chord_is_not_handled()
    {
        var keys = new KeyMap().On(ModifierKeys.Control, Key.B, () => { });

        Assert.False(keys.TryRun(ModifierKeys.None, Key.B));
        Assert.False(keys.TryRun(ModifierKeys.Control, Key.I));
    }

    [Fact]
    public void A_chord_needs_exactly_the_modifiers_it_was_registered_with()
    {
        var ran = false;
        var keys = new KeyMap().On(ModifierKeys.Control, Key.B, () => ran = true);

        Assert.False(keys.TryRun(ModifierKeys.Control | ModifierKeys.Alt, Key.B));
        Assert.False(keys.TryRun(ModifierKeys.Control | ModifierKeys.Shift, Key.B));

        Assert.False(ran);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Try_action_decides_whether_the_key_is_handled(bool applies)
    {
        var keys = new KeyMap().Try(ModifierKeys.None, Key.Tab, () => applies);

        Assert.Equal(applies, keys.TryRun(ModifierKeys.None, Key.Tab));
    }

    [Fact]
    public void Registering_a_chord_twice_is_rejected()
    {
        var keys = new KeyMap().On(ModifierKeys.None, Key.Enter, () => { });

        Assert.Throws<ArgumentException>(() => keys.On(ModifierKeys.None, Key.Enter, () => { }));
    }
}
