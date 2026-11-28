using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Moving the keyboard cursor.</summary>
internal static class KeyboardFocus
{
    /// <summary>Puts the keyboard cursor in the element.</summary>
    public static void MoveTo(UIElement element)
    {
        element.Focus();
        Keyboard.Focus(element);
    }
}
