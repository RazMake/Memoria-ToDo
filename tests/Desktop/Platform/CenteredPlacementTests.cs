using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Where a window of a given size in WPF units goes to be centered on a screen's work area, in device pixels.
/// The work area and the top-left are in pixels, in the virtual screen's coordinates; the size is in WPF units
/// and the scale (1.5 = 150%) turns it into pixels. Expected values are worked out by hand.
/// </summary>
[Unit]
public class CenteredPlacementTests
{
    [Fact]
    public void A_window_on_a_second_screen_at_150_percent_is_centered_in_that_screens_pixels()
    {
        // Screen A is 1920 px wide at 100%; screen B starts at x=1920 and is 3840x2160 px at 150%, taskbar 60 px.
        var workOfB = new Rect(1920, 0, 3840, 2100);

        var topLeft = CenteredPlacement.TopLeft(workOfB, 1.5, new Size(300, 200));

        // The window is 450x300 px: 1920 + (3840 - 450) / 2 = 3615 and (2100 - 300) / 2 = 900.
        Assert.Equal((3615, 900), topLeft);
    }

    [Fact]
    public void A_window_on_a_100_percent_screen_is_centered()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(0, 0, 1920, 1040), 1.0, new Size(300, 200));

        Assert.Equal((810, 420), topLeft);
    }

    [Fact]
    public void A_window_on_a_150_percent_screen_is_centered_by_its_size_in_pixels()
    {
        // 400x200 units are 600x300 px.
        var topLeft = CenteredPlacement.TopLeft(new Rect(0, 0, 2880, 1560), 1.5, new Size(400, 200));

        Assert.Equal((1140, 630), topLeft);
    }

    [Fact]
    public void A_window_on_a_200_percent_screen_is_centered_by_its_size_in_pixels()
    {
        // 300x200 units are 600x400 px.
        var topLeft = CenteredPlacement.TopLeft(new Rect(0, 0, 3840, 2080), 2.0, new Size(300, 200));

        Assert.Equal((1620, 840), topLeft);
    }

    [Fact]
    public void A_window_bigger_than_the_work_area_starts_at_its_top_left_corner()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(100, 50, 800, 600), 1.0, new Size(1000, 700));

        Assert.Equal((100, 50), topLeft);
    }

    [Fact]
    public void A_window_that_is_bigger_only_after_scaling_starts_at_its_top_left_corner()
    {
        // 600x500 units fit an 800x600 px work area at 100% but are 900x750 px at 150%.
        var topLeft = CenteredPlacement.TopLeft(new Rect(100, 50, 800, 600), 1.5, new Size(600, 500));

        Assert.Equal((100, 50), topLeft);
    }

    [Fact]
    public void A_window_wider_but_not_taller_than_the_work_area_is_clamped_only_horizontally()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(100, 50, 800, 600), 1.0, new Size(1000, 200));

        Assert.Equal((100, 250), topLeft);
    }

    [Fact]
    public void A_window_taller_but_not_wider_than_the_work_area_is_clamped_only_vertically()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(100, 50, 800, 600), 1.0, new Size(200, 700));

        Assert.Equal((400, 50), topLeft);
    }

    [Fact]
    public void A_window_on_a_screen_left_of_the_primary_one_gets_a_negative_left()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(-1920, -40, 1920, 1040), 1.0, new Size(300, 200));

        Assert.Equal((-1110, 380), topLeft);
    }

    [Fact]
    public void A_window_on_a_screen_above_the_primary_one_gets_a_negative_top()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(0, -1080, 1920, 1040), 1.0, new Size(300, 200));

        Assert.Equal((810, -660), topLeft);
    }

    [Fact]
    public void A_window_on_a_scaled_screen_with_negative_coordinates_is_centered()
    {
        // 400x200 units are 600x300 px.
        var topLeft = CenteredPlacement.TopLeft(new Rect(-2880, -20, 2880, 1560), 1.5, new Size(400, 200));

        Assert.Equal((-1740, 610), topLeft);
    }

    [Fact]
    public void A_bigger_window_on_a_screen_with_negative_coordinates_starts_at_its_top_left_corner()
    {
        var topLeft = CenteredPlacement.TopLeft(new Rect(-1920, -1080, 800, 600), 1.0, new Size(1000, 700));

        Assert.Equal((-1920, -1080), topLeft);
    }
}
