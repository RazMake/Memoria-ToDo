using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Makes the window's rounded boxes in the app's theme, so they follow light and dark mode. Like
/// <see cref="ThemedText"/>, the colors are resource references, which stay empty rather than throw when the theme
/// doesn't have a key.
/// </summary>
internal static class ThemedBox
{
    /// <summary>A rounded box for one section.</summary>
    public static Border Section(UIElement child) => Make(child, "CardBackgroundFillColorDefaultBrush");

    /// <summary>A rounded box inside a section, with a small bold title above its content; it stands out from the section.</summary>
    public static Border Card(string title, UIElement content)
    {
        var inside = new StackPanel();
        inside.Children.Add(ThemedText.Subheading(title, new Thickness(0, 0, 0, 6)));
        inside.Children.Add(content);
        return Make(inside, "CardBackgroundFillColorSecondaryBrush");
    }

    private static Border Make(UIElement child, string background)
    {
        var box = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
        };
        box.SetResourceReference(Border.BackgroundProperty, background);
        box.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return box;
    }
}
