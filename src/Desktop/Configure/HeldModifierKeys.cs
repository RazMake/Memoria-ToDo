using System.Numerics;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Works out from key events which modifier keys are held, and spots a chord of two or more modifier keys that was
/// released with no other key in between: the sign of a shortcut that Windows or another app took.
/// </summary>
internal sealed class HeldModifierKeys
{
    private readonly HashSet<Key> _held = [];
    private ModifierKeys _chord;
    private bool _otherKeyReached;

    /// <summary>Whether any modifier key is held.</summary>
    public bool AnyHeld => _held.Count > 0;

    /// <summary>The modifiers of the keys held now.</summary>
    public ModifierKeys Modifiers
    {
        get
        {
            var modifiers = ModifierKeys.None;
            foreach (var key in _held)
            {
                modifiers |= ModifierKey.Of(key);
            }

            return modifiers;
        }
    }

    /// <summary>A modifier key went down. The first one held starts a new chord.</summary>
    public void Pressed(Key modifierKey)
    {
        if (_held.Count == 0)
        {
            _chord = ModifierKeys.None;
            _otherKeyReached = false;
        }

        _held.Add(modifierKey);
        _chord |= ModifierKey.Of(modifierKey);
    }

    /// <summary>A key that isn't a modifier key reached the field, so the chord isn't a swallowed shortcut.</summary>
    public void OtherKeyReached() => _otherKeyReached = true;

    /// <summary>
    /// A key went up. Returns the chord's modifiers when this was the last held modifier key and the chord had two or
    /// more modifiers and no other key; otherwise <see cref="ModifierKeys.None"/>.
    /// </summary>
    public ModifierKeys Released(Key key)
    {
        if (!_held.Remove(key) || _held.Count > 0)
        {
            return ModifierKeys.None;
        }

        var swallowed = !_otherKeyReached && BitOperations.PopCount((uint)_chord) >= 2 ? _chord : ModifierKeys.None;
        Clear();
        return swallowed;
    }

    /// <summary>Forgets everything, as when the field loses the focus.</summary>
    public void Clear()
    {
        _held.Clear();
        _chord = ModifierKeys.None;
        _otherKeyReached = false;
    }
}
