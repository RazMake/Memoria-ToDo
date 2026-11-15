using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>The palette's rule is readability: WCAG 2.2's minimum contrast of 4.5:1 on each theme's backgrounds.</summary>
[Unit]
public class PaletteTests
{
    private const double MinimumContrast = 4.5;

    private static readonly string[] LightBackgrounds = ["#ffffff", "#f3f3f3"];
    private static readonly string[] DarkBackgrounds = ["#1f1f1f", "#2b2b2b"];

    [Fact]
    public void Gives_every_color_a_light_value_that_reads_on_the_light_backgrounds()
    {
        AssertReadable(c => c.Light, LightBackgrounds);
    }

    [Fact]
    public void Gives_every_color_a_dark_value_that_reads_on_the_dark_backgrounds()
    {
        AssertReadable(c => c.Dark, DarkBackgrounds);
    }

    private static void AssertReadable(Func<PaletteColor, string> value, string[] backgrounds)
    {
        Assert.NotEmpty(Palette.Colors);
        foreach (var color in Palette.Colors)
        {
            foreach (var background in backgrounds)
            {
                var contrast = Contrast(value(color), background);
                Assert.True(contrast >= MinimumContrast, $"{color.Name} {value(color)} on {background} has contrast {contrast:0.00}.");
            }
        }
    }

    private static double Contrast(string foreground, string background)
    {
        var (lighter, darker) = (Luminance(foreground), Luminance(background));
        if (lighter < darker)
        {
            (lighter, darker) = (darker, lighter);
        }

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int from)
        {
            var c = Convert.ToInt32(hex.Substring(from, 2), 16) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
