using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What a shortcut box does with raw key events: which key completes a pick, which keys it keeps from Windows, and
/// when released modifier keys mean the shortcut was taken. Handled means the box keeps the key to itself.
/// </summary>
[Unit]
public class ShortcutKeyInputTests
{
    private readonly List<(ModifierKeys Modifiers, Key Key)> _picks = [];
    private readonly List<ModifierKeys> _swallowed = [];
    private int _escaped;
    private readonly ShortcutKeyInput _input;

    public ShortcutKeyInputTests()
    {
        _input = new ShortcutKeyInput((modifiers, key) => _picks.Add((modifiers, key)), _swallowed.Add, () => _escaped++);
    }

    private void Down(params Key[] keys)
    {
        foreach (var key in keys)
        {
            _input.KeyDown(key, isRepeat: false);
        }
    }

    private void Up(params Key[] keys)
    {
        foreach (var key in keys)
        {
            _input.KeyUp(key);
        }
    }

    [Fact]
    public void A_key_after_two_modifiers_is_a_pick_with_those_modifiers()
    {
        Down(Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal([(ModifierKeys.Control | ModifierKeys.Shift, Key.K)], _picks);
    }

    [Fact]
    public void Modifier_keys_alone_pick_nothing_and_are_handled()
    {
        var handled = _input.KeyDown(Key.LeftCtrl, isRepeat: false);

        Assert.True(handled);
        Assert.Empty(_picks);
    }

    [Fact]
    public void A_key_with_no_modifier_is_still_a_pick_so_the_rules_can_refuse_it()
    {
        var handled = _input.KeyDown(Key.K, isRepeat: false);

        Assert.True(handled);
        Assert.Equal([(ModifierKeys.None, Key.K)], _picks);
    }

    [Fact]
    public void Key_repeat_while_held_does_not_pick_again()
    {
        Down(Key.LeftCtrl, Key.LeftShift);
        _input.KeyDown(Key.K, isRepeat: false);

        var handled = _input.KeyDown(Key.K, isRepeat: true);

        Assert.True(handled);
        Assert.Single(_picks);
    }

    [Fact]
    public void Esc_with_no_modifier_is_handled_picks_nothing_and_tells_the_box_to_keep_the_shortcut()
    {
        var handled = _input.KeyDown(Key.Escape, isRepeat: false);

        Assert.True(handled);
        Assert.Empty(_picks);
        Assert.Equal(1, _escaped);
    }

    [Fact]
    public void Esc_held_down_tells_the_box_once()
    {
        _input.KeyDown(Key.Escape, isRepeat: false);

        var handled = _input.KeyDown(Key.Escape, isRepeat: true);

        Assert.True(handled);
        Assert.Equal(1, _escaped);
    }

    [Fact]
    public void Esc_with_a_modifier_held_is_a_pick_so_Close_window_can_use_it()
    {
        Down(Key.LeftShift);

        var handled = _input.KeyDown(Key.Escape, isRepeat: false);

        Assert.True(handled);
        Assert.Equal([(ModifierKeys.Shift, Key.Escape)], _picks);
        Assert.Equal(0, _escaped);
    }

    [Fact]
    public void Keys_other_than_Esc_never_tell_the_box_to_keep_the_shortcut()
    {
        Down(Key.LeftCtrl, Key.LeftShift, Key.K);
        _input.KeyDown(Key.Tab, isRepeat: false);

        Assert.Equal(0, _escaped);
    }

    [Fact]
    public void A_box_without_an_escape_action_still_handles_Esc()
    {
        var input = new ShortcutKeyInput((_, _) => { }, _ => { });

        Assert.True(input.KeyDown(Key.Escape, isRepeat: false));
    }

    [Fact]
    public void Plain_Tab_is_left_to_the_box_so_the_keyboard_is_not_trapped()
    {
        var handled = _input.KeyDown(Key.Tab, isRepeat: false);

        Assert.False(handled);
        Assert.Empty(_picks);
    }

    [Fact]
    public void Tab_with_modifiers_is_a_pick()
    {
        Down(Key.LeftCtrl, Key.LeftShift);

        var handled = _input.KeyDown(Key.Tab, isRepeat: false);

        Assert.True(handled);
        Assert.Equal([(ModifierKeys.Control | ModifierKeys.Shift, Key.Tab)], _picks);
    }

    [Fact]
    public void A_key_that_names_nothing_is_ignored()
    {
        var handled = _input.KeyDown(Key.None, isRepeat: false);

        Assert.False(handled);
        Assert.Empty(_picks);
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    public void Every_modifier_key_release_is_handled_so_Alt_cannot_open_the_system_menu(Key modifier)
    {
        Down(modifier);

        Assert.True(_input.KeyUp(modifier));
    }

    [Fact]
    public void Releasing_a_key_that_is_not_a_modifier_is_not_handled()
    {
        Down(Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.False(_input.KeyUp(Key.K));
    }

    [Fact]
    public void Two_modifiers_released_with_no_other_key_report_the_taken_shortcut_once_after_the_last_release()
    {
        Down(Key.LeftCtrl, Key.LeftShift);

        Up(Key.LeftShift);
        Assert.Empty(_swallowed);
        Up(Key.LeftCtrl);

        Assert.Equal([ModifierKeys.Control | ModifierKeys.Shift], _swallowed);
    }

    [Fact]
    public void One_modifier_pressed_and_released_alone_reports_nothing()
    {
        Down(Key.LeftCtrl);
        Up(Key.LeftCtrl);

        Assert.Empty(_swallowed);
    }

    [Fact]
    public void Two_modifiers_and_a_key_that_reached_the_box_report_nothing()
    {
        Down(Key.LeftCtrl, Key.LeftShift, Key.Escape);
        Up(Key.Escape, Key.LeftShift, Key.LeftCtrl);

        Assert.Empty(_swallowed);
    }

    [Fact]
    public void Two_modifiers_released_after_the_box_lost_the_focus_report_nothing()
    {
        Down(Key.LeftCtrl, Key.LeftShift);

        _input.FocusLost();
        Up(Key.LeftShift, Key.LeftCtrl);

        Assert.Empty(_swallowed);
    }

    [Fact]
    public void A_pick_after_the_focus_returns_does_not_count_modifiers_held_before_it_was_lost()
    {
        Down(Key.LeftCtrl);
        _input.FocusLost();

        Down(Key.LeftShift, Key.K);

        Assert.Equal([(ModifierKeys.Shift, Key.K)], _picks);
    }

    [Fact]
    public void A_pick_still_works_after_Alt_was_pressed_and_released_alone()
    {
        Down(Key.LeftAlt);
        Up(Key.LeftAlt);

        Down(Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal([(ModifierKeys.Control | ModifierKeys.Shift, Key.K)], _picks);
    }
}
