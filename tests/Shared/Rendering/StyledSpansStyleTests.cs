using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansStyleTests
{
    [Fact]
    public void Styles_bold_text_as_Bold()
    {
        var spans = Analyzed.OfDescription("a **bold** b");

        Assert.All(spans.Over("bold"), s => Assert.Equal(SpanStyle.Bold, s.Styles));
    }

    [Theory]
    [InlineData("a *it* b", "it")]
    [InlineData("a _it_ b", "it")]
    public void Styles_italic_text_as_Italic(string markdown, string text)
    {
        Assert.All(Analyzed.OfDescription(markdown).Over(text), s => Assert.Equal(SpanStyle.Italic, s.Styles));
    }

    [Fact]
    public void Styles_struck_text_as_Strikethrough()
    {
        Assert.All(Analyzed.OfDescription("a ~~gone~~ b").Over("gone"), s => Assert.Equal(SpanStyle.Strikethrough, s.Styles));
    }

    [Fact]
    public void Styles_inline_code_as_Code()
    {
        Assert.All(Analyzed.OfDescription("a `code` b").Over("code"), s => Assert.Equal(SpanStyle.Code, s.Styles));
    }

    [Fact]
    public void Combines_nested_styles_so_triple_stars_are_Bold_and_Italic()
    {
        Assert.All(Analyzed.OfDescription("a ***x*** b").Over("x"), s => Assert.Equal(SpanStyle.Bold | SpanStyle.Italic, s.Styles));
    }

    [Fact]
    public void Combines_italic_inside_bold()
    {
        var spans = Analyzed.OfDescription("**a *b* c**");

        Assert.All(spans.Over("b"), s => Assert.Equal(SpanStyle.Bold | SpanStyle.Italic, s.Styles));
        Assert.All(spans.Over("a "), s => Assert.Equal(SpanStyle.Bold, s.Styles));
    }

    [Fact]
    public void Leaves_text_outside_the_styled_stretch_plain()
    {
        var spans = Analyzed.OfDescription("before **bold** after");

        Assert.All(spans.Over("before"), s => Assert.Equal(SpanStyle.None, s.Styles));
        Assert.All(spans.Over("after"), s => Assert.Equal(SpanStyle.None, s.Styles));
    }

    [Fact]
    public void Marks_the_stars_of_bold_text_as_markers()
    {
        var spans = Analyzed.OfDescription("a **bold** b");

        Assert.All(spans.Over("**"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over("**", startAt: 4), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Does_not_mark_the_styled_text_itself_as_a_marker()
    {
        Assert.All(Analyzed.OfDescription("a **bold** b").Over("bold"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Marks_the_backticks_of_inline_code_as_markers()
    {
        var spans = Analyzed.OfDescription("a `code` b");

        Assert.All(spans.Over("`"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over("`", startAt: 3), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Marks_the_tildes_of_struck_text_as_markers()
    {
        Assert.All(Analyzed.OfDescription("a ~~gone~~ b").Over("~~"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Marks_the_backslash_before_an_escaped_character_as_a_marker()
    {
        var spans = Analyzed.OfDescription("a \\*b");

        Assert.All(spans.Over("\\"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Shows_an_escaped_character_as_plain_text()
    {
        Assert.All(Analyzed.OfDescription("a \\*b").Over("b"), s => Assert.Equal(SpanStyle.None, s.Styles));
    }

    [Fact]
    public void Styles_the_text_of_a_title_as_inline_Markdown()
    {
        var spans = Analyzed.OfTitle("Buy **milk**");

        Assert.All(spans.Over("milk"), s => Assert.Equal(SpanStyle.Bold, s.Styles));
        Assert.All(spans.Over("**"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Theory]
    [InlineData("<b>x</b>")]
    [InlineData("a <b>x</b> c")]
    [InlineData("<script>alert(1)</script>")]
    public void Gives_raw_HTML_no_style_in_a_description(string markdown)
    {
        AssertAllPlain(Analyzed.OfDescription(markdown).All);
    }

    [Fact]
    public void Gives_raw_HTML_no_style_in_a_title()
    {
        AssertAllPlain(Analyzed.OfTitle("a <b>x</b> c").All);
    }

    private static void AssertAllPlain(IReadOnlyList<StyledSpan> spans)
    {
        Assert.All(spans, s =>
        {
            Assert.Equal(SpanStyle.None, s.Styles);
            Assert.Null(s.LinkAddress);
            Assert.Null(s.Color);
            Assert.Equal(0, s.HeadingLevel);
        });
    }
}
