using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>How a test reads the status bar along Task View's bottom edge: the Sync button and its failure line at its start, the update text at its end.</summary>
internal sealed partial class TaskListScreen
{
    /// <summary>The status bar along the window's bottom edge.</summary>
    public UpdateStatusBar StatusBar
    {
        get
        {
            Refresh();
            return Visuals(Window).OfType<UpdateStatusBar>().Single();
        }
    }

    /// <summary>Whether the status bar shows.</summary>
    public bool StatusBarShows => StatusBar.IsVisible;

    /// <summary>The texts the status bar shows, left to right.</summary>
    public IReadOnlyList<string> StatusBarTexts =>
        [.. Visuals(StatusBar).OfType<TextBlock>().Where(b => b.IsVisible).OrderBy(b => LeftOf(b)).Select(OpenWindows.TextOf).Where(t => t.Length > 0)];

    /// <summary>Whether the element is inside the status bar.</summary>
    public bool IsInStatusBar(DependencyObject element) => IsInside(element, StatusBar);

    /// <summary>The button that is the update text, such as "Update available: 2.2.0".</summary>
    public ButtonBase UpdateTextButton => Buttons(StatusBar).Single(b => b.Content is TextBlock);

    /// <summary>The state changes while the window is open, as when a check ends.</summary>
    public void PublishUpdate(UpdateStage stage, string? newVersion)
    {
        services.Updates.Publish(new UpdateState(stage, "2.1.0", newVersion, null));
        Refresh();
    }

    /// <summary>The window's x of the element's left edge.</summary>
    public double LeftOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Window).X;

    /// <summary>The window's x of the element's right edge.</summary>
    public double RightOf(FrameworkElement element) => element.TranslatePoint(new Point(element.ActualWidth, 0), Window).X;

    /// <summary>The window's y of the element's top edge.</summary>
    public double TopOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Window).Y;

    /// <summary>The window's y of the element's bottom edge.</summary>
    public double BottomOf(FrameworkElement element) => element.TranslatePoint(new Point(0, element.ActualHeight), Window).Y;

    /// <summary>The window's content area height: the bar's bottom edge meets it.</summary>
    public double ContentBottom => BottomOf((FrameworkElement)Window.Content);

    /// <summary>The failure line of the last sync, as a text block, or null when none shows.</summary>
    public TextBlock? FailureLine(string words) =>
        Visuals(Window).OfType<TextBlock>().FirstOrDefault(b => b.IsVisible && OpenWindows.TextOf(b).Contains(words, StringComparison.OrdinalIgnoreCase));
}
