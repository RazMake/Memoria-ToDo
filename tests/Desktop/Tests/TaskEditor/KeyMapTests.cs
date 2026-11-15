using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
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

    private static SaveKeys SaveOn(ModifierKeys modifiers, Key key)
    {
        var saveKeys = new SaveKeys();
        saveKeys.Set(modifiers, key);
        return saveKeys;
    }

    [Fact]
    public void The_Save_keys_run_the_save_action_and_are_handled()
    {
        var ran = 0;
        var keys = new KeyMap().OnSaveKeys(SaveOn(ModifierKeys.Control, Key.Enter), () => ran++);

        Assert.True(keys.TryRun(ModifierKeys.Control, Key.Enter));

        Assert.Equal(1, ran);
    }

    [Fact]
    public void A_key_that_is_not_the_Save_keys_is_not_handled_by_the_save_action()
    {
        var ran = false;
        var keys = new KeyMap().OnSaveKeys(SaveOn(ModifierKeys.Control, Key.Enter), () => ran = true);

        Assert.False(keys.TryRun(ModifierKeys.None, Key.Enter));
        Assert.False(keys.TryRun(ModifierKeys.Control, Key.S));
        Assert.False(keys.TryRun(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter));

        Assert.False(ran);
    }

    [Fact]
    public void Until_the_Save_keys_are_set_the_save_action_runs_on_no_key()
    {
        var ran = false;
        var keys = new KeyMap().OnSaveKeys(new SaveKeys(), () => ran = true);

        Assert.False(keys.TryRun(ModifierKeys.Control, Key.Enter));
        Assert.False(keys.TryRun(ModifierKeys.None, Key.None));

        Assert.False(ran);
    }

    [Fact]
    public void New_Save_keys_apply_at_once_and_the_old_ones_stop_saving()
    {
        var ran = 0;
        var saveKeys = SaveOn(ModifierKeys.Control, Key.Enter);
        var keys = new KeyMap().OnSaveKeys(saveKeys, () => ran++);

        saveKeys.Set(ModifierKeys.Control | ModifierKeys.Alt, Key.S);

        Assert.False(keys.TryRun(ModifierKeys.Control, Key.Enter));
        Assert.True(keys.TryRun(ModifierKeys.Control | ModifierKeys.Alt, Key.S));
        Assert.Equal(1, ran);
    }

    [Fact]
    public void Other_chords_still_run_beside_the_Save_keys()
    {
        var bold = 0;
        var keys = new KeyMap()
            .On(ModifierKeys.Control, Key.B, () => bold++)
            .OnSaveKeys(SaveOn(ModifierKeys.Control, Key.Enter), () => { });

        Assert.True(keys.TryRun(ModifierKeys.Control, Key.B));

        Assert.Equal(1, bold);
    }

    [Fact]
    public void The_Save_keys_win_when_the_user_picks_a_chord_another_action_already_has()
    {
        var events = new List<string>();
        var keys = new KeyMap()
            .On(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter, () => events.Add("save and clear"))
            .OnSaveKeys(SaveOn(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter), () => events.Add("save"));

        Assert.True(keys.TryRun(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter));

        Assert.Equal(["save"], events);
    }

    [Fact]
    public void Save_and_clear_runs_again_once_the_Save_keys_move_away_from_its_chord()
    {
        var events = new List<string>();
        var saveKeys = SaveOn(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter);
        var keys = new KeyMap()
            .On(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter, () => events.Add("save and clear"))
            .OnSaveKeys(saveKeys, () => events.Add("save"));
        saveKeys.Set(ModifierKeys.Control, Key.Enter);

        keys.TryRun(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter);

        Assert.Equal(["save and clear"], events);
    }

    [Fact]
    public void A_missing_Save_keys_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new KeyMap().OnSaveKeys(null!, () => { }));
    }
}
