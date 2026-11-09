using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansLinkAndHeadingTests
{
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
    public void Marks_a_links_brackets_and_address_as_markers()
    {
        var spans = Analyzed.OfDescription("see [docs](https://x.com/a) now");

        Assert.All(spans.Over("["), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over("](https://x.com/a)"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Does_not_mark_a_links_text_as_a_marker()
    {
        Assert.All(Analyzed.OfDescription("see [docs](https://x.com/a) now").Over("docs"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Gives_a_typed_web_address_its_own_address_through_autolinks()
    {
        var spans = Analyzed.OfDescription("go to https://example.com/page now");

        Assert.All(spans.Over("https://example.com/page"), s => Assert.Equal("https://example.com/page", s.LinkAddress));
        Assert.All(spans.Over("go to"), s => Assert.Null(s.LinkAddress));
    }

    [Fact]
    public void Gives_a_bracketed_autolinks_address_to_its_text_in_a_description()
    {
        var spans = Analyzed.OfDescription("go <https://example.com> now");

        Assert.All(spans.Over("https://example.com"), s => Assert.Equal("https://example.com", s.LinkAddress));
        Assert.All(spans.Over("go"), s => Assert.Null(s.LinkAddress));
        Assert.All(spans.Over("now"), s => Assert.Null(s.LinkAddress));
    }

    [Fact]
    public void Marks_a_bracketed_autolinks_angle_brackets_as_markers_in_a_description()
    {
        var spans = Analyzed.OfDescription("go <https://example.com> now");

        Assert.All(spans.Over("<"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over(">"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over("https://example.com"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Gives_a_bracketed_autolinks_address_to_its_text_in_a_title()
    {
        var spans = Analyzed.OfTitle("<https://example.com>");

        Assert.All(spans.Over("https://example.com"), s => Assert.Equal("https://example.com", s.LinkAddress));
    }

    [Fact]
    public void Marks_a_bracketed_autolinks_angle_brackets_as_markers_in_a_title()
    {
        var spans = Analyzed.OfTitle("<https://example.com>");

        Assert.All(spans.Over("<"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over(">"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Gives_a_links_text_its_address_in_a_title()
    {
        Assert.All(Analyzed.OfTitle("see [docs](https://x.com)").Over("docs"), s => Assert.Equal("https://x.com", s.LinkAddress));
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
        Assert.All(Analyzed.OfDescription("## Title").Over("##"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Shows_a_hash_in_a_title_as_typed_with_no_heading_level()
    {
        var spans = Analyzed.OfTitle("# text");

        Assert.All(spans.All, s => Assert.Equal(0, s.HeadingLevel));
        Assert.All(spans.Over("text"), s => Assert.Equal(SpanStyle.None, s.Styles));
    }
}
