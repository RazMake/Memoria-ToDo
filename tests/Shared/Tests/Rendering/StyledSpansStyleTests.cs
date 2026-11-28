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

    [Theory]
    [InlineData("a **bold** b", "bold", "**")]
    [InlineData("a `code` b", "code", "`")]
    [InlineData("a ~~gone~~ b", "gone", "~~")]
    public void Marks_the_characters_around_styled_text_as_markers_and_the_text_itself_as_not(string markdown, string text, string marker)
    {
        var spans = Analyzed.OfDescription(markdown);
        var firstMarker = markdown.IndexOf(marker, StringComparison.Ordinal);

        Analyzed.AssertMarkers(spans.Over(marker));
        Analyzed.AssertMarkers(spans.Over(marker, startAt: firstMarker + marker.Length + text.Length));
        Analyzed.AssertNotMarkers(spans.Over(text));
    }

    [Fact]
    public void Marks_the_backslash_before_an_escaped_character_as_a_marker()
    {
        var spans = Analyzed.OfDescription("a \\*b");

        Analyzed.AssertMarkers(spans.Over("\\"));
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
        Analyzed.AssertMarkers(spans.Over("**"));
    }

    [Theory]
    [InlineData(MarkdownField.Description, "<b>x</b>")]
    [InlineData(MarkdownField.Description, "a <b>x</b> c")]
    [InlineData(MarkdownField.Description, "<script>alert(1)</script>")]
    [InlineData(MarkdownField.Title, "a <b>x</b> c")]
    public void Gives_raw_HTML_no_style(MarkdownField field, string markdown)
    {
        Analyzed.AssertAllPlain(Analyzed.Of(markdown, field).All);
    }
}
