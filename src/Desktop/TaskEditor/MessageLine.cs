using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>An editor's one line for why something failed, shown under its fields and hidden while it has no text.</summary>
internal sealed class MessageLine : TextBlock
{
    public MessageLine()
    {
        TextWrapping = TextWrapping.Wrap;
        Foreground = Brushes.Firebrick;
        Margin = new Thickness(0, 0, 0, 8);
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Shows the text, or hides the line when the text is empty.</summary>
    public void Show(string text)
    {
        Text = text;
        Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
