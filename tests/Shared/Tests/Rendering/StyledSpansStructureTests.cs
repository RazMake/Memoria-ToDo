using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansStructureTests
{
    public static TheoryData<MarkdownField, string> Texts()
    {
        string[] texts =
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
        return Both(texts);
    }

    public static TheoryData<MarkdownField, string> Odd() => Both(
    [
        "", " ", "\n", "**", "*", "`", "~~", "[", "](", "[]()", ":red[", ":red[]", "]", "\\", "<", "#", "# ", ":", "::red[x]", "[[[[", "**a", "_a",
    ]);

    private static TheoryData<MarkdownField, string> Both(string[] texts)
    {
        var data = new TheoryData<MarkdownField, string>();
        foreach (var field in new[] { MarkdownField.Title, MarkdownField.Description })
        {
            foreach (var text in texts)
            {
                data.Add(field, text);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Gives_spans_in_text_order_that_never_overlap_and_cover_the_whole_text_with_no_gaps(MarkdownField field, string markdown)
    {
        AssertCovers(markdown, StyledSpans.Of(markdown, field));
    }

    [Theory]
    [MemberData(nameof(Odd))]
    public void Accepts_any_string_because_no_Markdown_is_invalid(MarkdownField field, string markdown)
    {
        AssertCovers(markdown, StyledSpans.Of(markdown, field));
    }

    [Fact]
    public void Gives_plain_text_spans_with_no_style_link_color_or_heading()
    {
        var spans = Analyzed.OfDescription("plain text").All;

        Assert.NotEmpty(spans);
        Analyzed.AssertAllPlain(spans);
    }

    [Theory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public void Rejects_null_Markdown(MarkdownField field)
    {
        Assert.Throws<ArgumentNullException>(() => StyledSpans.Of(null!, field));
    }

    // Each span starts where the one before ends, so spans are in order, never overlap, and leave no gap.
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
