using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// A box that shows a shortcut and records the keys the user presses in it. It reports a pick on the first key that
/// isn't a modifier, and Esc cancels a pick. It takes every modifier key's release, Alt's included, so Windows doesn't
/// move the keyboard to the window's hidden system menu. It also reports two or more modifier keys pressed and
/// released with no other key, which is what a shortcut taken by Windows or another app looks like.
/// </summary>
internal sealed class ShortcutField : TextBox
{
    private readonly ShortcutKeyInput _input;

    /// <param name="picked">Called with the held modifiers and the key that completed a pick.</param>
    /// <param name="swallowed">Called with the modifiers of a chord released with no other key.</param>
    public ShortcutField(Action<ModifierKeys, Key> picked, Action<ModifierKeys> swallowed)
    {
        _input = new ShortcutKeyInput(picked, swallowed);
        IsReadOnly = true;
        IsUndoEnabled = false;
        Width = 240;
        SetResourceReference(StyleProperty, typeof(TextBox)); // A theme styles exact types only, so this subclass asks for the text box style.
    }

    /// <summary>Shows the shortcut, or nothing when null.</summary>
    public void Show(Shortcut? shortcut) => Text = shortcut?.ToDisplayText() ?? "";

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = KeyOf(e);
        if (key == Key.None)
        {
            return;
        }

        if (_input.KeyDown(key, e.IsRepeat))
        {
            e.Handled = true;
        }
        else
        {
            base.OnPreviewKeyDown(e); // Plain Tab still moves on, so the field doesn't trap the keyboard.
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_input.KeyUp(KeyOf(e)))
        {
            e.Handled = true;
        }

        base.OnPreviewKeyUp(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        _input.FocusLost();
        base.OnLostKeyboardFocus(e);
    }

    // An input method's keys arrive as their own marker keys, with the real key beside them.
    private static Key KeyOf(KeyEventArgs e) => e.Key switch
    {
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => EventKey.Of(e),
    };
}
