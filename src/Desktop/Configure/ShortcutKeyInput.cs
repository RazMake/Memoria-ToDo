using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// What a shortcut box does with its key events. It reports a pick on the first key that isn't a modifier. Plain Esc
/// picks nothing and is left to the window, which closes; Esc with a modifier held picks nothing and stays here. It handles every modifier key's release, Alt's included, so Windows doesn't move the keyboard to
/// the window's hidden system menu. It also reports two or more modifier keys pressed and released with no other key,
/// which is what a shortcut taken by Windows or another app looks like. Each method returns whether the box keeps
/// the key to itself.
/// </summary>
internal sealed class ShortcutKeyInput
{
    private readonly Action<ModifierKeys, Key> _picked;
    private readonly Action<ModifierKeys> _swallowed;
    private readonly HeldModifierKeys _held = new();

    /// <param name="picked">Called with the held modifiers and the key that completed a pick.</param>
    /// <param name="swallowed">Called with the modifiers of a chord released with no other key.</param>
    public ShortcutKeyInput(Action<ModifierKeys, Key> picked, Action<ModifierKeys> swallowed)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(swallowed);
        _picked = picked;
        _swallowed = swallowed;
    }

    /// <summary>A key went down. Plain Tab isn't handled, so the box doesn't trap the keyboard; nor is plain Esc.</summary>
    public bool KeyDown(Key key, bool isRepeat)
    {
        if (key == Key.None)
        {
            return false;
        }

        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            _held.Pressed(key);
            return true;
        }

        _held.OtherKeyReached();
        if (key is Key.Tab or Key.Escape && !_held.AnyHeld)
        {
            return false;
        }

        if (key != Key.Escape && !isRepeat)
        {
            _picked(_held.Modifiers, key);
        }

        return true;
    }

    /// <summary>A key went up.</summary>
    public bool KeyUp(Key key)
    {
        var swallowed = _held.Released(key);
        if (swallowed != ModifierKeys.None)
        {
            _swallowed(swallowed);
        }

        return ModifierKey.Of(key) != ModifierKeys.None;
    }

    /// <summary>The box lost the keyboard focus, so held keys are forgotten.</summary>
    public void FocusLost() => _held.Clear();
}
