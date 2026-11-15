using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansLinkAndHeadingTests
{
    [Theory]
    [InlineData("Read [the docs](https://example.com/docs)", "Read the docs")]
    [InlineData("[the docs](https://example.com/docs)", "the docs")]
    [InlineData("Read [the docs](https://example.com/docs) now", "Read the docs now")]
    public void Shows_a_title_link_in_any_position_as_its_text_with_its_address(string markdown, string rendered)
    {
        var shown = StyledSpans.Of(markdown, MarkdownField.Title).Where(s => !s.Styles.HasFlag(SpanStyle.Marker)).ToList();

        Assert.Equal(rendered, string.Concat(shown.Select(s => markdown.Substring(s.Start, s.Length))));
        var linked = shown.Where(s => s.LinkAddress is not null).ToList();
        Assert.Equal("the docs", string.Concat(linked.Select(s => markdown.Substring(s.Start, s.Length))));
        Assert.All(linked, s => Assert.Equal("https://example.com/docs", s.LinkAddress));
    }

    [Fact]
    public void Gives_a_links_text_its_address()
    {
        var spans = Analyzed.OfDescription("see [docs](https://x.com/a) now");

        Assert.All(spans.Over("docs"), s => Assert.Equal("https://x.com/a", s.LinkAddress));
    }

    [Fact]
    public void Leaves_text_beside_a_link_without_an_address()
    {
        var spans = Analyzed.OfDescription("see [docs](https://x.com/a) now");

        Assert.All(spans.Over("see"), s => Assert.Null(s.LinkAddress));
        Assert.All(spans.Over("now"), s => Assert.Null(s.LinkAddress));
    }

    [Fact]
    public void Marks_a_links_brackets_and_address_as_markers_and_its_text_as_not()
    {
        var spans = Analyzed.OfDescription("see [docs](https://x.com/a) now");

        Analyzed.AssertMarkers(spans.Over("["));
        Analyzed.AssertMarkers(spans.Over("](https://x.com/a)"));
        Analyzed.AssertNotMarkers(spans.Over("docs"));
    }

    [Fact]
    public void Gives_a_typed_web_address_its_own_address_through_autolinks()
    {
        var spans = Analyzed.OfDescription("go to https://example.com/page now");

        Assert.All(spans.Over("https://example.com/page"), s => Assert.Equal("https://example.com/page", s.LinkAddress));
        Assert.All(spans.Over("go to"), s => Assert.Null(s.LinkAddress));
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Gives_a_bracketed_autolinks_address_to_its_text_and_leaves_the_text_beside_it_without_one(MarkdownField field)
    {
        var spans = Analyzed.Of("go <https://example.com> now", field);

        Assert.All(spans.Over("https://example.com"), s => Assert.Equal("https://example.com", s.LinkAddress));
        Assert.All(spans.Over("go"), s => Assert.Null(s.LinkAddress));
        Assert.All(spans.Over("now"), s => Assert.Null(s.LinkAddress));
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Marks_a_bracketed_autolinks_angle_brackets_as_markers_and_its_text_as_not(MarkdownField field)
    {
        var spans = Analyzed.Of("go <https://example.com> now", field);

        Analyzed.AssertMarkers(spans.Over("<"));
        Analyzed.AssertMarkers(spans.Over(">"));
        Analyzed.AssertNotMarkers(spans.Over("https://example.com"));
    }

    [Fact]
    public void Keeps_a_links_own_styling_beside_its_address()
    {
        var spans = Analyzed.OfDescription("[**bold**](https://x.com)");

        Assert.All(spans.Over("bold"), s =>
        {
            Assert.Equal(SpanStyle.Bold, s.Styles);
            Assert.Equal("https://x.com", s.LinkAddress);
        });
    }

    [Theory]
    [InlineData("# One", 1, "One")]
    [InlineData("## Two", 2, "Two")]
    [InlineData("### Three", 3, "Three")]
    [InlineData("#### Four", 4, "Four")]
    [InlineData("##### Five", 5, "Five")]
    [InlineData("###### Six", 6, "Six")]
    public void Gives_a_headings_text_its_level_in_a_description(string markdown, int level, string text)
    {
        Assert.All(Analyzed.OfDescription(markdown).Over(text), s => Assert.Equal(level, s.HeadingLevel));
    }

    [Fact]
    public void Leaves_a_paragraph_after_a_heading_at_level_zero()
    {
        var spans = Analyzed.OfDescription("## Title\n\nBody text");

        Assert.All(spans.Over("Body text"), s => Assert.Equal(0, s.HeadingLevel));
    }

    [Fact]
    public void Marks_a_headings_hashes_as_a_marker()
    {
        Analyzed.AssertMarkers(Analyzed.OfDescription("## Title").Over("##"));
    }

    [Fact]
    public void Shows_a_hash_in_a_title_as_typed_with_no_heading_level()
    {
        var spans = Analyzed.OfTitle("# text");

        Assert.All(spans.All, s => Assert.Equal(0, s.HeadingLevel));
        Assert.All(spans.Over("text"), s => Assert.Equal(SpanStyle.None, s.Styles));
    }
}
