using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The search band at the top of Task View: the band behind the "Search:" label and the search box, how wide and tall it is, and
/// the message line and Undo bar under it. Measured in a bare container with no window or theme, so the numbers are the
/// header's own; the container holds the theme resource the band's color comes from.
/// </summary>
[Unit]
public class ListHeaderTests
{
    private const string BandKey = "CardBackgroundFillColorDefaultBrush";
    private const double MostExtraBandHeight = 12;

    [WpfFact]
    public void The_band_behind_the_search_box_runs_from_the_left_edge_to_the_right_edge()
    {
        var host = new Host(width: 800);

        var band = host.Band;

        Assert.InRange(host.LeftOf(band), -0.5, 0.5);
        Assert.InRange(band.ActualWidth, 799.5, 800.5);
    }

    [WpfFact]
    public void The_band_takes_its_background_from_the_status_bars_theme_resource_and_follows_it()
    {
        var host = new Host(width: 800);
        Assert.Equal(Colors.Red, host.BandColor);

        host.Container.Resources[BandKey] = new SolidColorBrush(Colors.Blue);

        Assert.Equal(Colors.Blue, host.BandColor);
    }

    [WpfTheory]
    [InlineData(200)]
    [InlineData(1000)]
    public void The_search_box_is_180_wide_whatever_the_band_width(double width)
    {
        var host = new Host(width);

        Assert.InRange(host.Search.ActualWidth, 179.5, 180.5);
    }

    [WpfFact]
    public void The_band_has_no_button()
    {
        var host = new Host(width: 800);

        Assert.DoesNotContain(OpenWindows.Descendants(host.Header).OfType<Button>(), button => host.IsInside(button, host.Band));
    }

    [WpfFact]
    public void Each_change_to_the_box_sets_the_search_at_once()
    {
        var host = new Host(width: 800);

        host.Search.Text = "mi";
        Assert.Equal("mi", host.Filter.SearchText);

        host.Search.Text = "";
        Assert.Equal("", host.Filter.SearchText);
    }

    [WpfFact]
    public void The_band_is_one_row_of_controls_tall_with_small_padding()
    {
        var host = new Host(width: 1000);

        Assert.InRange(host.Band.ActualHeight - host.Search.ActualHeight, 0, MostExtraBandHeight);
    }

    [WpfFact]
    public void The_message_line_and_the_Undo_bar_stay_under_the_band()
    {
        var host = new Host(width: 800);
        host.Header.ShowMessage("Could not save");
        host.Header.ShowUndo("Deleted Buy milk");
        host.Layout();

        var band = host.Band;
        var message = host.TextBlockShowing("Could not save");
        var undo = host.TextBlockShowing("Deleted Buy milk");

        Assert.False(host.IsInside(message, band), "The message line is in the band.");
        Assert.False(host.IsInside(undo, band), "The Undo bar is in the band.");
        Assert.True(host.TopOf(message) >= host.TopOf(band) + band.ActualHeight - 0.5, "The message line isn't under the band.");
        Assert.True(host.TopOf(undo) >= host.TopOf(band) + band.ActualHeight - 0.5, "The Undo bar isn't under the band.");
    }

    private sealed class Host
    {
        public Host(double width)
        {
            Header = new ListHeader(Filter);
            Container = new DockPanel { LastChildFill = false, Width = width };
            Container.Resources[BandKey] = new SolidColorBrush(Colors.Red);
            DockPanel.SetDock(Header, Dock.Top);
            Container.Children.Add(Header);
            Layout(width);
        }

        public ListFilter Filter { get; } = new();

        public ListHeader Header { get; }

        public DockPanel Container { get; }

        public TextBox Search => OpenWindows.Descendants(Header).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Search");

        /// <summary>The element behind the search box that has a background: the band.</summary>
        public FrameworkElement Band
        {
            get
            {
                for (var current = VisualTreeHelper.GetParent(Search); current is not null; current = VisualTreeHelper.GetParent(current))
                {
                    if (current is Panel { Background: not null } or Border { Background: not null })
                    {
                        return (FrameworkElement)current;
                    }
                }

                throw new InvalidOperationException("Nothing with a background of its own sits behind the search box.");
            }
        }

        public Color BandColor => Band switch
        {
            Panel { Background: SolidColorBrush brush } => brush.Color,
            Border { Background: SolidColorBrush brush } => brush.Color,
            _ => throw new InvalidOperationException("The band's background isn't a solid color."),
        };

        public FrameworkElement TextBlockShowing(string text) =>
            OpenWindows.Descendants(Header).OfType<TextBlock>().Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block) == text);

        public double LeftOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Container).X;

        public double TopOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Container).Y;

        public bool IsInside(DependencyObject element, DependencyObject ancestor)
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

        public void Layout() => Layout(Container.Width);

        private void Layout(double width)
        {
            Container.InvalidateMeasure(); // A change below a container with no window doesn't reach it by itself.
            Container.Measure(new Size(width, 600));
            Container.Arrange(new Rect(0, 0, width, 600));
        }
    }
}
