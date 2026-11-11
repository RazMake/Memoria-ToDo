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

    [Fact]
    public void Gives_the_same_page_for_the_same_input()
    {
        Assert.Equal(Page("**a** :red[b]"), Page("**a** :red[b]"));
    }

    [Fact]
    public void Gives_a_different_page_for_each_theme()
    {
        Assert.NotEqual(Page("a", dark: false), Page("a", dark: true));
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
    public void Sets_no_background_so_the_window_shows_through()
    {
        Assert.DoesNotContain("background", Page("a", dark: true), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("background", Page("a", dark: false), StringComparison.OrdinalIgnoreCase);
    }

    // Markdown shows as what a reader sees: no source markers.

    [Fact]
    public void Shows_bold_without_its_asterisks()
    {
        var page = Page("**strong** word");

        AssertTag(page, "<(strong|b)>strong</(strong|b)>");
        Assert.DoesNotContain("**", page);
    }

    [Fact]
    public void Shows_a_heading_without_its_hash()
    {
        var page = Page("# Big title");

        AssertTag(page, "<h1[^>]*>Big title</h1>");
        Assert.DoesNotContain("# Big", page);
    }

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
    public void Escapes_html_in_an_images_alt_text()
    {
        var page = Page("![<b>x</b>](https://evil.example/a.png)");

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
    [InlineData("red")]
    [InlineData("orange")]
    [InlineData("green")]
    [InlineData("blue")]
    [InlineData("purple")]
    [InlineData("gray")]
    public void Colors_text_with_the_palette_value_of_the_light_theme(string name)
    {
        var color = Palette.Colors.Single(c => c.Name == name);

        AssertTag(Page($":{name}[urgent]"), $"<span style=\"color: ?{color.Light}\">urgent</span>");
    }

    [Theory]
    [InlineData("red")]
    [InlineData("orange")]
    [InlineData("green")]
    [InlineData("blue")]
    [InlineData("purple")]
    [InlineData("gray")]
    public void Colors_text_with_the_palette_value_of_the_dark_theme(string name)
    {
        var color = Palette.Colors.Single(c => c.Name == name);

        AssertTag(Page($":{name}[urgent]", dark: true), $"<span style=\"color: ?{color.Dark}\">urgent</span>");
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

    [Fact]
    public void Colors_text_that_holds_other_formatting()
    {
        AssertTag(Page(":red[very **urgent**]"), "<span style=\"color: ?#[0-9a-f]{6}\">very <(strong|b)>urgent</(strong|b)></span>");
    }

    [Fact]
    public void Shows_nothing_of_the_colors_markers()
    {
        Assert.DoesNotContain(":red[", Page(":red[urgent]"));
    }
}
