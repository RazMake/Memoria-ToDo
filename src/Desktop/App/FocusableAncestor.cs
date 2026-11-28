using System.Windows;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.App;

internal static class FocusableAncestor
{
    public static UIElement? Of(DependencyObject child)
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is UIElement { Focusable: true, IsEnabled: true } target)
            {
                return target;
            }
        }

        return null;
    }
}
