using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// A box that shows a shortcut and records the keys the user presses in it. It reports a pick on the first key that
/// isn't a modifier, and Esc cancels a pick. It takes every modifier key's release, Alt's included, so Windows doesn't
/// move the keyboard to the window's hidden system menu. It also reports two or more modifier keys pressed and
/// released with no other key, which is what a shortcut taken by Windows or another app looks like.
/// </summary>
internal sealed class ShortcutField : TextBox
{
    private readonly Action<ModifierKeys, Key> _picked;
    private readonly Action<ModifierKeys> _swallowed;
    private readonly HeldModifierKeys _held = new();

    /// <param name="picked">Called with the held modifiers and the key that completed a pick.</param>
    /// <param name="swallowed">Called with the modifiers of a chord released with no other key.</param>
    public ShortcutField(Action<ModifierKeys, Key> picked, Action<ModifierKeys> swallowed)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(swallowed);
        _picked = picked;
        _swallowed = swallowed;
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

        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            _held.Pressed(key);
            e.Handled = true;
            return;
        }

        _held.OtherKeyReached();
        if (key == Key.Tab && !_held.AnyHeld)
        {
            base.OnPreviewKeyDown(e); // Plain Tab still moves on, so the field doesn't trap the keyboard.
        }
        else
        {
            e.Handled = true;
            if (key != Key.Escape && !e.IsRepeat)
            {
                _picked(_held.Modifiers, key);
            }
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        var key = KeyOf(e);
        var swallowed = _held.Released(key);
        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            e.Handled = true;
        }

        if (swallowed != ModifierKeys.None)
        {
            _swallowed(swallowed);
        }

        base.OnPreviewKeyUp(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        _held.Clear();
        base.OnLostKeyboardFocus(e);
    }

    // Alt arrives as Key.System, and an input method's keys as their own marker keys, each with the real key beside it.
    private static Key KeyOf(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        var key => key,
    };
}
