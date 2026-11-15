using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Makes the window's buttons, which show the hand pointer so the user sees what can be clicked.</summary>
internal static class ConfigureButton
{
    /// <summary>A button with the text, left-aligned; the caller adds its Click handler.</summary>
    public static Button Make(string content, Thickness margin = default) => new()
    {
        Content = content,
        Margin = margin,
        HorizontalAlignment = HorizontalAlignment.Left,
        Cursor = Cursors.Hand,
    };
}
