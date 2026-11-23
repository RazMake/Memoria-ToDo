using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests.StatusBar;

/// <summary>
/// How tall the status bar of Add and Task View is, and where its background color comes from. Measured in a bare
/// container, with no window or theme, so the numbers are the bar's own.
/// </summary>
[Unit]
public class UpdateStatusBarLookTests
{
    private const string BackgroundKey = "CardBackgroundFillColorDefaultBrush";
    private const double MostExtraHeight = 4;

    [WpfFact]
    public void Is_no_taller_than_the_update_text_plus_a_small_padding()
    {
        var (bar, _) = Measured(start: null, UpdateStage.Updating);

        var text = Assert.IsType<TextBlock>(bar.Child is DockPanel panel ? panel.Children[0] : null);
        Assert.InRange(bar.DesiredSize.Height - text.DesiredSize.Height, 0, MostExtraHeight);
    }

    [WpfFact]
    public void Is_no_taller_than_its_start_element_plus_a_small_padding_when_it_holds_one()
    {
        var sync = new Button { Content = "Sync", Padding = new Thickness(0) };

        var (bar, _) = Measured(sync, UpdateStage.NoUpdate);

        Assert.InRange(bar.DesiredSize.Height - sync.DesiredSize.Height, 0, MostExtraHeight);
    }

    [WpfFact]
    public void Is_no_taller_than_the_compact_start_element_when_an_update_is_available_too()
    {
        var sync = new Button { Content = "Sync", Padding = new Thickness(0) };

        var (bar, _) = Measured(sync, UpdateStage.Available);

        Assert.InRange(bar.DesiredSize.Height - sync.DesiredSize.Height, 0, MostExtraHeight);
    }

    [WpfFact]
    public void Takes_its_background_from_a_theme_resource()
    {
        var (bar, container) = Measured(start: null, UpdateStage.Updating);
        container.Resources[BackgroundKey] = new SolidColorBrush(Colors.Red);
        container.UpdateLayout();

        Assert.Equal(Colors.Red, ((SolidColorBrush)bar.Background).Color);
    }

    [WpfFact]
    public void Follows_the_theme_resource_when_it_changes()
    {
        var (bar, container) = Measured(start: null, UpdateStage.Updating);
        container.Resources[BackgroundKey] = new SolidColorBrush(Colors.Red);

        container.Resources[BackgroundKey] = new SolidColorBrush(Colors.Blue);

        Assert.Equal(Colors.Blue, ((SolidColorBrush)bar.Background).Color);
    }

    [WpfFact]
    public void Has_the_same_background_without_an_update_when_it_holds_a_start_element()
    {
        var container = new Grid();
        container.Resources[BackgroundKey] = new SolidColorBrush(Colors.Green);
        var bar = new UpdateStatusBar(new FakeUpdateStatus(), new Button { Content = "Sync" });
        container.Children.Add(bar);

        Assert.Equal(Colors.Green, ((SolidColorBrush)bar.Background).Color);
    }

    [WpfTheory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Updating)]
    public void Leaves_5_pixels_between_its_start_element_and_the_update_text(UpdateStage stage)
    {
        var start = new Border { Height = 10 };

        var (bar, _) = Measured(start, stage);

        var (_, startRight) = Edges(bar, start);
        var (textLeft, _) = Edges(bar, UpdateTextOf(bar));
        Assert.Equal(5, textLeft - startRight, 0.01);
    }

    [WpfTheory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Updating)]
    public void Keeps_the_update_text_at_its_right_end_with_the_gap_on_its_left(UpdateStage stage)
    {
        var (bar, _) = Measured(new Border { Height = 10 }, stage);

        var (_, textRight) = Edges(bar, UpdateTextOf(bar));
        var contentRight = bar.ActualWidth - bar.Padding.Right - bar.BorderThickness.Right;
        Assert.Equal(contentRight, textRight, 0.01);
    }

    // The text block that shows "Update available: ..." or "Updating to ...", found by what it shows.
    private static TextBlock UpdateTextOf(DependencyObject bar) =>
        Assert.IsType<TextBlock>(FindUpdateText(bar));

    private static TextBlock? FindUpdateText(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock block && string.Concat(block.Inlines.OfType<Run>().Select(run => run.Text)).StartsWith("Updat", StringComparison.Ordinal))
            {
                return block;
            }

            if (FindUpdateText(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // The left and right edges of the element, in the bar's own coordinates.
    private static (double Left, double Right) Edges(UpdateStatusBar bar, UIElement element)
    {
        var bounds = element.TransformToAncestor(bar).TransformBounds(new Rect(element.RenderSize));
        return (bounds.Left, bounds.Right);
    }

    private static (UpdateStatusBar Bar, Grid Container) Measured(UIElement? start, UpdateStage stage)
    {
        var status = new FakeUpdateStatus();
        var container = new Grid();
        var bar = new UpdateStatusBar(status, start);
        container.Children.Add(bar);
        status.Publish(new UpdateState(stage, "2.1.0", stage is UpdateStage.NoUpdate ? null : "2.2.0", null));
        container.Measure(new Size(500, double.PositiveInfinity));
        container.Arrange(new Rect(0, 0, 500, container.DesiredSize.Height));
        return (bar, container);
    }
}
