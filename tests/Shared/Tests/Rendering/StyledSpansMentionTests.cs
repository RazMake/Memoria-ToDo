using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>How a mention shows in the styled spans: its text carries the Mention style and the link's address, its link syntax is markers.</summary>
[Unit]
public class StyledSpansMentionTests
{
    private const string Mention = "[Ana](contact:a1 \"{firstName}\")";
    private const string MentionTail = "](contact:a1 \"{firstName}\")";

    // A mention is a contact link.

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Gives_a_contact_links_text_the_Mention_style_and_keeps_its_address(MarkdownField field)
    {
        var spans = Analyzed.Of($"hi {Mention} bye", field);

        Assert.All(spans.Over("Ana"), s =>
        {
            Assert.True(s.Styles.HasFlag(SpanStyle.Mention));
            Assert.Equal("contact:a1", s.LinkAddress);
        });
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Leaves_text_beside_a_mention_without_the_Mention_style(MarkdownField field)
    {
        var spans = Analyzed.Of($"hi {Mention} bye", field);

        Assert.All(spans.Over("hi"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
        Assert.All(spans.Over("bye"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Marks_a_mentions_brackets_and_template_address_as_markers_and_its_text_as_not(MarkdownField field)
    {
        var spans = Analyzed.Of($"hi {Mention} bye", field);

        Analyzed.AssertMarkers(spans.Over("["));
        Analyzed.AssertMarkers(spans.Over(MentionTail));
        Analyzed.AssertNotMarkers(spans.Over("Ana"));
    }

    [Theory]
    [InlineData("see [Ana](https://example.com/ana) now")]
    [InlineData("see [Ana](task:t1) now")]
    [InlineData("see [Ana](contact:) now")]
    [InlineData("see [Ana](mailto:ana@example.com) now")]
    public void Gives_a_link_that_is_not_to_a_contact_no_Mention_style(string markdown)
    {
        Assert.All(Analyzed.OfDescription(markdown).All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
        Assert.All(Analyzed.OfTitle(markdown).All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Fact]
    public void Gives_typed_contact_text_outside_a_link_no_Mention_style()
    {
        var spans = Analyzed.OfDescription("write contact:a1 here");

        Assert.All(spans.All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Fact]
    public void Shows_a_mention_in_a_list_item_with_the_Mention_style()
    {
        var spans = Analyzed.OfDescription($"- one\n- {Mention}");

        Assert.All(spans.Over("Ana"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    // Styling inside the mention.

    [Theory]
    [InlineData("**Ana**", SpanStyle.Bold)]
    [InlineData("*Ana*", SpanStyle.Italic)]
    public void Keeps_the_bold_or_italic_of_a_mentions_text_beside_the_Mention_style(string text, SpanStyle style)
    {
        var spans = Analyzed.OfDescription($"[{text}](contact:a1 \"{{firstName}}\")");

        Assert.All(spans.Over("Ana"), s => Assert.Equal(style | SpanStyle.Mention, s.Styles));
    }

    // The mention's text is Markdown, so its inline styles show inside the mention.

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Shows_italic_inside_a_mention_for_an_emphasized_part_of_its_text(MarkdownField field)
    {
        var markdown = "[Ana Pop (_anap_)](contact:a1 \"{fullName} (_{alias}_)\")";
        var spans = Analyzed.Of(markdown, field);

        Assert.All(spans.Over("anap"), s => Assert.Equal(SpanStyle.Italic | SpanStyle.Mention, s.Styles));
        Assert.All(spans.Over("Ana Pop ("), s => Assert.Equal(SpanStyle.Mention, s.Styles));
        Analyzed.AssertMarkers(spans.Over("_"));
        Analyzed.AssertMarkers(spans.Over("_", startAt: markdown.IndexOf("anap", StringComparison.Ordinal)));
        Assert.All(spans.Over("_"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Theory]
    [InlineData("**Ana**", "Ana", SpanStyle.Bold)]
    [InlineData("~~Ana~~", "Ana", SpanStyle.Strikethrough)]
    [InlineData("`Ana`", "Ana", SpanStyle.Code)]
    [InlineData("Ana ***Pop***", "Pop", SpanStyle.Bold | SpanStyle.Italic)]
    public void Shows_each_inline_style_of_a_mentions_text_beside_the_Mention_style(string text, string styled, SpanStyle style)
    {
        foreach (var field in new[] { MarkdownField.Description, MarkdownField.Title })
        {
            var spans = Analyzed.Of($"[{text}](contact:a1 \"{{fullName}}\")", field);

            Assert.All(spans.Over(styled), s => Assert.Equal(style | SpanStyle.Mention, s.Styles));
            Assert.All(spans.Over(styled), s => Assert.Equal("contact:a1", s.LinkAddress));
        }
    }

    [Theory]
    [InlineData("**Ana**", "**")]
    [InlineData("~~Ana~~", "~~")]
    [InlineData("`Ana`", "`")]
    public void Marks_the_markdown_characters_inside_a_mentions_text_as_markers_that_are_still_part_of_the_mention(string text, string marker)
    {
        var markdown = $"[{text}](contact:a1 \"{{fullName}}\")";
        var spans = Analyzed.OfDescription(markdown);
        var closing = markdown.IndexOf(marker, 1 + marker.Length, StringComparison.Ordinal);

        Analyzed.AssertMarkers(spans.Over(marker, startAt: 1));
        Analyzed.AssertMarkers(spans.Over(marker, startAt: closing));
        Assert.All(spans.Over(marker, startAt: 1), s => Assert.True(s.Styles.HasFlag(SpanStyle.Mention)));
        Assert.All(spans.Over(marker, startAt: closing), s => Assert.True(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Shows_a_palette_color_inside_a_mentions_text_and_marks_its_directive_as_markers(MarkdownField field)
    {
        var spans = Analyzed.Of("[Ana :red[Pop]](contact:a1 \"{fullName}\")", field);

        Assert.All(spans.Over("Pop"), s =>
        {
            Assert.Equal("red", s.Color);
            Assert.Equal(SpanStyle.Mention, s.Styles);
        });
        Assert.All(spans.Over("Ana"), s => Assert.Null(s.Color));
        Analyzed.AssertMarkers(spans.Over(":red["));
        Assert.All(spans.Over(":red["), s => Assert.True(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Shows_escaped_punctuation_inside_a_mentions_text_as_written_with_no_style(MarkdownField field)
    {
        var spans = Analyzed.Of("[Ana \\*QA\\* (_ana\\.pop_)](contact:a1 \"{fullName}\")", field);

        Assert.All(spans.Over("QA"), s => Assert.Equal(SpanStyle.Mention, s.Styles));
        Analyzed.AssertMarkers(spans.Over("\\"));
        Analyzed.AssertNotMarkers(spans.Over("*"));
        Assert.All(spans.Over("ana"), s => Assert.Equal(SpanStyle.Italic | SpanStyle.Mention, s.Styles));
        Assert.All(spans.Over("pop"), s => Assert.Equal(SpanStyle.Italic | SpanStyle.Mention, s.Styles));
    }

    // A contact link that holds an image is not a mention.

    [Theory]
    [InlineData("[![face](images/a.png)](contact:a1 \"{fullName}\")")]
    [InlineData("[Ana ![face](images/a.png)](contact:a1 \"{fullName}\")")]
    [InlineData("[**![face](images/a.png)**](contact:a1 \"{fullName}\")")]
    public void Gives_a_contact_link_whose_text_holds_an_image_no_Mention_style(string markdown)
    {
        Assert.All(Analyzed.OfDescription(markdown).All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
        Assert.All(Analyzed.OfTitle(markdown).All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    [Fact]
    public void Keeps_the_link_address_of_a_contact_link_that_holds_an_image()
    {
        var spans = Analyzed.OfDescription("[![face](images/a.png)](contact:a1 \"{fullName}\")");

        Assert.Contains(spans.All, s => s.LinkAddress == "contact:a1");
    }

    // A reference-style contact link is not a mention.

    [Theory]
    [InlineData("[p]: contact:ana \"{fullName}\"\n\n[Ana][p]")]
    [InlineData("[Ana][p]\n\n[p]: contact:ana \"{fullName}\"")]
    [InlineData("[p]: contact:ana \"{fullName}\"\n\n[p][]")]
    public void Gives_a_reference_style_contact_link_no_Mention_style(string markdown)
    {
        Assert.All(Analyzed.OfDescription(markdown).All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }

    // A contact link with no template in its title is not a mention.

    [Theory]
    [InlineData(MarkdownField.Description)]
    [InlineData(MarkdownField.Title)]
    public void Gives_a_contact_link_with_no_template_no_Mention_style_and_keeps_its_address(MarkdownField field)
    {
        var spans = Analyzed.Of("hi [Ana](contact:a1) bye", field);

        Assert.All(spans.Over("Ana"), s =>
        {
            Assert.False(s.Styles.HasFlag(SpanStyle.Mention));
            Assert.Equal("contact:a1", s.LinkAddress);
        });
        Assert.All(spans.All, s => Assert.False(s.Styles.HasFlag(SpanStyle.Mention)));
    }
}
