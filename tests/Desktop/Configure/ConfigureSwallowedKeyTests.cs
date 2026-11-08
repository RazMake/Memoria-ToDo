using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A shortcut Windows or another app holds never reaches the box, but the modifier keys do. So a box reads two or
/// more modifier keys pressed together and released with no other key as a sign of a taken shortcut. The tests can't
/// raise the system menu that Alt alone opens in Windows, so the Alt tests only check that the box takes the key-up
/// and takes the next pick.
/// </summary>
[UX]
public class ConfigureSwallowedKeyTests
{
    private const string CtrlShiftMessage =
        "If you pressed a key with Ctrl+Shift, Windows or another app uses that shortcut. Press a different one.";

    [WpfFact]
    public void Two_modifiers_pressed_and_released_with_no_other_key_say_the_shortcut_may_be_taken()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift);

        Assert.Equal(CtrlShiftMessage, screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
    }

    [WpfTheory]
    [InlineData(Key.K)]
    [InlineData(Key.Escape)]
    public void A_key_pressed_alone_before_the_modifiers_does_not_hide_the_message(Key aloneKey)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, aloneKey);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift);

        Assert.Equal(CtrlShiftMessage, screen.AddMessage);
    }

    [WpfFact]
    public void The_message_names_the_modifiers_in_the_order_Win_Ctrl_Alt_Shift()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ManageField, Key.LeftShift, Key.LeftAlt, Key.LWin);

        Assert.Equal(
            "If you pressed a key with Win+Alt+Shift, Windows or another app uses that shortcut. Press a different one.",
            screen.ManageMessage);
    }

    [WpfFact]
    public void The_message_shows_only_after_the_modifiers_are_released()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.KeyDown(screen.AddField, Key.LeftCtrl);
        screen.KeyDown(screen.AddField, Key.LeftShift);

        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void One_modifier_pressed_and_released_alone_shows_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl);

        Assert.Equal("", screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
    }

    [WpfFact]
    public void Two_modifiers_released_after_the_box_lost_the_focus_show_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        screen.KeyDown(screen.AddField, Key.LeftCtrl);
        screen.KeyDown(screen.AddField, Key.LeftShift);

        screen.FocusStartWithWindowsBox();
        screen.KeyUp(screen.AddField, Key.LeftShift);
        screen.KeyUp(screen.AddField, Key.LeftCtrl);

        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void Two_modifiers_and_Esc_show_nothing_since_a_key_reached_the_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.KeyDown(screen.AddField, Key.LeftCtrl);
        screen.KeyDown(screen.AddField, Key.LeftShift);

        screen.KeyDown(screen.AddField, Key.Escape);
        screen.KeyUp(screen.AddField, Key.Escape);
        screen.KeyUp(screen.AddField, Key.LeftShift);
        screen.KeyUp(screen.AddField, Key.LeftCtrl);

        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void A_later_valid_pick_clears_the_message()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("", screen.AddMessage);
    }

    [WpfTheory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    public void Every_modifier_key_up_is_handled_by_the_box(Key modifier)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.KeyDown(screen.AddField, modifier);

        var handled = screen.KeyUp(screen.AddField, modifier);

        Assert.True(handled);
    }

    [WpfFact]
    public void A_pick_still_works_after_Alt_was_pressed_and_released_alone()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.KeyDown(screen.AddField, Key.LeftAlt);
        screen.KeyUp(screen.AddField, Key.LeftAlt);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal([ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K))], screen.AddNewTask.Calls);
        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);
    }
}
