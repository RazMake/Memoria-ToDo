using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>Writing an image's width into a description's Markdown, by the number the description page gives the image.</summary>
[Unit]
public class ImageWidthsTests
{
    [Theory]
    [InlineData("![a](p.png)", 0, 300, "![a|300](p.png)")]
    [InlineData("![](p.png)", 0, 40, "![|40](p.png)")]
    [InlineData("![a|b](p.png)", 0, 5, "![a|b|5](p.png)")]
    [InlineData("![a|wide](p.png)", 0, 5, "![a|wide|5](p.png)")]
    [InlineData("![a|0](p.png)", 0, 5, "![a|0|5](p.png)")]
    public void Adds_a_width_to_an_image_with_none(string markdown, int image, int width, string expected)
    {
        Assert.Equal(expected, ImageWidths.With(markdown, image, width));
    }

    [Theory]
    [InlineData("![a|100](p.png)", 300, "![a|300](p.png)")]
    [InlineData("![a|b|100](p.png)", 1, "![a|b|1](p.png)")]
    [InlineData("![|100](p.png)", 10000, "![|10000](p.png)")]
    public void Replaces_the_width_an_image_already_has(string markdown, int width, string expected)
    {
        Assert.Equal(expected, ImageWidths.With(markdown, 0, width));
    }

    [Theory]
    [InlineData(0, "![a|9](x.png) and ![b](y.png) and ![c](z.png)")]
    [InlineData(1, "![a](x.png) and ![b|9](y.png) and ![c](z.png)")]
    [InlineData(2, "![a](x.png) and ![b](y.png) and ![c|9](z.png)")]
    public void Changes_only_the_image_with_the_given_number(int image, string expected)
    {
        Assert.Equal(expected, ImageWidths.With("![a](x.png) and ![b](y.png) and ![c](z.png)", image, 9));
    }

    [Fact]
    public void Counts_an_image_from_outside_the_clone_as_the_description_page_does()
    {
        var markdown = "![a](x.png)\n\n![b](https://evil.example/b.png)\n\n![c](z.png)";

        Assert.Equal("![a](x.png)\n\n![b](https://evil.example/b.png)\n\n![c|9](z.png)", ImageWidths.With(markdown, 2, 9));
    }

    [Theory]
    [InlineData("![photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![photo][]\n\n[photo]: images/t/p.png")]
    [InlineData("![photo][photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![a|100][photo]\n\n[photo]: images/t/p.png")]
    public void Keeps_a_reference_image_pointing_at_its_definition_when_it_sets_a_width(string markdown)
    {
        var result = ImageWidths.With(markdown, 0, 300);

        var page = DescriptionPage.Of(result, false, new Uri("https://example.com/clone/"));
        Assert.Matches("<img [^>]*src=\"https://example.com/clone/images/t/p.png\"", page);
        Assert.Matches("<[^>]*data-image=\"0\"[^>]*width:\\s*300px|<img [^>]*width=\"300\"", page);
        Assert.DoesNotContain("![", page);
        Assert.DoesNotContain("[photo]", page);
    }

    [Theory]
    [InlineData("![photo]\n\n[photo]: images/t/p.png", "![photo|300][photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![photo][]\n\n[photo]: images/t/p.png", "![photo|300][photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![photo][photo]\n\n[photo]: images/t/p.png", "![photo|300][photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![a|100][photo]\n\n[photo]: images/t/p.png", "![a|300][photo]\n\n[photo]: images/t/p.png")]
    [InlineData("![a][photo]\n\n[photo]: images/t/p.png", "![a|300][photo]\n\n[photo]: images/t/p.png")]
    public void Sets_the_width_of_a_reference_image_keeping_one_reference_to_its_definition(string markdown, string expected)
    {
        Assert.Equal(expected, ImageWidths.With(markdown, 0, 300));
    }

    [Fact]
    public void Does_not_count_a_link()
    {
        Assert.Equal("[l](u) ![a|9](x.png)", ImageWidths.With("[l](u) ![a](x.png)", 0, 9));
    }

    [Fact]
    public void Does_not_count_image_text_inside_code()
    {
        Assert.Equal("`![a](x.png)` ![b|9](y.png)", ImageWidths.With("`![a](x.png)` ![b](y.png)", 0, 9));
    }

    [Fact]
    public void Leaves_the_rest_of_the_markdown_as_it_was()
    {
        var markdown = "# Title\r\n\r\n- **bold** ![a](x.png)  \r\n\r\n:red[urgent] ![b|20](y.png)\r\n";

        Assert.Equal(
            "# Title\r\n\r\n- **bold** ![a](x.png)  \r\n\r\n:red[urgent] ![b|77](y.png)\r\n",
            ImageWidths.With(markdown, 1, 77));
    }

    [Theory]
    [InlineData("no image here", 0)]
    [InlineData("![a](x.png)", 1)]
    [InlineData("![a](x.png)", -1)]
    [InlineData("", 0)]
    public void Rejects_an_image_number_the_markdown_has_no_image_for(string markdown, int image)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageWidths.With(markdown, image, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Rejects_a_width_outside_1_to_10000(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageWidths.With("![a](x.png)", 0, width));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10000)]
    public void Accepts_the_smallest_and_largest_width(int width)
    {
        Assert.Equal($"![a|{width}](x.png)", ImageWidths.With("![a](x.png)", 0, width));
    }
}
