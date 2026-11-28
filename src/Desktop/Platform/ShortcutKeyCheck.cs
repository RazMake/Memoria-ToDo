using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The one rule for which modifiers and key can make a shortcut.</summary>
internal static class ShortcutKeyCheck
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> when key is <see cref="Key.None"/> or a modifier key, or modifiers
    /// holds anything but Ctrl, Alt, Shift, and Win.
    /// </summary>
    public static void Validate(ModifierKeys modifiers, Key key)
    {
        const ModifierKeys All = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;
        if ((modifiers & ~All) != 0)
        {
            throw new ArgumentException("Only Ctrl, Alt, Shift, and Win can be modifiers.", nameof(modifiers));
        }

        if (key is Key.None or Key.System || ModifierKey.Of(key) != ModifierKeys.None)
        {
            throw new ArgumentException("The key can't be none or a modifier key.", nameof(key));
        }
    }
}
