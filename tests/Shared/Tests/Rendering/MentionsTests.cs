using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>Finding the mentions in a Markdown text, and swapping their text for a live one.</summary>
[Unit]
public class MentionsTests
{
    private const string Ana = "[Ana](contact:a1 \"{firstName}\")";

    private static Mention Only(string markdown, bool title = false)
    {
        var found = Mentions.In(markdown, title);
        return Assert.Single(found);
    }

    // In: what it lists

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lists_no_mention_in_text_that_has_none(bool title)
    {
        Assert.Empty(Mentions.In("plain [link](https://example.com) and **bold**", title));
        Assert.Empty(Mentions.In("", title));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lists_a_mentions_extent_from_its_bracket_through_its_closing_parenthesis(bool title)
    {
        var markdown = $"hi {Ana} bye";

        var mention = Only(markdown, title);

        Assert.Equal(3, mention.Start);
        Assert.Equal(Ana.Length, mention.Length);
        Assert.Equal(Ana, markdown.Substring(mention.Start, mention.Length));
    }

    [Fact]
    public void Lists_a_mentions_address_template_and_stored_text()
    {
        var mention = Only($"hi {Ana}");

        Assert.Equal("contact:a1", mention.Address);
        Assert.Equal("{firstName}", mention.Template);
        Assert.Equal("Ana", mention.StoredText);
    }

    [Fact]
    public void Unescapes_a_templates_quotes_and_backslashes()
    {
        var mention = Only("[Ana](contact:a1 \"say \\\"{firstName}\\\" \\\\ ok\")");

        Assert.Equal("say \"{firstName}\" \\ ok", mention.Template);
    }

    [Fact]
    public void Unescapes_a_stored_texts_brackets_and_backslashes()
    {
        var mention = Only("[a\\[b\\]c\\\\d](contact:a1 \"{firstName}\")");

        Assert.Equal("a[b]c\\d", mention.StoredText);
    }

    [Fact]
    public void Lists_mentions_in_the_order_they_appear_with_each_extent()
    {
        var markdown = $"{Ana} then [Bo](contact:b2 \"{{fullName}}\") and {Ana}";

        var found = Mentions.In(markdown, title: false);

        Assert.Equal(["contact:a1", "contact:b2", "contact:a1"], found.Select(m => m.Address));
        Assert.Equal(["Ana", "Bo", "Ana"], found.Select(m => m.StoredText));
        Assert.Equal(found.OrderBy(m => m.Start).ToList(), found);
        Assert.All(found, m => Assert.StartsWith("[", markdown.Substring(m.Start, m.Length)));
        Assert.All(found, m => Assert.EndsWith(")", markdown.Substring(m.Start, m.Length)));
    }

    [Fact]
    public void Lists_a_mention_in_a_list_item_a_heading_and_a_quote_of_a_description()
    {
        var markdown = $"# {Ana}\n\n- {Ana}\n\n> {Ana}";

        Assert.Equal(3, Mentions.In(markdown, title: false).Count);
    }

    [Fact]
    public void Does_not_list_a_mention_that_is_inside_code()
    {
        var markdown = $"`{Ana}`\n\n```\n{Ana}\n```";

        Assert.Empty(Mentions.In(markdown, title: false));
    }

    [Theory]
    [InlineData("[Ana](https://example.com \"{firstName}\")")]
    [InlineData("[Ana](task:t1 \"{firstName}\")")]
    [InlineData("[Ana](contact: \"{firstName}\")")]
    public void Does_not_list_a_link_that_is_not_to_a_contact(string markdown)
    {
        Assert.Empty(Mentions.In(markdown, title: false));
        Assert.Empty(Mentions.In(markdown, title: true));
    }

    // In: a title is inline Markdown only

    [Fact]
    public void Reads_a_title_as_inline_Markdown_only()
    {
        var indented = $"    {Ana}";

        Assert.Empty(Mentions.In(indented, title: false));
        Assert.Equal(4, Only(indented, title: true).Start);
    }

    [Fact]
    public void Lists_a_mention_after_a_hash_in_a_title_and_in_a_description()
    {
        var markdown = $"# {Ana}";

        Assert.Equal(2, Only(markdown, title: true).Start);
        Assert.Equal(2, Only(markdown, title: false).Start);
    }

    // A contact link that holds an image is not a mention

    [Theory]
    [InlineData("[![face](images/a.png)](contact:a1 \"{fullName}\")")]
    [InlineData("[Ana ![face](images/a.png)](contact:a1 \"{fullName}\")")]
    [InlineData("[**![face](images/a.png)**](contact:a1 \"{fullName}\")")]
    public void Does_not_list_a_contact_link_whose_text_holds_an_image(string markdown)
    {
        Assert.Empty(Mentions.In(markdown, title: false));
        Assert.Empty(Mentions.In(markdown, title: true));
    }

    [Fact]
    public void Lists_only_the_real_mention_beside_a_contact_link_that_holds_an_image()
    {
        var markdown = $"[![face](images/a.png)](contact:a1 \"{{fullName}}\") {Ana}";

        var mention = Only(markdown);

        Assert.Equal(markdown.IndexOf(Ana, StringComparison.Ordinal), mention.Start);
    }

    // WithTexts: the swap

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Swaps_a_mentions_text_for_the_callbacks_and_keeps_its_address_and_template(bool title)
    {
        var result = Mentions.WithTexts($"hi {Ana} bye", title, _ => "Bob");

        Assert.Equal("hi [Bob](contact:a1 \"{firstName}\") bye", result);
    }

    [Fact]
    public void Swaps_each_mention_with_the_text_for_that_mention_whatever_the_lengths()
    {
        var markdown = $"{Ana} and [Bo](contact:b2 \"{{fullName}}\") end";

        var result = Mentions.WithTexts(markdown, title: false, m => m.Address == "contact:a1" ? "Alexandrina" : "B");

        Assert.Equal("[Alexandrina](contact:a1 \"{firstName}\") and [B](contact:b2 \"{fullName}\") end", result);
    }

    [Fact]
    public void Asks_the_callback_once_for_each_mention_in_order_with_the_mention_that_In_lists()
    {
        var markdown = $"{Ana} and [Bo](contact:b2 \"{{fullName}}\")";
        var asked = new List<Mention>();

        Mentions.WithTexts(markdown, title: false, m => { asked.Add(m); return null; });

        Assert.Equal(Mentions.In(markdown, title: false), asked);
    }

    [Fact]
    public void Keeps_the_stored_text_of_a_mention_the_callback_has_no_text_for()
    {
        var markdown = $"{Ana} and [B\\]o](contact:b2 \"{{fullName}}\")";

        var result = Mentions.WithTexts(markdown, title: false, m => m.Address == "contact:a1" ? "Alex" : null);

        Assert.Equal("[Alex](contact:a1 \"{firstName}\") and [B\\]o](contact:b2 \"{fullName}\")", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no mentions here")]
    [InlineData("a [link](https://example.com) and `code`")]
    public void Returns_text_without_mentions_unchanged(string markdown)
    {
        Assert.Equal(markdown, Mentions.WithTexts(markdown, title: false, _ => "X"));
    }

    [Fact]
    public void Changes_nothing_outside_the_mentions_byte_for_byte()
    {
        var markdown = $"  # Héllo \u00e9  \r\n\r\n-\t[web](https://x.y/a_b)  \u2603 {Ana}\r\n\r\n```\r\n{Ana}\r\n```\r\n   trailing  ";

        var result = Mentions.WithTexts(markdown, title: false, _ => "Bob");

        var swapped = markdown.Replace($"\u2603 {Ana}", "\u2603 [Bob](contact:a1 \"{firstName}\")");
        Assert.Equal(swapped, result);
    }

    // WithTexts: the swapped text shows as written

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Escapes_every_markdown_character_in_the_new_text_so_it_shows_as_written(bool title)
    {
        const string text = "*a* _b_ `c` [d](u) \\e ~~f~~ <g> # h :red[i]";

        var result = Mentions.WithTexts($"hi {Ana} bye", title, _ => text);

        var mention = Only(result, title);
        Assert.Equal("contact:a1", mention.Address);
        Assert.Equal("{firstName}", mention.Template);
        var spans = Analyzed.Of(result, title ? MarkdownField.Title : MarkdownField.Description).All
            .Where(s => s.Styles.HasFlag(SpanStyle.Mention))
            .ToList();
        Assert.All(spans, s => Assert.Equal(SpanStyle.None, s.Styles & ~SpanStyle.Marker & ~SpanStyle.Mention));
        Assert.Equal(text, string.Concat(spans.Where(s => !s.Styles.HasFlag(SpanStyle.Marker)).Select(s => result.Substring(s.Start, s.Length))));
    }

    [Fact]
    public void Escapes_a_closing_bracket_so_the_new_text_cannot_end_the_link_early()
    {
        var result = Mentions.WithTexts($"{Ana} tail", title: false, _ => "a](https://evil.example) b");

        var mention = Only(result);
        Assert.Equal("contact:a1", mention.Address);
        Assert.EndsWith(" tail", result);
        Assert.DoesNotContain(Analyzed.OfDescription(result).All, s => s.LinkAddress == "https://evil.example");
    }

    // WithTexts: a contact link that holds an image is not swapped

    [Fact]
    public void Leaves_a_contact_link_that_holds_an_image_unswapped_and_never_asks_about_it()
    {
        var link = "[![face](images/a.png)](contact:a1 \"{fullName}\")";
        var markdown = $"{link} and {Ana}";
        var asked = new List<Mention>();

        var result = Mentions.WithTexts(markdown, title: false, m => { asked.Add(m); return "Bob"; });

        Assert.Equal($"{link} and [Bob](contact:a1 \"{{firstName}}\")", result);
        Assert.Single(asked);
    }

    // A contact link with no template in its title is not a mention

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Does_not_list_a_contact_link_with_no_template(bool title)
    {
        Assert.Empty(Mentions.In("hi [Ana](contact:a1) bye", title));
    }

    [Fact]
    public void Lists_only_the_real_mention_beside_a_contact_link_with_no_template()
    {
        var markdown = $"[Bo](contact:b2) {Ana}";

        Assert.Equal(markdown.IndexOf(Ana, StringComparison.Ordinal), Only(markdown).Start);
    }

    [Fact]
    public void Leaves_a_contact_link_with_no_template_unswapped_and_never_asks_about_it()
    {
        var markdown = $"[Bo](contact:b2) and {Ana}";
        var asked = new List<Mention>();

        var result = Mentions.WithTexts(markdown, title: false, m => { asked.Add(m); return "Bob"; });

        Assert.Equal("[Bo](contact:b2) and [Bob](contact:a1 \"{firstName}\")", result);
        Assert.Single(asked);
    }

    // A reference-style contact link is not a mention

    public static TheoryData<string> ReferenceStyleLinks => new()
    {
        "[p]: contact:ana \"{fullName}\"\n\n[Ana][p]",
        "[Ana][p]\n\n[p]: contact:ana \"{fullName}\"",
        "[p]: contact:ana \"{fullName}\"\n\n[p][]",
        "[p][]\n\n[p]: contact:ana \"{fullName}\"",
        "[p]: contact:ana \"{fullName}\"\n\n[p]",
        "[p]\n\n[p]: contact:ana \"{fullName}\"",
    };

    [Theory]
    [MemberData(nameof(ReferenceStyleLinks))]
    public void Does_not_list_a_reference_style_contact_link(string markdown)
    {
        Assert.Empty(Mentions.In(markdown, title: false));
    }

    [Theory]
    [MemberData(nameof(ReferenceStyleLinks))]
    public void Leaves_a_reference_style_contact_link_unchanged_when_swapping(string markdown)
    {
        Assert.Equal(markdown, Mentions.WithTexts(markdown, title: false, _ => "Bob"));
    }

    [Fact]
    public void Lists_only_the_real_mention_beside_a_reference_style_contact_link()
    {
        var markdown = $"[Ana][p] {Ana}\n\n[p]: contact:ana \"{{fullName}}\"";

        Assert.Equal(markdown.IndexOf(Ana, StringComparison.Ordinal), Only(markdown).Start);
    }

    // WithTexts: bad arguments

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejects_null_markdown_when_listing(bool title)
    {
        Assert.Throws<ArgumentNullException>(() => Mentions.In(null!, title));
    }

    [Fact]
    public void Rejects_null_markdown_when_swapping()
    {
        Assert.Throws<ArgumentNullException>(() => Mentions.WithTexts(null!, title: false, _ => null));
    }

    [Fact]
    public void Rejects_a_null_callback_when_swapping()
    {
        Assert.Throws<ArgumentNullException>(() => Mentions.WithTexts(Ana, title: false, null!));
    }

    [Fact]
    public void Rejects_a_null_callback_even_when_the_text_has_no_mention()
    {
        Assert.Throws<ArgumentNullException>(() => Mentions.WithTexts("plain", title: false, null!));
    }
}
