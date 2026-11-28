using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// A window's line for why the last action failed: wrapping text in the theme's critical color,
/// hidden while it has no text.
/// </summary>
public sealed class MessageLine : TextBlock
{
    public MessageLine()
    {
        TextWrapping = TextWrapping.Wrap;
        Visibility = Visibility.Collapsed;
        SetResourceReference(ForegroundProperty, "SystemFillColorCriticalBrush");
    }

    /// <summary>Shows the text, or hides the line when it's null or empty.</summary>
    public void Show(string? text)
    {
        Text = text ?? "";
        Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
