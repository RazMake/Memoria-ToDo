using System.Windows;
using System.Windows.Documents;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The help style: the theme's secondary text color, for a row's title, the details' labels, and the problems line.</summary>
internal static class ContactHelpStyle
{
    private const string Brush = "TextFillColorSecondaryBrush";

    public static void Apply(FrameworkElement element) => element.SetResourceReference(TextElement.ForegroundProperty, Brush);

    public static void Apply(FrameworkContentElement element) => element.SetResourceReference(TextElement.ForegroundProperty, Brush);
}
