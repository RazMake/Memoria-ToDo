using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Finds the Prevent screen saver sun in a status bar, by what it shows: the text U+E706. Shared test setup, so no test depends on a
/// private element.
/// </summary>
internal static class KeepAwakeIcon
{
    public const string Sun = "\uE706";

    /// <summary>The one text block under the root that shows the sun, or null when none shows it.</summary>
    public static TextBlock? In(DependencyObject root) =>
        OpenWindows.Descendants(root).OfType<TextBlock>().SingleOrDefault(block => OpenWindows.TextOf(block) == Sun);

    /// <summary>The icon's tooltip text, whether it was set as a string or as a tooltip's content.</summary>
    public static string? ToolTipOf(TextBlock icon) =>
        icon.ToolTip is ToolTip { Content: var content } ? content?.ToString() : icon.ToolTip?.ToString();

    /// <summary>The name a screen reader reads for the icon.</summary>
    public static string NameOf(TextBlock icon) => AutomationProperties.GetName(icon);
}
