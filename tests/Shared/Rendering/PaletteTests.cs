using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class PaletteTests
{
    [Fact]
    public void Holds_red_orange_green_blue_purple_and_gray_in_that_order()
    {
        Assert.Equal(["red", "orange", "green", "blue", "purple", "gray"], Palette.Colors.Select(c => c.Name));
    }

    [Theory]
    [InlineData("red", "#d1242f", "#ff7b72")]
    [InlineData("orange", "#bc4c00", "#f0883e")]
    [InlineData("green", "#1a7f37", "#3fb950")]
    [InlineData("blue", "#0969da", "#4493f8")]
    [InlineData("purple", "#8250df", "#ab7df8")]
    [InlineData("gray", "#59636e", "#9198a1")]
    public void Gives_each_color_its_light_and_dark_values(string name, string light, string dark)
    {
        var color = Palette.Colors.Single(c => c.Name == name);

        Assert.Equal(light, color.Light);
        Assert.Equal(dark, color.Dark);
    }

    [Fact]
    public void Gives_the_same_colors_every_time()
    {
        Assert.Equal(Palette.Colors, Palette.Colors);
    }
}
