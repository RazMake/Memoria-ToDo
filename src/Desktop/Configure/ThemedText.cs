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

    /// <summary>A line of text in the theme's secondary color.</summary>
    public static TextBlock Secondary(string text, Thickness margin) =>
        Make(text, margin, style: null, brush: "TextFillColorSecondaryBrush");

    /// <summary>A small line of text in the theme's secondary color.</summary>
    public static TextBlock Footnote(string text, Thickness margin) =>
        Make(text, margin, style: "CaptionTextBlockStyle", brush: "TextFillColorSecondaryBrush");

    /// <summary>
    /// An empty message in the theme's error color. It keeps room for two lines, so the window keeps its size when a
    /// message comes and goes.
    /// </summary>
    public static TextBlock Message()
    {
        var message = Make("", new Thickness(0, 4, 0, 0), style: "CaptionTextBlockStyle", brush: "SystemFillColorCriticalBrush");
        message.MinHeight = 32;
        return message;
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
