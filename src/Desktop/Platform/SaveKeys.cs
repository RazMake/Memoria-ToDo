using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// The keys that save the task being edited, in whichever of the app's windows edits one.
/// The composition root makes one for the app. Use it on the UI thread.
/// </summary>
public sealed class SaveKeys
{
    private ModifierKeys _modifiers;
    private Key _key = Key.None;

    /// <summary>No keys yet: no key press is the Save shortcut until <see cref="Set"/> is called.</summary>
    public SaveKeys()
    {
    }

    /// <summary>
    /// From now on, these keys save. Throws <see cref="ArgumentException"/> when key is <see cref="Key.None"/> or a
    /// modifier key, or modifiers holds anything but Ctrl, Alt, Shift, and Win.
    /// </summary>
    public void Set(ModifierKeys modifiers, Key key)
    {
        ShortcutKeyCheck.Validate(modifiers, key);
        _modifiers = modifiers;
        _key = key;
        Text = ShortcutText.Of(modifiers, key);
    }

    /// <summary>The Save shortcut as the user reads it, such as <c>Ctrl+Enter</c>; empty until <see cref="Set"/> is called.</summary>
    public string Text { get; private set; } = "";

    /// <summary>Whether key, pressed with exactly these modifier keys held, is the Save shortcut now.</summary>
    public bool Matches(ModifierKeys modifiers, Key key) => key != Key.None && key == _key && modifiers == _modifiers;
}
