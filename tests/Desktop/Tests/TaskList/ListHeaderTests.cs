using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The search band at the top of Task View: the band behind the search box and the tag filter, how wide and tall it is, and
/// the message line and Undo bar under it. Measured in a bare container with no window or theme, so the numbers are the
/// header's own; the container holds the theme resource the band's color comes from.
/// </summary>
[Unit]
public class ListHeaderTests
{
    private const string BandKey = "CardBackgroundFillColorDefaultBrush";
    private const double SmallestSearchWidth = 200;
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

    [WpfFact]
    public void The_search_box_takes_the_free_width_beside_the_tag_filter()
    {
        var narrower = new Host(width: 700).Search.ActualWidth;
        var wider = new Host(width: 1000).Search.ActualWidth;

        Assert.True(wider - narrower >= 250, $"The box grew from {narrower} to {wider} when the band grew by 300.");
    }

    [WpfFact]
    public void The_search_box_keeps_a_smallest_width_and_the_tag_filter_wraps_below_it()
    {
        var host = new Host(width: 300);

        Assert.True(host.Search.ActualWidth >= SmallestSearchWidth, $"The box shrank to {host.Search.ActualWidth}.");
        Assert.True(host.TopOf(host.AllTags) >= host.TopOf(host.Search) + host.Search.ActualHeight - 0.5, "The tag filter didn't wrap.");
    }

    [WpfFact]
    public void The_band_is_one_row_of_controls_tall_with_small_padding_when_the_window_is_wide_enough()
    {
        var host = new Host(width: 1000);

        Assert.InRange(host.TopOf(host.AllTags) - host.TopOf(host.Search), -1, 1);
        var controls = Math.Max(host.Search.ActualHeight, host.AllTags.ActualHeight);
        Assert.InRange(host.Band.ActualHeight - controls, 0, MostExtraBandHeight);
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
            var services = new ListServices();
            Header = new ListHeader(services.Tasks, services.TagColors, services.Theme, new ListFilter());
            Container = new DockPanel { LastChildFill = false, Width = width };
            Container.Resources[BandKey] = new SolidColorBrush(Colors.Red);
            DockPanel.SetDock(Header, Dock.Top);
            Container.Children.Add(Header);
            Layout(width);
        }

        public ListHeader Header { get; }

        public DockPanel Container { get; }

        public TextBox Search => OpenWindows.Descendants(Header).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Search");

        public ComboBox AllTags => OpenWindows.Descendants(Header).OfType<ComboBox>().Single(box => AutomationProperties.GetName(box) == "All tags");

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
