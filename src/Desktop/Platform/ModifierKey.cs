using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Tells which keys are the modifier keys Ctrl, Alt, Shift, and Win.</summary>
public static class ModifierKey
{
    /// <summary>The modifier the key stands for, left or right, or <see cref="ModifierKeys.None"/> for any other key.</summary>
    public static ModifierKeys Of(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None,
    };

    /// <summary>The modifiers as the user reads them, in the order Win, Ctrl, Alt, Shift, such as <c>Win+Alt</c>.</summary>
    public static string Describe(ModifierKeys modifiers)
    {
        var names = new List<string>(4);
        Add(ModifierKeys.Windows, "Win");
        Add(ModifierKeys.Control, "Ctrl");
        Add(ModifierKeys.Alt, "Alt");
        Add(ModifierKeys.Shift, "Shift");
        return string.Join('+', names);

        void Add(ModifierKeys modifier, string name)
        {
            if (modifiers.HasFlag(modifier))
            {
                names.Add(name);
            }
        }
    }
}
