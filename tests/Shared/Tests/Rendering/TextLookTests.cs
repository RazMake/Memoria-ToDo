using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Shared.Rendering.Tests.Analyzed;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>The text look of each theme: its colors, their readability, heading sizes, and the color a styled span shows in.</summary>
[Unit]
public class TextLookTests
{
    private const double MinimumContrast = 4.5;

    private static readonly string[] LightBackgrounds = ["#ffffff", "#f3f3f3"];
    private static readonly string[] DarkBackgrounds = ["#1f1f1f", "#2b2b2b"];

    private static StyledSpan Span(
        SpanStyle styles = SpanStyle.None, string? link = null, string? color = null, int heading = 0) =>
        new(0, 1, styles, link, color, heading);

    private static string[] BackgroundsOf(bool dark) => dark ? DarkBackgrounds : LightBackgrounds;

    // The values

    [Fact]
    public void Gives_the_light_theme_its_heading_link_code_and_italic_colors()
    {
        var look = TextLooks.Light;

        Assert.Equal(
            ["#0f548c", "#0e7a75", "#6b3fa0", "#0969da", "#9a3c00", "#4f5b93"],
            [look.Heading1, look.Heading2, look.Heading3, look.Link, look.Code, look.Italic]);
    }

    [Fact]
    public void Gives_the_dark_theme_its_heading_link_code_and_italic_colors()
    {
        var look = TextLooks.Dark;

        Assert.Equal(
            ["#6cb8f6", "#4fd1c5", "#c4a7f5", "#4493f8", "#f0a868", "#b4bdf5"],
            [look.Heading1, look.Heading2, look.Heading3, look.Link, look.Code, look.Italic]);
    }

    [Fact]
    public void Fades_the_light_themes_code_background_to_a_gray_at_15_percent()
    {
        Assert.Equal("#808080", TextLooks.Light.CodeBackground);
        Assert.Equal(0.15, TextLooks.Light.CodeBackgroundOpacity);
    }

    [Fact]
    public void Fades_the_dark_themes_code_background_to_a_gray_at_25_percent()
    {
        Assert.Equal("#808080", TextLooks.Dark.CodeBackground);
        Assert.Equal(0.25, TextLooks.Dark.CodeBackgroundOpacity);
    }

    [Fact]
    public void Picks_the_light_or_dark_look_by_the_theme()
    {
        Assert.Same(TextLooks.Light, TextLooks.Of(dark: false));
        Assert.Same(TextLooks.Dark, TextLooks.Of(dark: true));
    }

    [Fact]
    public void Shows_code_in_Consolas()
    {
        Assert.Contains("Consolas", TextLooks.CodeFont);
    }

    // Readability

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keeps_every_color_of_a_theme_at_4_5_to_1_or_more_on_its_backgrounds(bool dark)
    {
        var look = TextLooks.Of(dark);
        var colors = new Dictionary<string, string>
        {
            ["Heading1"] = look.Heading1, ["Heading2"] = look.Heading2, ["Heading3"] = look.Heading3,
            ["Link"] = look.Link, ["Code"] = look.Code, ["Italic"] = look.Italic,
        };

        foreach (var (name, color) in colors)
        {
            foreach (var background in BackgroundsOf(dark))
            {
                var contrast = ColorMath.Contrast(color, background);
                Assert.True(contrast >= MinimumContrast, $"{name} {color} on {background} has contrast {contrast:0.00}.");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keeps_code_text_at_4_5_to_1_or_more_on_its_faded_background_over_each_background(bool dark)
    {
        var look = TextLooks.Of(dark);

        foreach (var background in BackgroundsOf(dark))
        {
            var faded = ColorMath.Blend(look.CodeBackground, look.CodeBackgroundOpacity, background);
            var contrast = ColorMath.Contrast(look.Code, faded);
            Assert.True(contrast >= MinimumContrast, $"Code {look.Code} on {faded} over {background} has contrast {contrast:0.00}.");
        }
    }

    // Headings

    [Theory]
    [InlineData(false, 1, "#0f548c")]
    [InlineData(false, 2, "#0e7a75")]
    [InlineData(false, 3, "#6b3fa0")]
    [InlineData(true, 1, "#6cb8f6")]
    [InlineData(true, 2, "#4fd1c5")]
    [InlineData(true, 3, "#c4a7f5")]
    public void Colors_headings_1_to_3(bool dark, int level, string expected)
    {
        Assert.Equal(expected, TextLooks.Of(dark).HeadingColor(level));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Leaves_headings_4_to_6_in_the_text_color(int level)
    {
        Assert.Null(TextLooks.Light.HeadingColor(level));
        Assert.Null(TextLooks.Dark.HeadingColor(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    public void Rejects_a_heading_level_outside_1_to_6_when_asked_for_its_color(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextLooks.Light.HeadingColor(level));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1.6)]
    [InlineData(2, 1.4)]
    [InlineData(3, 1.25)]
    [InlineData(4, 1.15)]
    [InlineData(5, 1.1)]
    [InlineData(6, 1.05)]
    public void Sizes_a_heading_as_a_multiple_of_the_text_size_and_text_at_one(int level, double expected)
    {
        Assert.Equal(expected, TextLooks.HeadingScale(level));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Rejects_a_heading_level_outside_0_to_6_when_asked_for_its_size(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextLooks.HeadingScale(level));
    }

    // The color a span shows in

    [Fact]
    public void Shows_plain_and_bold_text_in_the_text_color()
    {
        Assert.Null(TextLooks.Light.ColorOf(Span()));
        Assert.Null(TextLooks.Dark.ColorOf(Span(SpanStyle.Bold | SpanStyle.Strikethrough)));
    }

    [Fact]
    public void Shows_a_palette_color_in_its_value_for_the_theme()
    {
        Assert.NotEmpty(Palette.Colors);
        foreach (var color in Palette.Colors)
        {
            Assert.Equal(color.Light, TextLooks.Light.ColorOf(Span(color: color.Name)));
            Assert.Equal(color.Dark, TextLooks.Dark.ColorOf(Span(color: color.Name)));
        }
    }

    [Fact]
    public void Shows_a_link_in_the_link_color()
    {
        Assert.Equal(TextLooks.Light.Link, TextLooks.Light.ColorOf(Span(link: "https://example.org")));
        Assert.Equal(TextLooks.Dark.Link, TextLooks.Dark.ColorOf(Span(link: "https://example.org")));
    }

    [Fact]
    public void Shows_code_in_the_code_color()
    {
        Assert.Equal(TextLooks.Light.Code, TextLooks.Light.ColorOf(Span(SpanStyle.Code)));
        Assert.Equal(TextLooks.Dark.Code, TextLooks.Dark.ColorOf(Span(SpanStyle.Code)));
    }

    [Fact]
    public void Shows_italic_text_in_the_italic_color()
    {
        Assert.Equal(TextLooks.Light.Italic, TextLooks.Light.ColorOf(Span(SpanStyle.Italic)));
        Assert.Equal(TextLooks.Dark.Italic, TextLooks.Dark.ColorOf(Span(SpanStyle.Italic)));
    }

    [Fact]
    public void Shows_heading_text_in_its_heading_color_only_for_levels_1_to_3()
    {
        var look = TextLooks.Light;

        Assert.Equal(
            [look.Heading1, look.Heading2, look.Heading3, null, null, null],
            Enumerable.Range(1, 6).Select(level => look.ColorOf(Span(heading: level))));
    }

    [Fact]
    public void Prefers_a_palette_color_over_a_link_code_italic_and_heading()
    {
        var red = Palette.Colors.Single(c => c.Name == "red");

        var span = Span(SpanStyle.Code | SpanStyle.Italic, link: "https://example.org", color: "red", heading: 1);

        Assert.Equal(red.Light, TextLooks.Light.ColorOf(span));
        Assert.Equal(red.Dark, TextLooks.Dark.ColorOf(span));
    }

    [Fact]
    public void Prefers_a_link_over_code_italic_and_heading()
    {
        var span = Span(SpanStyle.Code | SpanStyle.Italic, link: "https://example.org", heading: 1);

        Assert.Equal(TextLooks.Light.Link, TextLooks.Light.ColorOf(span));
    }

    [Fact]
    public void Prefers_code_over_italic_and_heading()
    {
        var span = Span(SpanStyle.Code | SpanStyle.Italic, heading: 1);

        Assert.Equal(TextLooks.Light.Code, TextLooks.Light.ColorOf(span));
    }

    [Fact]
    public void Prefers_italic_over_a_heading_color()
    {
        var span = Span(SpanStyle.Italic, heading: 1);

        Assert.Equal(TextLooks.Light.Italic, TextLooks.Light.ColorOf(span));
    }

    [Fact]
    public void Shows_italic_text_inside_a_heading_4_in_the_italic_color()
    {
        Assert.Equal(TextLooks.Dark.Italic, TextLooks.Dark.ColorOf(Span(SpanStyle.Italic, heading: 4)));
    }

    // From Markdown to color

    [Fact]
    public void Shows_italic_text_inside_a_red_color_in_red()
    {
        var spans = OfDescription("a :red[_x_] b").Over("x");
        var red = Palette.Colors.Single(c => c.Name == "red");

        Assert.All(spans, s => Assert.Equal(red.Light, TextLooks.Light.ColorOf(s)));
    }

    [Fact]
    public void Shows_a_link_inside_a_heading_in_the_link_color()
    {
        var spans = OfDescription("# see [site](https://example.org)").Over("site");

        Assert.All(spans, s => Assert.Equal(TextLooks.Dark.Link, TextLooks.Dark.ColorOf(s)));
    }

    [Fact]
    public void Shows_the_plain_text_of_a_heading_1_in_the_heading_color()
    {
        var spans = OfDescription("# Title").Over("Title");

        Assert.All(spans, s => Assert.Equal(TextLooks.Light.Heading1, TextLooks.Light.ColorOf(s)));
    }
}
