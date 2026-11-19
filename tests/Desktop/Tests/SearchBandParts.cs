using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop;

/// <summary>Finds the parts of a search band in a laid-out visual tree, for every test that looks at the band. Shared test setup, not a fake.</summary>
internal static class SearchBandParts
{
    /// <summary>The box named "Search" below <paramref name="root"/>.</summary>
    public static TextBox SearchBoxIn(DependencyObject root) =>
        OpenWindows.Descendants(root).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Search");

    /// <summary>The element behind the search box that has a background: the band.</summary>
    public static FrameworkElement BandBehind(TextBox search)
    {
        for (var current = VisualTreeHelper.GetParent(search); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Panel { Background: not null } or Border { Background: not null })
            {
                return (FrameworkElement)current;
            }
        }

        throw new InvalidOperationException("Nothing with a background of its own sits behind the search box.");
    }

    /// <summary>Whether <paramref name="element"/> is <paramref name="ancestor"/> or sits below it in the visual tree.</summary>
    public static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}
