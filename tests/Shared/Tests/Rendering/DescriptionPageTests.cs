using System.Text.RegularExpressions;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>The web page of a description: rendered Markdown, no scripts, no raw HTML, images only from the clone, and the theme's colors.</summary>
[Unit]
public class DescriptionPageTests
{
    private static readonly Uri Clone = new("https://example.com/clone/");

    private static string Page(string markdown, bool dark = false) => DescriptionPage.Of(markdown, dark, Clone);

    private static void AssertTag(string html, string pattern) =>
        Assert.Matches(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline), html);

    [Fact]
    public void Makes_a_full_utf8_page()
    {
        var page = Page("hello");

        AssertTag(page, "<html");
        AssertTag(page, "charset=.?utf-8");
        Assert.Contains("hello", page);
    }

    [Theory]
    [InlineData(false, "#1f1f1f", "#f3f3f3")]
    [InlineData(true, "#f3f3f3", "#1f1f1f")]
    public void Sets_the_text_color_of_the_theme(bool dark, string expected, string other)
    {
        var page = Page("a", dark);

        Assert.Contains($"color:{expected}", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(other, page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sets_no_background_on_the_page_so_the_window_shows_through()
    {
        foreach (var dark in new[] { false, true })
        {
            var page = Page("a", dark);

            Assert.DoesNotContain("<body style=\"background", page, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Rules(page, "html", "body"), body => body.Contains("background", StringComparison.OrdinalIgnoreCase));
        }
    }

    // The text look: one style block with the theme's colors.

    private static IEnumerable<string> Rules(string page, params string[] tags)
    {
        var style = Regex.Match(page, "<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!style.Success)
        {
            return [];
        }

        return Regex.Matches(style.Groups[1].Value, "([^{}]+)\\{([^}]*)\\}")
            .Where(rule => rule.Groups[1].Value.Split(',')
                .Any(selector => tags.Any(tag => Regex.IsMatch(selector, $"(^|[\\s>]){tag}([\\s:.\\[]|$)"))))
            .Select(rule => rule.Groups[2].Value);
    }

    private static void AssertColorOf(string page, string tag, string color)
    {
        var bodies = Rules(page, tag).ToList();
        Assert.True(
            bodies.Any(b => Regex.IsMatch(b, $"(^|[;\\s])color\\s*:\\s*{color}", RegexOptions.IgnoreCase)),
            $"No style rule for <{tag}> sets color:{color}.");
    }

    private const string FadedGray15 = @"rgba\(\s*128\s*,\s*128\s*,\s*128\s*,\s*0?\.15\s*\)|#80808026";
    private const string FadedGray25 = @"rgba\(\s*128\s*,\s*128\s*,\s*128\s*,\s*0?\.25\s*\)|#80808040";

    [Theory]
    [InlineData(false, "#bc4c00", "#8a6a00", "#5c5c00")]
    [InlineData(true, "#ffa500", "#ffff00", "#9a9a00")]
    public void Colors_headings_1_to_3_in_the_themes_heading_colors(bool dark, string h1, string h2, string h3)
    {
        var page = Page("# a\n\n## b\n\n### c", dark);

        AssertColorOf(page, "h1", h1);
        AssertColorOf(page, "h2", h2);
        AssertColorOf(page, "h3", h3);
    }

    [Theory]
    [InlineData(false, "#0550ae")]
    [InlineData(true, "#79c0ff")]
    public void Colors_headings_4_to_6_in_the_themes_heading_4_color(bool dark, string h4)
    {
        var page = Page("#### a\n\n##### b\n\n###### c", dark);

        AssertColorOf(page, "h4", h4);
        AssertColorOf(page, "h5", h4);
        AssertColorOf(page, "h6", h4);
    }

    [Theory]
    [InlineData(false, "#0969da")]
    [InlineData(true, "#4493f8")]
    public void Colors_links_in_the_themes_link_color(bool dark, string link)
    {
        AssertColorOf(Page("[a](https://example.org)", dark), "a", link);
    }

    [Theory]
    [InlineData(false, "#4f5b93")]
    [InlineData(true, "#b4bdf5")]
    public void Colors_italic_text_in_the_themes_italic_color(bool dark, string italic)
    {
        AssertColorOf(Page("_a_", dark), "em", italic);
    }

    [Theory]
    [InlineData(false, "#9a3c00", FadedGray15)]
    [InlineData(true, "#f0a868", FadedGray25)]
    public void Shows_inline_code_in_the_themes_code_color_on_its_faded_background(bool dark, string code, string faded)
    {
        var page = Page("`a`", dark);

        AssertColorOf(page, "code", code);
        Assert.Contains(Rules(page, "code"), body => Regex.IsMatch(body, $"background[^:;]*:[^;]*({faded})", RegexOptions.IgnoreCase));
    }

    [Theory]
    [InlineData(false, "#9a3c00", FadedGray15)]
    [InlineData(true, "#f0a868", FadedGray25)]
    public void Shows_a_code_block_in_the_themes_code_color_on_its_faded_background(bool dark, string code, string faded)
    {
        var page = Page("```\na\n```", dark);

        Assert.Contains(Rules(page, "pre", "code"), body => Regex.IsMatch(body, $"(^|[;\\s])color\\s*:\\s*{code}", RegexOptions.IgnoreCase));
        Assert.Contains(Rules(page, "pre"), body => Regex.IsMatch(body, $"background[^:;]*:[^;]*({faded})", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Sets_the_system_interface_font_at_14_pixels()
    {
        var page = Page("a");

        Assert.Contains(Rules(page, "html", "body"), body =>
            Regex.IsMatch(body, "font-family\\s*:[^;]*system-ui", RegexOptions.IgnoreCase)
            && Regex.IsMatch(body, "font-size\\s*:\\s*14px", RegexOptions.IgnoreCase));
    }

    // Markdown shows as what a reader sees: no source markers.

    [Fact]
    public void Shows_a_pipe_table_as_a_table()
    {
        var page = Page("| a | b |\n| --- | --- |\n| 1 | 2 |");

        AssertTag(page, "<table");
        AssertTag(page, "<td[^>]*>1</td>");
    }

    [Fact]
    public void Shows_strikethrough_without_its_tildes()
    {
        var page = Page("~~gone~~");

        AssertTag(page, "<(del|s|strike)>gone</(del|s|strike)>");
        Assert.DoesNotContain("~~", page);
    }

    [Fact]
    public void Turns_a_typed_web_address_into_a_link()
    {
        AssertTag(Page("see https://example.org/page now"), "<a [^>]*href=\"https://example.org/page\"");
    }

    [Theory]
    [InlineData("NOTE")]
    [InlineData("TIP")]
    [InlineData("IMPORTANT")]
    [InlineData("WARNING")]
    [InlineData("CAUTION")]
    public void Shows_each_kind_of_alert_block_without_its_marker(string kind)
    {
        var page = Page($"> [!{kind}]\n> Read this");

        Assert.Contains("Read this", page);
        Assert.DoesNotContain($"[!{kind}]", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shows_a_single_line_break_as_a_line_break()
    {
        AssertTag(Page("first\nsecond"), "first<br ?/?>\\s*second");
    }

    [Fact]
    public void Keeps_a_task_link_address_as_written()
    {
        var address = "task:01900000-0000-7000-8000-000000000001";

        AssertTag(Page($"[other task]({address})"), $"<a [^>]*href=\"{address}\"[^>]*>other task</a>");
    }

    // A mention shows as a colored chip with nothing to click.

    private const string Mention = "[Ana](contact:a1 \"{firstName}\")";

    [Fact]
    public void Shows_a_mention_as_a_mention_span_holding_its_text()
    {
        var page = Page($"hi {Mention} bye");

        AssertTag(page, "<span class=\"mention\"[^>]*>Ana</span>");
    }

    [Fact]
    public void Shows_a_mention_without_a_link_or_its_address()
    {
        var page = Page($"hi {Mention} bye");

        Assert.DoesNotContain("<a ", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact:", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("firstName", page, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Styles_a_mention_with_the_themes_mention_background_in_the_one_style_block(bool dark)
    {
        var page = Page($"hi {Mention}", dark);

        Assert.Single(Regex.Matches(page, "<style", RegexOptions.IgnoreCase));
        Assert.Contains(
            Rules(page, ".mention"),
            body => Regex.IsMatch(body, $"background[^:;]*:\\s*{TextLooks.Of(dark).MentionBackground}", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Keeps_the_bold_of_a_mentions_text_inside_its_span()
    {
        var page = Page("[**Ana**](contact:a1 \"{firstName}\")");

        AssertTag(page, "<span class=\"mention\"[^>]*><strong>Ana</strong></span>");
    }

    [Fact]
    public void Shows_a_link_to_a_web_address_beside_a_mention_as_a_link()
    {
        var page = Page($"{Mention} and [site](https://example.org)");

        AssertTag(page, "<a href=\"https://example.org\">site</a>");
        AssertTag(page, "<span class=\"mention\"[^>]*>Ana</span>");
    }

    [Fact]
    public void Shows_a_contact_link_that_holds_an_image_as_no_mention()
    {
        var page = Page("[![face](images/a.png)](contact:a1 \"{fullName}\")");

        Assert.DoesNotContain("class=\"mention\"", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shows_a_contact_link_with_no_template_as_no_mention()
    {
        var page = Page("[Ana](contact:a1)");

        Assert.DoesNotContain("class=\"mention\"", page, StringComparison.OrdinalIgnoreCase);
    }

    // Each mention's span carries its number, so an app can tell which mention the pointer is over.

    private const string Bo = "[Bo Li](contact:b2 \"{fullName}\")";

    /// <summary>The (number, text) of every element that carries data-mention, in page order.</summary>
    private static (string Number, string Text)[] NumberedMentions(string page) =>
        Regex.Matches(page, "<span\\b[^>]*\\bdata-mention=\"([^\"]*)\"[^>]*>(.*?)</span>", RegexOptions.Singleline)
            .Select(m => (m.Groups[1].Value, Regex.Replace(m.Groups[2].Value, "<[^>]*>", "")))
            .ToArray();

    [Fact]
    public void Numbers_the_only_mention_0_on_the_mention_span_itself()
    {
        var page = Page($"hi {Mention} bye");

        AssertTag(page, "<span\\b(?=[^>]*class=\"mention\")(?=[^>]*data-mention=\"0\")[^>]*>Ana</span>");
    }

    [Fact]
    public void Numbers_mentions_from_0_in_the_order_they_appear()
    {
        var page = Page($"{Mention} then {Bo} and {Mention}");

        Assert.Equal([("0", "Ana"), ("1", "Bo Li"), ("2", "Ana")], NumberedMentions(page));
    }

    [Fact]
    public void Numbers_a_mention_in_a_heading_a_list_item_and_a_quote_in_document_order()
    {
        var page = Page($"# {Mention}\n\n- {Bo}\n\n> {Mention} and {Bo}");

        Assert.Equal([("0", "Ana"), ("1", "Bo Li"), ("2", "Ana"), ("3", "Bo Li")], NumberedMentions(page));
    }

    [Fact]
    public void Numbers_a_mention_whose_text_is_bold_on_its_span()
    {
        var page = Page($"[**Ana**](contact:a1 \"{{firstName}}\") and {Bo}");

        Assert.Equal([("0", "Ana"), ("1", "Bo Li")], NumberedMentions(page));
    }

    [Fact]
    public void Numbers_no_text_that_is_not_a_mention()
    {
        var page = Page("plain Ana, a [link](https://example.org), `code` and **bold**");

        Assert.DoesNotContain("data-mention", page, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[Ana](contact:a1)")]
    [InlineData("[![face](images/a.png)](contact:a1 \"{fullName}\")")]
    [InlineData("[Ana][p]\n\n[p]: contact:a1 \"{firstName}\"")]
    [InlineData("`[Ana](contact:a1 \"{firstName}\")`")]
    public void Does_not_count_a_contact_link_that_is_not_a_mention_in_the_numbers(string notMention)
    {
        var page = Page($"{notMention}\n\n{Bo}");

        Assert.Equal([("0", "Bo Li")], NumberedMentions(page));
    }

    [Fact]
    public void Numbers_the_mentions_exactly_as_the_mention_list_finds_them_in_the_same_Markdown()
    {
        var markdown = $"[Ana](contact:a1) `{Mention}`\n\n# {Bo}\n\n- {Mention}\n- [![f](images/a.png)](contact:c3 \"{{fullName}}\") {Bo}";

        var numbered = NumberedMentions(Page(markdown));

        var found = Mentions.In(markdown, title: false);
        Assert.Equal(found.Count, numbered.Length);
        Assert.Equal(Enumerable.Range(0, found.Count).Select(n => n.ToString()), numbered.Select(m => m.Number));
        Assert.Equal(found.Select(m => m.StoredText), numbered.Select(m => m.Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Numbers_the_mentions_the_same_in_every_theme_and_in_a_page_with_resizable_images(bool dark)
    {
        var markdown = $"![a](a.png) {Mention} and {Bo}";

        var plain = NumberedMentions(DescriptionPage.Of(markdown, dark, Clone));
        var resizable = NumberedMentions(DescriptionPage.WithResizableImages(markdown, dark, Clone));

        Assert.Equal([("0", "Ana"), ("1", "Bo Li")], plain);
        Assert.Equal(plain, resizable);
    }

    // Raw HTML and scripts.

    [Fact]
    public void Shows_raw_html_as_text()
    {
        var page = Page("<b>not bold</b>");

        Assert.Contains("&lt;b&gt;not bold&lt;/b&gt;", page);
        Assert.DoesNotContain("<b>not bold", page);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<SCRIPT src=\"https://evil.example/x.js\"></SCRIPT>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<a href=\"javascript:alert(1)\" onclick=\"alert(1)\">x</a>")]
    [InlineData("<style>body{display:none}</style>")]
    public void Puts_no_script_or_other_task_html_tag_in_the_page(string raw)
    {
        var page = Page($"before {raw} after");

        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a href=\"javascript", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style>body", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=alert(1)>", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Allows_no_script_in_the_content_security_policy()
    {
        var page = Page("a");

        Assert.Contains("Content-Security-Policy", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default-src 'none'", page);
        Assert.DoesNotContain("script-src", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Allows_images_in_the_content_security_policy_only_from_the_clone()
    {
        var page = Page("a");

        AssertTag(page, "img-src https://example.com/clone/");
        Assert.DoesNotContain("img-src *", page);
        Assert.DoesNotContain("data:", page);
    }

    [Fact]
    public void Carries_the_whole_content_security_policy_the_design_states()
    {
        Assert.Contains("default-src 'none'; img-src https://example.com/clone/; style-src 'unsafe-inline'", Page("a"));
    }

    [Fact]
    public void Escapes_html_in_an_images_alt_text()
    {
        var page = Page("![<b>x</b>](images/a.png)");

        AssertTag(page, "<img [^>]*alt=\"&lt;b&gt;x&lt;/b&gt;\"");
        Assert.DoesNotContain("<b>x", page);
    }

    // Images load only from the clone.

    [Fact]
    public void Loads_a_relative_image_from_the_clone()
    {
        var page = Page("![a cat](images/task/pic.png)");

        AssertTag(page, "<img [^>]*src=\"(https://example.com/clone/)?images/task/pic.png\"");
        AssertTag(page, "<img [^>]*alt=\"a cat\"");
    }

    [Fact]
    public void Loads_an_image_whose_address_is_inside_the_clone()
    {
        AssertTag(Page("![a](https://example.com/clone/images/t/i.png)"), "<img [^>]*src=\"https://example.com/clone/images/t/i.png\"");
    }

    [Theory]
    [InlineData("https://evil.example/a.png")]
    [InlineData("http://example.com/clone/a.png")]
    [InlineData("https://example.com/clone-evil/a.png")]
    [InlineData("https://example.com/other/a.png")]
    [InlineData("https://example.com.evil.example/clone/a.png")]
    [InlineData("//evil.example/a.png")]
    [InlineData("images/../../secret.png")]
    [InlineData("../secret.png")]
    [InlineData("file:///C:/secret.png")]
    [InlineData("C:/secret.png")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://example.com/clone/a.png")]
    public void Does_not_load_an_image_from_outside_the_clone(string address)
    {
        Assert.DoesNotContain("<img", Page($"![outside]({address})"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shows_an_image_from_outside_the_clone_as_a_link_with_its_alt_text()
    {
        AssertTag(Page("![outside pic](https://evil.example/a.png)"),
            "<a [^>]*href=\"https://evil.example/a.png\"[^>]*>outside pic</a>");
    }

    [Theory]
    [InlineData("https://example.com/clone")]
    [InlineData("ftp://example.com/clone/")]
    [InlineData("file:///C:/clone/")]
    [InlineData("https://example.com/clone/?query")]
    [InlineData("https://example.com/clone/#fragment")]
    public void Refuses_a_clone_address_that_is_not_an_http_or_https_address_ending_in_a_slash(string address)
    {
        Assert.Throws<ArgumentException>(() => DescriptionPage.Of("a", false, new Uri(address)));
    }

    [Fact]
    public void Refuses_a_relative_clone_address()
    {
        Assert.Throws<ArgumentException>(() => DescriptionPage.Of("a", false, new Uri("clone/", UriKind.Relative)));
    }

    // An image keeps the width the user gives it, and shows in a box the user can resize.

    private const string Picture = "images/task/pic.png";

    private static string[] BoxNumbers(string page) =>
        Regex.Matches(page, "<[^>]*\\bdata-image=\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToArray();

    private static string Compact(string page) => Regex.Replace(page, "\\s+", "");

    private static void AssertShownWidth(string page, int width) =>
        AssertTag(page, $"width:\\s*{width}px|width=\"{width}\"");

    private static void AssertNoShownWidth(string page) =>
        Assert.DoesNotMatch(new Regex("width:\\s*\\d+px|width=\"\\d+\"", RegexOptions.IgnoreCase), page);

    [Theory]
    [InlineData("![a|300](" + Picture + ")", "a", 300)]
    [InlineData("![a cat|1](" + Picture + ")", "a cat", 1)]
    [InlineData("![a|10000](" + Picture + ")", "a", 10000)]
    [InlineData("![a|b|250](" + Picture + ")", "a|b", 250)]
    [InlineData("![|40](" + Picture + ")", "", 40)]
    public void Shows_an_image_with_a_width_after_the_last_bar_at_that_width_with_the_rest_as_alt_text(
        string markdown, string alt, int width)
    {
        var page = Page(markdown);

        AssertTag(page, $"<img [^>]*alt=\"{Regex.Escape(alt)}\"");
        AssertShownWidth(page, width);
        Assert.DoesNotContain($"|{width}", page);
    }

    [Theory]
    [InlineData("a|0")]
    [InlineData("a|10001")]
    [InlineData("a|-5")]
    [InlineData("a|3.5")]
    [InlineData("a|wide")]
    [InlineData("a|300px")]
    [InlineData("a|")]
    [InlineData("a|300|wide")]
    public void Keeps_text_after_the_last_bar_in_the_alt_text_when_it_is_no_width_from_1_to_10000(string alt)
    {
        var page = Page($"![{alt}]({Picture})");

        // The alt text is escaped: its bar would otherwise be a regex alternation and match any alt text starting with "a".
        AssertTag(page, $"<img [^>]*alt=\"{Regex.Escape(alt)}\"");
        AssertNoShownWidth(page);
    }

    [Fact]
    public void Shows_an_image_with_no_width_at_its_own_size()
    {
        AssertNoShownWidth(Page($"![a]({Picture})"));
    }

    [Fact]
    public void Shows_an_image_from_outside_the_clone_with_a_width_as_a_link_with_the_alt_text_alone()
    {
        AssertTag(Page("![outside pic|300](https://evil.example/a.png)"),
            "<a [^>]*href=\"https://evil.example/a.png\"[^>]*>outside pic</a>");
    }

    [Fact]
    public void Puts_every_loaded_image_in_a_numbered_box_that_has_no_resize_corner()
    {
        var page = Compact(Page($"![a]({Picture})"));

        Assert.Contains("display:inline-block", page);
        Assert.DoesNotContain("resize:", page);
        Assert.Contains("overflow:hidden", page);
        Assert.Contains("max-width:100%", page);
        Assert.Matches("<[^>]*data-image=\"0\"[^>]*>(<[^>]*>)*<img", page);
    }

    [Fact]
    public void Keeps_the_width_of_each_image_on_the_page_without_resize_corners()
    {
        var page = Page($"![a|300]({Picture})\n\n![b]({Picture})\n\n![c|50]({Picture})");

        Assert.Equal(["0", "1", "2"], BoxNumbers(page));
        AssertTag(page, "<[^>]*data-image=\"0\"[^>]*width:\\s*300px");
        AssertTag(page, "<[^>]*data-image=\"2\"[^>]*width:\\s*50px");
    }

    [Fact]
    public void Gives_the_page_with_resizable_images_a_resize_corner_on_every_loaded_image_box()
    {
        var page = Compact(ResizablePage($"![a]({Picture})"));

        Assert.Contains("[data-image]{display:inline-block;", page);
        Assert.Contains("resize:horizontal", page);
        Assert.Contains("overflow:hidden", page);
        Assert.Contains("max-width:100%", page);
        Assert.Matches("<[^>]*data-image=\"0\"[^>]*>(<[^>]*>)*<img", page);
    }

    [Fact]
    public void Keeps_the_numbers_and_widths_on_the_page_with_resizable_images()
    {
        var page = ResizablePage(
            $"![a|300]({Picture})\n\n![b](https://evil.example/b.png)\n\n![c]({Picture})");

        Assert.Equal(["0", "2"], BoxNumbers(page));
        AssertTag(page, "<[^>]*data-image=\"0\"[^>]*width:\\s*300px");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Differs_between_the_two_pages_only_in_the_resize_rule(bool dark)
    {
        var markdown = $"# T\n\n![a|300]({Picture}) text **bold**\n\n:red[x] ![b]({Picture})";

        static string WithoutRule(string page) =>
            Compact(page).Replace("resize:horizontal", "").Replace(";;", ";").Replace(";}", "}");

        var plain = DescriptionPage.Of(markdown, dark, Clone);
        var resizable = DescriptionPage.WithResizableImages(markdown, dark, Clone);

        Assert.NotEqual(plain, resizable);
        Assert.Equal(WithoutRule(plain), WithoutRule(resizable));
    }

    [Fact]
    public void Keeps_the_content_security_policy_and_runs_no_script_on_the_page_with_resizable_images()
    {
        var page = ResizablePage($"![a|300]({Picture})");

        Assert.Contains("default-src 'none'; img-src https://example.com/clone/; style-src 'unsafe-inline'", page);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.com/clone")]
    [InlineData("ftp://example.com/clone/")]
    [InlineData("file:///C:/clone/")]
    [InlineData("https://example.com/clone/?query")]
    [InlineData("https://example.com/clone/#fragment")]
    public void Refuses_a_bad_clone_address_for_the_page_with_resizable_images(string address)
    {
        Assert.Throws<ArgumentException>(() => DescriptionPage.WithResizableImages("a", false, new Uri(address)));
    }

    [Fact]
    public void Refuses_a_relative_clone_address_for_the_page_with_resizable_images()
    {
        Assert.Throws<ArgumentException>(() =>
            DescriptionPage.WithResizableImages("a", false, new Uri("clone/", UriKind.Relative)));
    }

    private static string ResizablePage(string markdown, bool dark = false) =>
        DescriptionPage.WithResizableImages(markdown, dark, Clone);

    [Fact]
    public void Makes_the_image_fill_its_boxs_width()
    {
        Assert.Matches("width:100%", Compact(Page($"![a|300]({Picture})")));
    }

    [Fact]
    public void Sizes_the_box_to_the_width_the_image_was_given()
    {
        var page = Page($"![a|300]({Picture})");

        AssertTag(page, "<[^>]*data-image=\"0\"[^>]*width:\\s*300px");
    }

    [Fact]
    public void Numbers_each_image_in_the_order_the_markdown_holds_them_from_0_counting_one_shown_as_a_link()
    {
        var page = Page(
            $"![a]({Picture})\n\n![b](https://evil.example/b.png)\n\n[plain](https://example.org)\n\n![c|50]({Picture})");

        Assert.Equal(["0", "2"], BoxNumbers(page));
    }

    [Fact]
    public void Numbers_images_in_a_list_and_a_heading_in_the_order_the_markdown_holds_them()
    {
        var page = Page($"# ![a]({Picture})\n\n- ![b]({Picture})\n- ![c]({Picture})");

        Assert.Equal(["0", "1", "2"], BoxNumbers(page));
    }

    [Fact]
    public void Puts_no_box_around_text_without_an_image()
    {
        Assert.Empty(BoxNumbers(Page("hello [a](https://example.org)")));
    }

    [Fact]
    public void Keeps_the_content_security_policy_and_runs_no_script_with_an_image_that_sets_a_width()
    {
        var page = Page($"![a|300]({Picture})");

        Assert.Contains("default-src 'none'; img-src https://example.com/clone/; style-src 'unsafe-inline'", page);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("script-src", page, StringComparison.OrdinalIgnoreCase);
    }

    // Colors follow the theme.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Colors_text_of_each_palette_color_with_its_value_for_the_theme_and_shows_no_marker(bool dark)
    {
        Assert.NotEmpty(Palette.Colors);
        foreach (var color in Palette.Colors)
        {
            var page = Page($":{color.Name}[urgent]", dark);

            AssertTag(page, $"<span style=\"color: ?{(dark ? color.Dark : color.Light)}\">urgent</span>");
            Assert.DoesNotContain($":{color.Name}[", page);
        }
    }

    [Fact]
    public void Shows_an_unknown_color_name_as_plain_text()
    {
        var page = Page("a :teal[urgent] b");

        Assert.Contains(":teal[urgent]", page);
        Assert.DoesNotContain("<span style", page);
    }

    [Fact]
    public void Does_not_color_text_inside_inline_code()
    {
        var page = Page("`:red[urgent]`");

        AssertTag(page, "<code>:red\\[urgent\\]</code>");
    }

    // The color the page gives the word "zed": the nearest element around it that has a color, from its own style attribute or
    // from the style block's rule for its tag, else the page's text color. Only tag selectors and style attributes exist on the page.
    private static string ColorOfZed(string page)
    {
        var stack = new List<(string Tag, string? Color)>();
        string? zedColor = null;
        var found = false;
        var body = page[page.IndexOf("<body", StringComparison.OrdinalIgnoreCase)..];
        foreach (Match token in Regex.Matches(body, "<(/?)([a-z0-9]+)([^>]*?)(/?)>|[^<]+", RegexOptions.IgnoreCase))
        {
            if (!token.Groups[2].Success)
            {
                if (token.Value.Contains("zed"))
                {
                    zedColor = stack.LastOrDefault(e => e.Color is not null).Color;
                    found = true;
                }

                continue;
            }

            var tag = token.Groups[2].Value.ToLowerInvariant();
            if (token.Groups[1].Value == "/")
            {
                var open = stack.FindLastIndex(e => e.Tag == tag);
                if (open >= 0)
                {
                    stack.RemoveRange(open, stack.Count - open);
                }
            }
            else if (token.Groups[4].Value != "/" && tag is not ("br" or "img" or "hr"))
            {
                stack.Add((tag, ColorOfElement(page, tag, token.Groups[3].Value)));
            }
        }

        Assert.True(found, "The word zed isn't on the page.");
        return (zedColor ?? ColorDeclared(Rules(page, "body")) ?? throw new InvalidOperationException("The page sets no text color.")).ToLowerInvariant();
    }

    private static string? ColorOfElement(string page, string tag, string attributes) =>
        Regex.Match(attributes, "style=\"(?:[^\"]*[;\\s])?color\\s*:\\s*([^;\"\\s]+)", RegexOptions.IgnoreCase) is { Success: true } inline
            ? inline.Groups[1].Value
            : ColorDeclared(Rules(page, tag).Concat(attributes.Contains("class=\"mention\"") ? Rules(page, ".mention") : []));

    private static string? ColorDeclared(IEnumerable<string> bodies) =>
        bodies.Select(b => Regex.Matches(b, "(?:^|[;\\s])color\\s*:\\s*([^;\\s]+)", RegexOptions.IgnoreCase))
            .Where(m => m.Count > 0).Select(m => m[^1].Groups[1].Value).LastOrDefault();

    // What the design says the span of "zed" shows in: Rendering's one precedence, for the same Markdown and theme.
    private static string DesignedColorOfZed(string markdown, bool dark)
    {
        var at = markdown.IndexOf("zed", StringComparison.Ordinal);
        var span = StyledSpans.Of(markdown, MarkdownField.Description).Single(s => s.Start <= at && at < s.Start + s.Length);
        return TextLooks.Of(dark).ColorOf(span)!.ToLowerInvariant();
    }

    [Theory]
    [InlineData(":red[_zed_]", false)]
    [InlineData(":red[_zed_]", true)]
    [InlineData(":red[`zed`]", false)]
    [InlineData(":red[`zed`]", true)]
    [InlineData(":red[[zed](https://example.org)]", false)]
    [InlineData(":red[[zed](https://example.org)]", true)]
    [InlineData("[_zed_](https://example.org)", false)]
    [InlineData("[_zed_](https://example.org)", true)]
    [InlineData("[`zed`](https://example.org)", false)]
    [InlineData("[`zed`](https://example.org)", true)]
    public void Gives_text_with_more_than_one_style_the_one_color_that_Rendering_chooses_for_it(string markdown, bool dark)
    {
        var expected = DesignedColorOfZed(markdown, dark);

        Assert.Equal(expected, ColorOfZed(Page(markdown, dark)));
    }

    [Theory]
    [InlineData("# [zed](contact:a1 \"{firstName}\")", false)]
    [InlineData("# [zed](contact:a1 \"{firstName}\")", true)]
    [InlineData("*[zed](contact:a1 \"{firstName}\")*", false)]
    [InlineData("*[zed](contact:a1 \"{firstName}\")*", true)]
    [InlineData("## *[zed](contact:a1 \"{firstName}\")*", true)]
    public void Shows_a_mention_in_the_normal_text_color_even_inside_a_heading_or_italic_text(string markdown, bool dark)
    {
        var normal = ColorDeclared(Rules(Page("a", dark), "body"))!.ToLowerInvariant();

        Assert.Equal(normal, ColorOfZed(Page(markdown, dark)));
    }

    [Fact]
    public void Colors_text_that_holds_other_formatting()
    {
        AssertTag(Page(":red[very **urgent**]"), "<span style=\"color: ?#[0-9a-f]{6}\">very <(strong|b)>urgent</(strong|b)></span>");
    }
}
