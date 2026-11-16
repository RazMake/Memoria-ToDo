using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// A title in a heading's look from <see cref="TextLooks"/>: the heading's color for the theme, bold, and the
/// heading's multiple of the window's normal text size. It has no colors or sizes of its own and follows theme changes.
/// </summary>
internal static class HeadingText
{
    /// <summary>A title that shows in the look of heading <paramref name="level"/>.</summary>
    public static TextBlock Make(string text, int level, ITheme theme)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold };
        // The follower stays alive through its event subscriptions on the block and the theme.
        _ = new ThemeFollower(block, theme, () => Show(block, level, theme));
        return block;
    }

    private static void Show(TextBlock block, int level, ITheme theme)
    {
        var color = (Color)ColorConverter.ConvertFromString(TextLooks.Of(theme.IsDark).HeadingColor(level));
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        // The normal size is the window's: clear the block's own size first, so a second call doesn't scale the first's.
        block.ClearValue(TextBlock.FontSizeProperty);
        block.Foreground = brush;
        block.FontSize = TextLooks.HeadingScale(level) * TextElement.GetFontSize(Window.GetWindow(block) ?? (DependencyObject)block);
    }
}
