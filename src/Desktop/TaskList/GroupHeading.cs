using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// A group's heading on the left side: the tag as the Task editor's badge, or "No tag" for the untagged tasks, and, while the
/// group is collapsed, how many tasks it holds. A click on the badge or the words asks to collapse or expand the group.
/// </summary>
internal sealed class GroupHeading : Border
{
    public const string NoTagName = "No tag";

    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
    public GroupHeading(string? tag, ITagColors tagColors, ITheme theme)
    {
        Margin = new Thickness(0, 12, 0, 4);
        Cursor = Cursors.Hand;
        AutomationProperties.SetAutomationId(this, "GroupHeading");
        AutomationProperties.SetName(this, tag ?? NoTagName);
        UIElement label = tag is null
            ? new TextBlock { Text = NoTagName, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand }
            : new TagBadges(tagColors, theme) { Tags = [tag], HorizontalAlignment = HorizontalAlignment.Left };
        var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        line.Children.Add(label);
        line.Children.Add(_count);
        Child = line;
        if (tag is not null)
        {
            LayoutUpdated += HandOverBadge;
            Loaded += (_, _) =>
            {
                LayoutUpdated -= HandOverBadge;
                LayoutUpdated += HandOverBadge;
            };
            Unloaded += (_, _) => LayoutUpdated -= HandOverBadge;
        }

        MouseUp += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                Clicked?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>Raised when the user clicks the heading's badge or words.</summary>
    public event EventHandler? Clicked;

    // The badge is drawn from a template that styles from outside can't reach, and a theme change draws it again, so every
    // layout gives the badge the hand cursor.
    private void HandOverBadge(object? sender, EventArgs e)
    {
        if (FindBadge(this) is { } badge && badge.Cursor != Cursors.Hand)
        {
            badge.Cursor = Cursors.Hand;
        }
    }

    private static FrameworkElement? FindBadge(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element
                && AutomationProperties.GetAutomationId(element).StartsWith("Badge ", StringComparison.Ordinal))
            {
                return element;
            }

            if (FindBadge(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Shows the count of a collapsed group, such as "(3)", or nothing when it is empty text.</summary>
    public void Show(string countText)
    {
        _count.Text = countText;
        _count.Visibility = countText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
