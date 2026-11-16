using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;

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

    /// <summary>The Sync button, which sits in the status bar.</summary>
    public ButtonBase SyncButton => FindButton("Sync", Window) ?? throw new InvalidOperationException("No \"Sync\" button shows.");

    /// <summary>Whether the element is inside the status bar.</summary>
    public bool IsInStatusBar(DependencyObject element) => IsInside(element, StatusBar);

    /// <summary>The button that is the update text, such as "Update available: 2.2.0".</summary>
    public ButtonBase UpdateTextButton =>
        Buttons(StatusBar).Single(b => b.Content is TextBlock text && OpenWindows.TextOf(text).StartsWith("Update available", StringComparison.Ordinal));

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

    /// <summary>The shared sync service reports the state, as when a sync starts on its own after a save, and the window shows it.</summary>
    public void PublishSync(SyncState state)
    {
        services.Sync.Publish(new FakeSyncStatus { State = state });
        Refresh();
    }

    /// <summary>The color the Sync button is drawn in at its left edge, inside its border and clear of its icon, as the screen shows it.</summary>
    public Color SyncButtonFill
    {
        get
        {
            var button = SyncButton;
            var width = (int)Math.Ceiling(button.ActualWidth);
            var height = (int)Math.Ceiling(button.ActualHeight);
            var picture = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            picture.Render(button);
            var pixel = new byte[4];
            picture.CopyPixels(new Int32Rect(3, height / 2, 1, 1), pixel, 4, 0);
            return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
        }
    }

    /// <summary>The color of a brush of the window's theme, such as SystemFillColorSuccessBrush.</summary>
    public Color ThemeColor(string brushKey) => ((SolidColorBrush)Window.FindResource(brushKey)).Color;

    /// <summary>Gives the window the Fluent theme the platform gives the app's windows, so the theme's resources and control styles apply; a test window has none until then.</summary>
    public void UseTheme(bool dark = false)
    {
        OpenWindows.UseTheme(Window, dark);
        Refresh();
    }

    /// <summary>The search box.</summary>
    public TextBox SearchBox
    {
        get
        {
            Refresh();
            return Visuals(Window).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");
        }
    }

    /// <summary>The band behind the search box: its nearest ancestor with a background of its own inside the window's content.</summary>
    public FrameworkElement SearchBand
    {
        get
        {
            for (var current = VisualTreeHelper.GetParent(SearchBox); current is not null && current != Window.Content; current = VisualTreeHelper.GetParent(current))
            {
                if (current is Panel { Background: not null } or Border { Background: not null })
                {
                    return (FrameworkElement)current;
                }
            }

            throw new InvalidOperationException("Nothing with a background of its own sits behind the search box.");
        }
    }

    /// <summary>The color of the band's background.</summary>
    public Color SearchBandColor => SearchBand switch
    {
        Panel { Background: SolidColorBrush brush } => brush.Color,
        Border { Background: SolidColorBrush brush } => brush.Color,
        _ => throw new InvalidOperationException("The band's background isn't a solid color."),
    };

    /// <summary>The color of the status bar's background.</summary>
    public Color StatusBarColor => ((SolidColorBrush)StatusBar.Background).Color;

    /// <summary>The color of the window's background.</summary>
    public Color WindowColor => ((SolidColorBrush)Window.Background).Color;

    /// <summary>The window's content width: the band's right edge meets it when the band runs edge to edge.</summary>
    public double ContentRight => RightOf((FrameworkElement)Window.Content);

    /// <summary>The Undo button of the bar that shows after an edit is discarded.</summary>
    public ButtonBase UndoButton => FindButton("Undo", Window) ?? throw new InvalidOperationException("No \"Undo\" button shows.");

    /// <summary>Whether the element is inside the ancestor.</summary>
    public bool IsInside(FrameworkElement element, FrameworkElement ancestor) => IsInside((DependencyObject)element, ancestor);
}
