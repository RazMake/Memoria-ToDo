using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The top of Task View: the message line and Undo bar under the search band, and the search the box sets on the filter. Measured in a bare
/// container with no window or theme; the container holds the theme resource the band's color comes from. The band's own look is in
/// the platform's search band tests.
/// </summary>
[Unit]
public class ListHeaderTests
{
    private const string BandKey = "CardBackgroundFillColorDefaultBrush";

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
    public void The_message_line_and_the_Undo_bar_stay_under_the_band()
    {
        var host = new Host(width: 800);
        host.Header.ShowMessage("Could not save");
        host.Header.ShowUndo("Deleted Buy milk");
        host.Layout();

        var band = host.Band;
        var message = host.TextBlockShowing("Could not save");
        var undo = host.TextBlockShowing("Deleted Buy milk");

        Assert.False(SearchBandParts.IsInside(message, band), "The message line is in the band.");
        Assert.False(SearchBandParts.IsInside(undo, band), "The Undo bar is in the band.");
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

        public ListFilter Filter { get; } = new(new ListTasks().Contacts);

        public ListHeader Header { get; }

        public DockPanel Container { get; }

        public TextBox Search => SearchBandParts.SearchBoxIn(Header);

        public FrameworkElement Band => SearchBandParts.BandBehind(Search);

        public FrameworkElement TextBlockShowing(string text) =>
            OpenWindows.Descendants(Header).OfType<TextBlock>().Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block) == text);

        public double TopOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Container).Y;

        public void Layout() => Layout(Container.Width);

        private void Layout(double width)
        {
            Container.InvalidateMeasure(); // A change below a container with no window doesn't reach it by itself.
            Container.Measure(new Size(width, 600));
            Container.Arrange(new Rect(0, 0, width, 600));
        }
    }
}
