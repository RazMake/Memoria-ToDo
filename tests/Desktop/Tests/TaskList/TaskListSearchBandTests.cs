using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The search band at Task View's top: it runs from edge to edge in the status bar's color, the search box takes its free
/// width, it is one row tall, and the message line and Undo bar stay under it. Unit tests cover the band's measurements
/// in detail.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListSearchBandTests
{
    private const double MostExtraBandHeight = 12;

    private static TaskListScreen Open(ListServices? services = null)
    {
        var screen = new TaskListScreen(services ?? new ListServices());
        screen.UseTheme();
        return screen;
    }

    [WpfFact]
    public void The_search_band_runs_from_the_left_edge_to_the_right_edge_of_the_window_at_its_top()
    {
        using var screen = Open();

        var band = screen.SearchBand;

        Assert.InRange(screen.LeftOf(band), -0.5, 0.5);
        Assert.InRange(screen.RightOf(band), screen.ContentRight - 0.5, screen.ContentRight + 0.5);
        Assert.InRange(screen.TopOf(band), -0.5, 0.5);
    }

    [WpfFact]
    public void The_search_band_has_the_status_bars_color_which_is_not_the_windows()
    {
        using var screen = Open();

        Assert.Equal(screen.StatusBarColor, screen.SearchBandColor);
        Assert.NotEqual(screen.WindowColor, screen.SearchBandColor);
    }

    [WpfFact]
    public void The_search_band_follows_the_theme()
    {
        using var screen = Open();
        var light = screen.SearchBandColor;

        screen.UseTheme(dark: true);

        Assert.NotEqual(light, screen.SearchBandColor);
        Assert.Equal(screen.StatusBarColor, screen.SearchBandColor);
    }

    [WpfFact]
    public void The_search_band_is_one_row_of_controls_tall_with_small_padding()
    {
        using var screen = Open();

        var band = screen.SearchBand;

        Assert.InRange(band.ActualHeight - screen.SearchBox.ActualHeight, 0, MostExtraBandHeight);
    }

    [WpfFact]
    public void The_search_box_grows_with_the_window()
    {
        using var screen = Open();
        screen.ResizeTo(800);
        var narrower = screen.SearchBox.ActualWidth;

        screen.ResizeTo(1300);

        Assert.True(screen.SearchBox.ActualWidth - narrower >= 300, $"The box went from {narrower} to {screen.SearchBox.ActualWidth}.");
    }

    [WpfFact]
    public void The_Undo_bar_shows_under_the_search_band()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Open(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Escape);

        var band = screen.SearchBand;

        Assert.True(screen.HasButton("Undo"));
        Assert.True(screen.TopOf(screen.UndoButton) >= screen.BottomOf(band) - 0.5, "The Undo bar isn't under the band.");
        Assert.False(screen.IsInside(screen.UndoButton, band));
    }
}
