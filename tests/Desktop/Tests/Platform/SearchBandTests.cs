using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The search band: the band behind the "Search:" label and the search box, and how wide and tall it is. Measured in a bare
/// container with no window or theme, so the numbers are the band's own; the container holds the theme resource the band's color comes from.
/// </summary>
[Unit]
public class SearchBandTests
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

        Assert.DoesNotContain(OpenWindows.Descendants(host.Bar).OfType<Button>(), button => SearchBandParts.IsInside(button, host.Band));
    }

    [WpfFact]
    public void The_band_is_one_row_of_controls_tall_with_small_padding()
    {
        var host = new Host(width: 1000);

        Assert.InRange(host.Band.ActualHeight - host.Search.ActualHeight, 0, MostExtraBandHeight);
    }

    private sealed class Host
    {
        public Host(double width)
        {
            Bar = new SearchBand();
            Container = new DockPanel { LastChildFill = false, Width = width };
            Container.Resources[BandKey] = new SolidColorBrush(Colors.Red);
            DockPanel.SetDock(Bar, Dock.Top);
            Container.Children.Add(Bar);
            Container.Measure(new Size(width, 600));
            Container.Arrange(new Rect(0, 0, width, 600));
        }

        public SearchBand Bar { get; }

        public DockPanel Container { get; }

        public TextBox Search => SearchBandParts.SearchBoxIn(Bar);

        public FrameworkElement Band => SearchBandParts.BandBehind(Search);

        public Color BandColor => Band switch
        {
            Panel { Background: SolidColorBrush brush } => brush.Color,
            Border { Background: SolidColorBrush brush } => brush.Color,
            _ => throw new InvalidOperationException("The band's background isn't a solid color."),
        };

        public double LeftOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Container).X;
    }
}
