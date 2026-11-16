using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

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

    private static (UpdateStatusBar Bar, Grid Container) Measured(UIElement? start, UpdateStage stage)
    {
        var status = new FakeUpdateStatus();
        var container = new Grid();
        var bar = new UpdateStatusBar(status, start);
        container.Children.Add(bar);
        status.Publish(new UpdateState(stage, "2.1.0", stage is UpdateStage.NoUpdate ? null : "2.2.0", null));
        container.Measure(new Size(500, double.PositiveInfinity));
        return (bar, container);
    }
}
