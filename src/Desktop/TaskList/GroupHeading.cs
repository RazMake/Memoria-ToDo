using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// A group's heading on the left side: the tag as the Task editor's badge, or "No tag" for the untagged tasks, and, while the
/// group is collapsed, how many tasks it holds. A click on the badge or the words asks to collapse or expand the group; a press on a
/// tag's heading that moves past the system's drag distance drags the group instead, and is no click.
/// </summary>
internal sealed class GroupHeading : Border
{
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Run _open = new();
    private readonly Run _number = new() { FontWeight = FontWeights.Bold };
    private readonly Run _close = new();
    private readonly string? _tag;
    private readonly IDragPointer _pointer;
    private readonly PressDrag _press = new();

    public GroupHeading(string? tag, ITagColors tagColors, ITheme theme, IDragPointer pointer)
    {
        _pointer = pointer;
        foreach (var piece in new[] { _open, _number, _close })
        {
            piece.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
            _count.Inlines.Add(piece);
        }

        _count.Margin = new Thickness(8, 0, 0, 0);
        _tag = tag;
        Margin = new Thickness(0, Gap, 0, 4);
        Cursor = Cursors.Hand;
        AutomationProperties.SetAutomationId(this, "GroupHeading");
        AutomationProperties.SetName(this, tag ?? TagEditor.NoTagText);
        UIElement label = tag is null
            ? new TextBlock { Text = TagEditor.NoTagText, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand }
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

        PreviewMouseLeftButtonDown += (_, _) => _press.Press(_pointer.PositionIn(this));
        PreviewMouseMove += (_, _) => OnPreviewMouseMove();
        MouseLeave += (_, _) => _press.Leave();
        MouseUp += (_, e) =>
        {
            if (_press.Release() && e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                Clicked?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    // A press that moves past the system's drag distance drags the group, and is no click. "No tag" can't be dragged.
    private void OnPreviewMouseMove()
    {
        if (_tag is { } tag && _press.Move(_pointer.PositionIn(this), _pointer.IsLeftButtonDown))
        {
            _pointer.StartDrag(this, new DraggedGroup(tag).ToData());
        }
    }

    /// <summary>The space above a heading, which the group before it ends in.</summary>
    public const double Gap = 12;

    /// <summary>Raised when the user clicks the heading's badge or words.</summary>
    public event EventHandler? Clicked;

    // The badge is drawn from a template that styles from outside can't reach, and a theme change draws it again, so every
    // layout gives the badge the hand cursor, and lines the count up with the badge's middle. The badge's margin below it
    // puts that middle above the line's middle; the same margin on the count lifts it there, and the opposite margin above
    // keeps its box no taller than its text.
    private void HandOverBadge(object? sender, EventArgs e)
    {
        if (FindBadge(this) is not { } badge)
        {
            return;
        }

        if (badge.Cursor != Cursors.Hand)
        {
            badge.Cursor = Cursors.Hand;
        }

        var lift = (badge.Margin.Bottom - badge.Margin.Top) / 2;
        var margin = new Thickness(_count.Margin.Left, -lift, 0, lift);
        if (_count.Margin != margin)
        {
            _count.Margin = margin;
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

    /// <summary>
    /// Shows the count of a collapsed group, such as "(3)", with only its digits bold, or nothing when it is empty text.
    /// The text around the digits is shown as given, so the count's format keeps its one home in the group plan.
    /// </summary>
    public void Show(string countText)
    {
        var start = countText.AsSpan().IndexOfAnyInRange('0', '9');
        var end = start < 0 ? 0 : countText.AsSpan().LastIndexOfAnyInRange('0', '9') + 1;
        start = Math.Max(start, 0);
        _open.Text = countText[..start];
        _number.Text = countText[start..end];
        _close.Text = countText[end..];
        _count.Visibility = countText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
