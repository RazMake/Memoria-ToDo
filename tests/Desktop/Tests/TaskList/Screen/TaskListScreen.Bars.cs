using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a Task View test reaches the status bar, the group headings' collapse, and the focus rules.
/// The status bar is Updates' <see cref="Updates.UpdateStatusBar"/> (see TaskListScreen.Updates); its start holds the Sync button, named "Sync" with a tooltip, then the error, and the version, as text.
/// A group's tag badge has the automation id "Badge {tag}" (see
/// <see cref="TaskEditor.Tests.TagControls"/>); the "No tag" heading has no badge, so a click lands on the heading itself.
/// Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>The status bar's Sync button.</summary>
    public ButtonBase SyncButton => Buttons(StatusBar).Single(button => LabelOf(button) == "Sync");

    /// <summary>The Sync button's tooltip text.</summary>
    public string? SyncTooltip => SyncButton.ToolTip as string;

    /// <summary>Gives the Sync button the keyboard focus, as tabbing to it does.</summary>
    public void FocusSyncButton()
    {
        var button = SyncButton;
        button.Focus();
        Keyboard.Focus(button);
    }

    /// <summary>Whether the status bar shows the text, such as the sync error or the version.</summary>
    public bool StatusBarShowsText(string words) => ShowsText(words, StatusBar);

    /// <summary>The window's x of the first text in the status bar that contains the words.</summary>
    public double StatusBarTextLeft(string words) =>
        Visuals(StatusBar).OfType<TextBlock>().Where(b => b.IsVisible && OpenWindows.TextOf(b).Contains(words))
            .Min(b => b.TranslatePoint(new Point(0, 0), Window).X);

    /// <summary>The window's x of the right edge of the first text in the status bar that contains the words.</summary>
    public double StatusBarTextRight(string words) =>
        Visuals(StatusBar).OfType<TextBlock>().Where(b => b.IsVisible && OpenWindows.TextOf(b).Contains(words))
            .Min(b => b.TranslatePoint(new Point(b.ActualWidth, 0), Window).X);

    /// <summary>The window's x of the right edge of the Sync button.</summary>
    public double SyncButtonRight => SyncButton.TranslatePoint(new Point(SyncButton.ActualWidth, 0), Window).X;

    /// <summary>Whether Tab lands on the status bar itself, as it does on its Sync button.</summary>
    public bool StatusBarIsTabStop => IsKeyboardStop(StatusBar);

    /// <summary>Whether a text with the words shows next to the search box as its label.</summary>
    public bool ShowsSearchLabel => ShowsText("Search:");

    /// <summary>Clicks the group's tag badge, or the "No tag" heading, as the mouse does.</summary>
    public void ToggleGroup(string group)
    {
        MouseClickOn(GroupClickTarget(group));
        Refresh();
    }

    /// <summary>The pointer shown over the group's tag badge, or over the "No tag" heading.</summary>
    public Cursor? GroupCursor(string group) => GroupClickTarget(group).Cursor;

    private FrameworkElement GroupClickTarget(string group)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return Visuals(heading).OfType<FrameworkElement>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e).StartsWith("Badge ", StringComparison.Ordinal))
            ?? heading;
    }

    /// <summary>The text of the group's heading, such as "(3)" after the badge while the group is collapsed.</summary>
    public string HeadingText(string group)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return string.Join(" ", TextsWithin(heading));
    }

    /// <summary>
    /// What the group's collapsed heading shows as its count, such as "(3)", piece by piece: each piece's text, the weight it
    /// is drawn in, and the color it is drawn in. The count is the one text in the heading that reads as a number in parentheses.
    /// </summary>
    public IReadOnlyList<(string Text, FontWeight Weight, System.Windows.Media.Color Color)> HeadingCountPieces(string group) =>
        [.. Pieces(HeadingCount(group).Inlines)];

    /// <summary>How far below the middle of the group's badge (of the "No tag" words, for that heading) the middle of its count lies; negative when above.</summary>
    public double HeadingCountOffsetFromBadgeCenter(string group)
    {
        var count = HeadingCount(group);
        return CenterY(count) - CenterY(HeadingMark(group));
    }

    /// <summary>How much taller the count's box is than its text needs, which is more than 0 when it is stretched to the heading's height, so its text sits at the top.</summary>
    public double HeadingCountExtraHeight(string group)
    {
        var count = HeadingCount(group);
        return count.ActualHeight - count.DesiredSize.Height;
    }

    private TextBlock HeadingCount(string group)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return Visuals(heading).OfType<TextBlock>()
            .Single(block => block.IsVisible && System.Text.RegularExpressions.Regex.IsMatch(OpenWindows.TextOf(block), @"^\( \d+ \)$"));
    }

    // The badge of a tag's heading, or the words of the "No tag" heading.
    private FrameworkElement HeadingMark(string group)
    {
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return Visuals(heading).OfType<FrameworkElement>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e).StartsWith("Badge ", StringComparison.Ordinal))
            ?? Visuals(heading).OfType<TextBlock>().First(block => block.IsVisible && OpenWindows.TextOf(block) == group);
    }

    private double CenterY(FrameworkElement element) =>
        element.TranslatePoint(new Point(0, element.ActualHeight / 2), Window).Y;

    private static IEnumerable<(string Text, FontWeight Weight, System.Windows.Media.Color Color)> Pieces(System.Windows.Documents.InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is System.Windows.Documents.Span span)
            {
                foreach (var piece in Pieces(span.Inlines))
                {
                    yield return piece;
                }
            }
            else if (inline is System.Windows.Documents.Run run)
            {
                var color = run.Foreground is System.Windows.Media.SolidColorBrush solid
                    ? solid.Color
                    : throw new InvalidOperationException("The count isn't drawn in one solid color.");
                yield return (run.Text, run.FontWeight, color);
            }
        }
    }

    /// <summary>How far the row's left edge lies right of its group heading's left edge.</summary>
    public double RowIndent(string title, string group)
    {
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return Row(title, group).TranslatePoint(new Point(0, 0), Window).X - heading.TranslatePoint(new Point(0, 0), Window).X;
    }

    /// <summary>Whether the keyboard focus is in the splitter or the splitter can take it.</summary>
    public bool SplitterIsTabStop
    {
        get
        {
            Refresh();
            var splitter = Visuals(Window).OfType<GridSplitter>().Single(s => s.IsVisible);
            return splitter.Focusable || KeyboardNavigation.GetIsTabStop(splitter);
        }
    }

    /// <summary>Whether the list's scroll viewer, or the element holding its rows, is a keyboard stop.</summary>
    public bool ListContainerIsTabStop
    {
        get
        {
            Refresh();
            var row = Rows().First();
            for (DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(row); parent is not null and not System.Windows.Window;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
            {
                if (parent is (ScrollViewer or ItemsControl) and FrameworkElement { Focusable: true } stop
                    && KeyboardNavigation.GetIsTabStop(stop))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Whether the row is a keyboard stop, so Tab reaches it.</summary>
    public bool RowIsTabStop(string title) => Row(title).Focusable && KeyboardNavigation.GetIsTabStop(Row(title));

    /// <summary>Whether the row draws no dashed focus rectangle of its own.</summary>
    public bool RowHasNoFocusRectangle(string title) => Row(title).FocusVisualStyle is null;

    private static void MouseClickOn(FrameworkElement target) => MouseClick(target, MouseButton.Left);

    /// <summary>Whether a Title field shows anywhere in the window.</summary>
    public bool ShowsTitleField => TextFields.FindShowing(Window, "Title") is not null;

    /// <summary>Whether the row's checkbox can be clicked.</summary>
    public bool CheckBoxIsEnabled(string title) => Visuals(Row(title)).OfType<CheckBox>().Single().IsEnabled;
}
