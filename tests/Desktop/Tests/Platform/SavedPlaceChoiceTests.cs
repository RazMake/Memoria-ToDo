using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Whether a saved place is on a screen now, and the size a window opens at. Screens are plain work-area rectangles
/// in pixels and the minimum size is in pixels. Null means the window opens as Show on the pointer's screen places it.
/// </summary>
[Unit]
public class SavedPlaceChoiceTests
{
    private static readonly Rect Main = new(0, 0, 1920, 1040);
    private static readonly Rect Right = new(1920, 0, 1920, 1040);
    private static readonly Size NoMinimum = new(1, 1);

    private static SavedPlace? Choose(SavedPlace saved, Size? minimum = null, params Rect[] screens) =>
        SavedPlaceChoice.Choose(saved, screens.Length == 0 ? [Main] : screens, minimum ?? NoMinimum);

    [Fact]
    public void A_place_inside_a_work_area_is_used_as_saved()
    {
        var saved = new SavedPlace(120, 80, 1100, 720, false);

        Assert.Equal(saved, Choose(saved));
    }

    [Fact]
    public void A_place_on_the_second_screen_is_used_as_saved()
    {
        var saved = new SavedPlace(2400, 100, 800, 600, false);

        Assert.Equal(saved, Choose(saved, null, Main, Right));
    }

    [Fact]
    public void A_place_that_lies_exactly_half_in_one_work_area_is_used()
    {
        var saved = new SavedPlace(1870, 100, 100, 100, false);

        Assert.Equal(saved, Choose(saved));
    }

    [Fact]
    public void A_place_that_lies_just_under_half_in_the_only_work_area_is_not_used()
    {
        var saved = new SavedPlace(1871, 100, 100, 100, false);

        Assert.Null(Choose(saved));
    }

    [Fact]
    public void A_place_split_evenly_between_two_neighbouring_work_areas_is_used()
    {
        var saved = new SavedPlace(1870, 100, 100, 100, false);

        Assert.Equal(saved, Choose(saved, null, Main, Right));
    }

    [Fact]
    public void A_place_that_lies_half_in_a_work_area_by_height_is_used()
    {
        var saved = new SavedPlace(100, 990, 200, 100, false);

        Assert.Equal(saved, Choose(saved));
    }

    [Fact]
    public void A_place_that_lies_more_than_half_along_each_side_but_less_than_half_by_area_is_not_used()
    {
        // 60 of 100 pixels in each direction: 36% of the window's area.
        var saved = new SavedPlace(1860, 980, 100, 100, false);

        Assert.Null(Choose(saved));
    }

    [Fact]
    public void A_place_that_is_less_than_half_on_the_screen_once_its_size_is_cut_to_the_work_area_is_not_used()
    {
        // Before the cut exactly half of the 2200-wide window is on screen; cut to 1200 at left -1100, only 100 pixels show.
        var saved = new SavedPlace(-1100, 0, 2200, 720, false);

        Assert.Null(Choose(saved, null, new Rect(0, 0, 1200, 800)));
    }

    [Fact]
    public void A_place_with_only_its_title_bar_in_a_work_area_is_not_used()
    {
        var saved = new SavedPlace(100, 1020, 800, 600, false);

        Assert.Null(Choose(saved));
    }

    [Fact]
    public void A_place_on_a_screen_that_is_gone_is_not_used()
    {
        var saved = new SavedPlace(2400, 100, 800, 600, false);

        Assert.Null(Choose(saved));
    }

    [Fact]
    public void A_place_is_not_used_when_there_are_no_screens()
    {
        Assert.Null(SavedPlaceChoice.Choose(new SavedPlace(120, 80, 1100, 720, false), [], NoMinimum));
    }

    [Fact]
    public void A_maximized_place_on_a_screen_keeps_its_restored_size_and_its_maximized_state()
    {
        var saved = new SavedPlace(2400, 100, 800, 600, true);

        Assert.Equal(saved, Choose(saved, null, Main, Right));
    }

    [Fact]
    public void A_maximized_place_off_every_screen_is_not_used()
    {
        Assert.Null(Choose(new SavedPlace(5000, 100, 800, 600, true)));
    }

    [Fact]
    public void A_width_below_the_windows_minimum_opens_at_the_minimum_width()
    {
        var chosen = Choose(new SavedPlace(120, 80, 200, 720, false), new Size(480, 100));

        Assert.Equal((480, 720), (chosen!.Value.Width, chosen.Value.Height));
    }

    [Fact]
    public void A_height_below_the_windows_minimum_opens_at_the_minimum_height()
    {
        var chosen = Choose(new SavedPlace(120, 80, 1100, 150, false), new Size(100, 320));

        Assert.Equal((1100, 320), (chosen!.Value.Width, chosen.Value.Height));
    }

    [Fact]
    public void A_size_larger_than_the_screens_work_area_opens_at_the_work_areas_size()
    {
        var chosen = Choose(new SavedPlace(0, 0, 2400, 1200, false));

        Assert.Equal((1920, 1040), (chosen!.Value.Width, chosen.Value.Height));
    }

    [Fact]
    public void A_size_larger_than_the_work_area_of_the_screen_it_is_on_is_cut_to_that_screen_not_another()
    {
        var small = new Rect(1920, 0, 1024, 728);

        var chosen = Choose(new SavedPlace(1920, 0, 1500, 900, false), null, Main, small);

        Assert.Equal((1024, 728), (chosen!.Value.Width, chosen.Value.Height));
    }

    [Fact]
    public void A_size_within_the_minimum_and_the_work_area_is_left_alone()
    {
        var chosen = Choose(new SavedPlace(120, 80, 1100, 720, false), new Size(480, 320));

        Assert.Equal((1100, 720), (chosen!.Value.Width, chosen.Value.Height));
    }

    [Fact]
    public void A_place_with_a_raised_or_cut_size_keeps_its_maximized_state()
    {
        var chosen = Choose(new SavedPlace(0, 0, 2400, 100, true), new Size(100, 200));

        Assert.True(chosen!.Value.Maximized);
    }
}
