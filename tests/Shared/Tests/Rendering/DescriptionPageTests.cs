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
    [InlineData(false, "#0f548c", "#0e7a75", "#6b3fa0")]
    [InlineData(true, "#6cb8f6", "#4fd1c5", "#c4a7f5")]
    public void Colors_headings_1_to_3_in_the_themes_heading_colors(bool dark, string h1, string h2, string h3)
    {
        var page = Page("# a\n\n## b\n\n### c", dark);

        AssertColorOf(page, "h1", h1);
        AssertColorOf(page, "h2", h2);
        AssertColorOf(page, "h3", h3);
    }

    [Fact]
    public void Leaves_headings_4_to_6_in_the_text_color()
    {
        var page = Page("#### a\n\n##### b\n\n###### c");

        Assert.NotEmpty(Rules(page, "h1", "h2", "h3"));
        Assert.DoesNotContain(Rules(page, "h4", "h5", "h6"),
            body => Regex.IsMatch(body, "(^|[;\\s])color\\s*:", RegexOptions.IgnoreCase));
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
            : ColorDeclared(Rules(page, tag));

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

    [Fact]
    public void Colors_text_that_holds_other_formatting()
    {
        AssertTag(Page(":red[very **urgent**]"), "<span style=\"color: ?#[0-9a-f]{6}\">very <(strong|b)>urgent</(strong|b)></span>");
    }
}
