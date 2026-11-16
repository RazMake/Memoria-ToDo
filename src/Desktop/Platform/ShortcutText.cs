using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Writes a shortcut the way the app shows it to the user.</summary>
public static class ShortcutText
{
    /// <summary>
    /// The shortcut as the user reads it, such as <c>Win+Alt+N</c>: the modifiers in the order Win, Ctrl, Alt, Shift,
    /// then the key, with Enter written "Enter".
    /// </summary>
    public static string Of(ModifierKeys modifiers, Key key)
    {
        var keyName = key == Key.Enter ? "Enter" : key.ToString();
        return modifiers == ModifierKeys.None ? keyName : $"{ModifierKey.Describe(modifiers)}+{keyName}";
    }
}
