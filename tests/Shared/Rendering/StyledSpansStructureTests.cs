using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansStructureTests
{
    public static TheoryData<string> Texts() =>
    [
        "plain text",
        "a **bold *and italic*** and `code` and ~~gone~~",
        "***x*** and [a *link*](https://x.com) and https://example.com/path.",
        ":red[see [docs](https://x)] and :blue[**bold**]{.big} and (:green[x])",
        "# Heading\n\nA paragraph.\n\n- item one\n- item **two**\n\n> [!NOTE]\n> text\n\n```\n:red[code]\n```",
        "line one\r\nline two\r\n\r\n## Two",
        "<b>html</b> and <script>x</script>",
        "😀 **emoji** 😀 :red[é]",
        "| a | b |\n| - | - |\n| 1 | 2 |",
        "\\*escaped\\* and \\\\ and \\[x\\]",
    ];

    public static TheoryData<string> Odd() =>
    [
        "", " ", "\n", "**", "*", "`", "~~", "[", "](", "[]()", ":red[", ":red[]", "]", "\\", "<", "#", "# ", ":", "::red[x]", "[[[[", "**a", "_a",
    ];

    [Theory]
    [MemberData(nameof(Texts))]
    public void Gives_spans_in_text_order_that_never_overlap_in_a_description(string markdown)
    {
        var spans = Analyzed.OfDescription(markdown).All;

        AssertInOrderWithoutOverlap(spans);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Gives_spans_in_text_order_that_never_overlap_in_a_title(string markdown)
    {
        var spans = Analyzed.OfTitle(markdown).All;

        AssertInOrderWithoutOverlap(spans);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Covers_the_whole_description_with_no_gaps(string markdown)
    {
        AssertCovers(markdown, Analyzed.OfDescription(markdown).All);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Covers_the_whole_title_with_no_gaps(string markdown)
    {
        AssertCovers(markdown, Analyzed.OfTitle(markdown).All);
    }

    [Fact]
    public void Gives_plain_text_spans_with_no_style_link_color_or_heading()
    {
        var spans = Analyzed.OfDescription("plain text").All;

        Assert.NotEmpty(spans);
        Assert.All(spans, s =>
        {
            Assert.Equal(SpanStyle.None, s.Styles);
            Assert.Null(s.LinkAddress);
            Assert.Null(s.Color);
            Assert.Equal(0, s.HeadingLevel);
        });
    }

    [Theory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public void Gives_no_spans_beyond_the_text_for_an_empty_string(MarkdownField field)
    {
        var spans = StyledSpans.Of("", field);

        Assert.Equal(0, spans.Sum(s => s.Length));
    }

    [Theory]
    [MemberData(nameof(Odd))]
    public void Accepts_any_string_in_a_description_because_no_Markdown_is_invalid(string markdown)
    {
        AssertCovers(markdown, StyledSpans.Of(markdown, MarkdownField.Description));
    }

    [Theory]
    [MemberData(nameof(Odd))]
    public void Accepts_any_string_in_a_title_because_no_Markdown_is_invalid(string markdown)
    {
        AssertCovers(markdown, StyledSpans.Of(markdown, MarkdownField.Title));
    }

    [Theory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public void Rejects_null_Markdown(MarkdownField field)
    {
        Assert.Throws<ArgumentNullException>(() => StyledSpans.Of(null!, field));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Gives_the_same_spans_for_the_same_input(string markdown)
    {
        var first = StyledSpans.Of(markdown, MarkdownField.Description);
        var second = StyledSpans.Of(markdown, MarkdownField.Description);

        Assert.Equal(first, second);
    }

    private static void AssertInOrderWithoutOverlap(IReadOnlyList<StyledSpan> spans)
    {
        for (var i = 1; i < spans.Count; i++)
        {
            Assert.True(spans[i].Start >= spans[i - 1].Start + spans[i - 1].Length, $"Span {i} starts before span {i - 1} ends.");
        }
    }

    private static void AssertCovers(string markdown, IReadOnlyList<StyledSpan> spans)
    {
        var next = 0;
        foreach (var span in spans)
        {
            Assert.Equal(next, span.Start);
            Assert.True(span.Length >= 0);
            next += span.Length;
        }

        Assert.Equal(markdown.Length, next);
    }
}
