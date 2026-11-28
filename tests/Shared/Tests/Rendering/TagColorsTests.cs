using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>The 30 tag colors: their values, the three measured rules, lookup by name, and the default from a tag's text.</summary>
[Unit]
public class TagColorsTests
{
    private const double MinimumDifference = 10;
    private const double MinimumBackgroundDifference = 7;
    private const double MinimumContrast = 4.5;

    private static readonly string[] LightBackgrounds = ["#ffffff", "#f3f3f3"];
    private static readonly string[] DarkBackgrounds = ["#1f1f1f", "#2b2b2b"];

    private static readonly (string Name, string Light, string Dark)[] Expected =
    [
        ("red-1", "#fca5a5", "#f87171"), ("red-2", "#ef4444", "#dc2626"), ("red-3", "#b91c1c", "#7f1d1d"),
        ("orange-1", "#fdba74", "#fb923c"), ("orange-2", "#f97316", "#ea580c"), ("orange-3", "#c2410c", "#9a3412"),
        ("yellow-1", "#fde047", "#facc15"), ("yellow-2", "#eab308", "#ca8a04"), ("yellow-3", "#a16207", "#854d0e"),
        ("lime-1", "#bef264", "#a3e635"), ("lime-2", "#84cc16", "#65a30d"), ("lime-3", "#4d7c0f", "#3f6212"),
        ("green-1", "#86efac", "#4ade80"), ("green-2", "#22c55e", "#16a34a"), ("green-3", "#15803d", "#166534"),
        ("teal-1", "#5eead4", "#2dd4bf"), ("teal-2", "#14b8a6", "#0d9488"), ("teal-3", "#0f766e", "#115e59"),
        ("blue-1", "#93c5fd", "#60a5fa"), ("blue-2", "#3b82f6", "#2563eb"), ("blue-3", "#1d4ed8", "#1e40af"),
        ("purple-1", "#c4b5fd", "#a78bfa"), ("purple-2", "#8b5cf6", "#7c3aed"), ("purple-3", "#6d28d9", "#5b21b6"),
        ("pink-1", "#f9a8d4", "#f472b6"), ("pink-2", "#ec4899", "#db2777"), ("pink-3", "#be185d", "#9d174d"),
        ("gray-1", "#d6d3d1", "#a8a29e"), ("gray-2", "#78716c", "#78716c"), ("gray-3", "#44403c", "#44403c"),
    ];

    [Fact]
    public void Holds_the_thirty_colors_in_table_order_with_their_light_and_dark_values()
    {
        var actual = TagColors.All.Select(c => (c.Name, c.Light.Background, c.Dark.Background)).ToArray();

        Assert.Equal(Expected, actual);
    }

    [Fact]
    public void Keeps_any_two_light_colors_at_least_ten_apart()
    {
        AssertDistinct(c => c.Light.Background);
    }

    [Fact]
    public void Keeps_any_two_dark_colors_at_least_ten_apart()
    {
        AssertDistinct(c => c.Dark.Background);
    }

    [Fact]
    public void Gives_every_light_fill_the_black_or_white_text_that_reads_best_at_4_5_to_1_or_more()
    {
        AssertText(c => c.Light);
    }

    [Fact]
    public void Gives_every_dark_fill_the_black_or_white_text_that_reads_best_at_4_5_to_1_or_more()
    {
        AssertText(c => c.Dark);
    }

    [Fact]
    public void Keeps_every_light_color_at_least_seven_from_the_light_window_backgrounds()
    {
        AssertApartFromBackgrounds(c => c.Light.Background, LightBackgrounds);
    }

    [Fact]
    public void Keeps_every_dark_color_at_least_seven_from_the_dark_window_backgrounds()
    {
        AssertApartFromBackgrounds(c => c.Dark.Background, DarkBackgrounds);
    }

    [Fact]
    public void Finds_a_color_by_its_exact_name()
    {
        var color = TagColors.Find("red-1");

        Assert.NotNull(color);
        Assert.Equal("red-1", color.Name);
        Assert.Equal("#fca5a5", color.Light.Background);
    }

    [Theory]
    [InlineData("Red-1")]
    [InlineData("red-4")]
    [InlineData("")]
    public void Finds_no_color_for_a_name_that_differs_in_case_is_unknown_or_is_empty(string name)
    {
        Assert.Null(TagColors.Find(name));
    }

    [Theory]
    [InlineData("Home", "gray-2")]
    [InlineData("home", "gray-2")]
    [InlineData("Work", "yellow-1")]
    [InlineData("Urgent", "yellow-1")]
    [InlineData("Shopping", "orange-1")]
    public void Gives_these_fixed_tags_the_same_default_color_in_every_release(string tag, string name)
    {
        Assert.Equal(name, TagColors.DefaultFor(tag).Name);
    }

    [Fact]
    public void Gives_a_tag_the_same_default_whatever_its_case()
    {
        Assert.Equal(TagColors.DefaultFor("Shopping"), TagColors.DefaultFor("sHOPPING"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("some longer tag")]
    public void Gives_every_tag_a_default_from_the_thirty_colors(string tag)
    {
        Assert.Contains(TagColors.DefaultFor(tag), TagColors.All);
    }

    [Fact]
    public void Rejects_a_null_tag_when_asked_for_its_default()
    {
        Assert.Throws<ArgumentNullException>(() => TagColors.DefaultFor(null!));
    }

    private static void AssertDistinct(Func<TagColor, string> value)
    {
        var colors = TagColors.All;
        Assert.Equal(30, colors.Count);
        for (var i = 0; i < colors.Count; i++)
        {
            for (var j = i + 1; j < colors.Count; j++)
            {
                var difference = ColorMath.DeltaE2000(value(colors[i]), value(colors[j]));
                Assert.True(difference >= MinimumDifference,
                    $"{colors[i].Name} and {colors[j].Name} are only {difference:0.0} apart.");
            }
        }
    }

    private static void AssertText(Func<TagColor, TagFill> fill)
    {
        Assert.NotEmpty(TagColors.All);
        foreach (var color in TagColors.All)
        {
            var f = fill(color);
            var black = ColorMath.Contrast("#000000", f.Background);
            var white = ColorMath.Contrast("#ffffff", f.Background);
            var best = black > white ? "#000000" : "#ffffff";
            Assert.True(Math.Max(black, white) >= MinimumContrast, $"{color.Name} reads at only {Math.Max(black, white):0.00}:1.");
            Assert.Equal(best, f.Text);
        }
    }

    private static void AssertApartFromBackgrounds(Func<TagColor, string> value, string[] backgrounds)
    {
        Assert.NotEmpty(TagColors.All);
        foreach (var color in TagColors.All)
        {
            foreach (var background in backgrounds)
            {
                var difference = ColorMath.DeltaE2000(value(color), background);
                Assert.True(difference >= MinimumBackgroundDifference,
                    $"{color.Name} is only {difference:0.0} from {background}.");
            }
        }
    }
}
