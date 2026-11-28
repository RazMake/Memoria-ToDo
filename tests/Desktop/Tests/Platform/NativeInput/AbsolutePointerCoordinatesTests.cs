using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class AbsolutePointerCoordinatesTests
{
    private static readonly Win32.Rect Desktop = new() { Left = -1920, Top = -100, Right = 1920, Bottom = 1180 };

    [Fact]
    public void The_first_pixel_of_the_desktop_is_zero_even_when_the_origin_is_negative()
    {
        Assert.Equal((0, 0), AbsolutePointerCoordinates.Normalize(new Win32.Point { X = -1920, Y = -100 }, Desktop));
    }

    [Fact]
    public void The_last_pixel_of_the_desktop_is_the_top_of_the_range()
    {
        Assert.Equal((65535, 65535), AbsolutePointerCoordinates.Normalize(new Win32.Point { X = 1919, Y = 1179 }, Desktop));
    }

    [Fact]
    public void A_pixel_between_is_placed_in_proportion_from_a_negative_origin()
    {
        var screen = new Win32.Rect { Left = -4, Top = -8, Right = 0, Bottom = -4 };

        Assert.Equal((21845, 43690), AbsolutePointerCoordinates.Normalize(new Win32.Point { X = -3, Y = -6 }, screen));
    }

    [Fact]
    public void An_axis_one_pixel_long_maps_its_only_pixel_to_zero()
    {
        var screen = new Win32.Rect { Left = 7, Top = -3, Right = 8, Bottom = 97 };

        Assert.Equal((0, 65535), AbsolutePointerCoordinates.Normalize(new Win32.Point { X = 7, Y = 96 }, screen));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    [InlineData(100, -5)]
    public void Rejects_a_desktop_without_a_positive_width_and_height(int width, int height)
    {
        var screen = new Win32.Rect { Left = 10, Top = 10, Right = 10 + width, Bottom = 10 + height };

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AbsolutePointerCoordinates.Normalize(new Win32.Point { X = 10, Y = 10 }, screen));

        Assert.Equal("virtualScreen", error.ParamName);
    }

    [Theory]
    [InlineData(-1921, 0)]
    [InlineData(1920, 0)]
    [InlineData(0, -101)]
    [InlineData(0, 1180)]
    public void Rejects_a_point_outside_the_desktop(int x, int y)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AbsolutePointerCoordinates.Normalize(new Win32.Point { X = x, Y = y }, Desktop));

        Assert.Equal("point", error.ParamName);
    }
}
