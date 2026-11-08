using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Makes the window's text blocks in the app's theme, so they follow light and dark mode. Styles and colors are
/// resource references, which stay empty rather than throw when the theme doesn't have a key.
/// </summary>
internal static class ThemedText
{
    /// <summary>A section heading.</summary>
    public static TextBlock Heading(string text, Thickness margin) =>
        Make(text, margin, style: "SubtitleTextBlockStyle", brush: null);

    /// <summary>Help text: small, in the theme's secondary color, and muted further so it stays behind the controls.</summary>
    public static TextBlock Help(string text, Thickness margin)
    {
        var block = Make(text, margin, style: "CaptionTextBlockStyle", brush: "TextFillColorSecondaryBrush");
        block.Opacity = 0.7;
        return block;
    }

    /// <summary>An empty, hidden message in the theme's error color. <see cref="ShowMessage"/> sets it.</summary>
    public static TextBlock Message()
    {
        var message = Make("", new Thickness(0, 4, 0, 0), style: "CaptionTextBlockStyle", brush: "SystemFillColorCriticalBrush");
        message.Visibility = Visibility.Collapsed;
        return message;
    }

    /// <summary>Shows the text in a <see cref="Message"/>, or hides the message when the text is empty.</summary>
    public static void ShowMessage(TextBlock message, string? text)
    {
        message.Text = text ?? "";
        message.Visibility = message.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static TextBlock Make(string text, Thickness margin, string? style, string? brush)
    {
        var block = new TextBlock { Text = text, Margin = margin, TextWrapping = TextWrapping.Wrap };
        if (style is not null)
        {
            block.SetResourceReference(FrameworkElement.StyleProperty, style);
        }

        if (brush is not null)
        {
            block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        }

        return block;
    }
}
