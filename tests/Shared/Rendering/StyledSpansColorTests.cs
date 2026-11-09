using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

[Unit]
public class StyledSpansColorTests
{
    [Theory]
    [InlineData("red")]
    [InlineData("orange")]
    [InlineData("green")]
    [InlineData("blue")]
    [InlineData("purple")]
    [InlineData("gray")]
    public void Gives_the_text_of_a_palette_color_that_color(string name)
    {
        var spans = Analyzed.OfDescription($"a :{name}[urgent] b");

        Assert.All(spans.Over("urgent"), s => Assert.Equal(name, s.Color));
    }

    [Fact]
    public void Leaves_text_beside_a_color_uncolored()
    {
        var spans = Analyzed.OfDescription("before :red[urgent] after");

        Assert.All(spans.Over("before"), s => Assert.Null(s.Color));
        Assert.All(spans.Over("after"), s => Assert.Null(s.Color));
    }

    [Fact]
    public void Colors_the_text_of_a_title()
    {
        Assert.All(Analyzed.OfTitle("Fix :red[urgent] bug").Over("urgent"), s => Assert.Equal("red", s.Color));
    }

    [Fact]
    public void Marks_the_name_and_brackets_of_a_color_as_markers()
    {
        var spans = Analyzed.OfDescription("a :red[urgent] b");

        Assert.All(spans.Over(":red["), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
        Assert.All(spans.Over("]"), s => Assert.True(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Does_not_mark_the_colored_text_as_a_marker()
    {
        Assert.All(Analyzed.OfDescription("a :red[urgent] b").Over("urgent"), s => Assert.False(s.Styles.HasFlag(SpanStyle.Marker)));
    }

    [Fact]
    public void Shows_an_unknown_color_name_as_plain_text()
    {
        AssertNoColor(Analyzed.OfDescription("a :pink[x] b"));
    }

    [Fact]
    public void Colors_the_whole_phrase_when_brackets_inside_balance()
    {
        var spans = Analyzed.OfDescription(":red[see [docs](https://x)]");

        Assert.All(spans.Over("see "), s => Assert.Equal("red", s.Color));
        Assert.All(spans.Over("docs"), s =>
        {
            Assert.Equal("red", s.Color);
            Assert.Equal("https://x", s.LinkAddress);
        });
    }

    [Fact]
    public void Starts_no_color_when_the_colon_follows_a_letter()
    {
        AssertNoColor(Analyzed.OfDescription("ratio:red[1]"));
    }

    [Fact]
    public void Starts_no_color_when_the_colon_follows_a_digit()
    {
        AssertNoColor(Analyzed.OfDescription("v2:red[1]"));
    }

    [Theory]
    [InlineData("(:red[urgent])")]
    [InlineData("a :red[urgent]")]
    [InlineData(":red[urgent]")]
    [InlineData("a\n:red[urgent]")]
    [InlineData("done,:red[urgent]")]
    public void Starts_a_color_after_punctuation_a_space_or_a_lines_start(string markdown)
    {
        Assert.All(Analyzed.OfDescription(markdown).Over("urgent"), s => Assert.Equal("red", s.Color));
    }

    [Fact]
    public void Shows_braces_after_a_color_as_plain_text()
    {
        var spans = Analyzed.OfDescription(":red[x]{.big}");

        Assert.All(spans.Over("x"), s => Assert.Equal("red", s.Color));
        Assert.All(spans.Over("{.big}"), s =>
        {
            Assert.Null(s.Color);
            Assert.Equal(SpanStyle.None, s.Styles);
        });
    }

    [Fact]
    public void Accepts_lowercase_names_only()
    {
        AssertNoColor(Analyzed.OfDescription("a :Red[x] b"));
        AssertNoColor(Analyzed.OfDescription("a :RED[x] b"));
    }

    [Fact]
    public void Shows_a_color_that_is_not_closed_as_plain_text()
    {
        AssertNoColor(Analyzed.OfDescription("a :red[urgent"));
    }

    [Fact]
    public void Shows_a_color_that_would_cross_a_paragraph_break_as_plain_text()
    {
        AssertNoColor(Analyzed.OfDescription(":red[first\n\nsecond]"));
    }

    [Fact]
    public void Shows_the_form_inside_inline_code_as_code_not_color()
    {
        var spans = Analyzed.OfDescription("a `:red[x]` b");

        AssertNoColor(spans);
        Assert.All(spans.Over(":red[x]"), s => Assert.Equal(SpanStyle.Code, s.Styles));
    }

    [Fact]
    public void Shows_the_form_inside_a_code_block_as_code_not_color()
    {
        AssertNoColor(Analyzed.OfDescription("```\n:red[x]\n```"));
    }

    [Fact]
    public void Lets_a_color_hold_other_inline_formatting()
    {
        var spans = Analyzed.OfDescription(":red[a **b** c]");

        Assert.All(spans.Over("b"), s =>
        {
            Assert.Equal("red", s.Color);
            Assert.Equal(SpanStyle.Bold, s.Styles);
        });
        Assert.All(spans.Over("a "), s => Assert.Equal("red", s.Color));
    }

    [Fact]
    public void Lets_bold_text_hold_a_color()
    {
        var spans = Analyzed.OfDescription("**a :blue[x] c**");

        Assert.All(spans.Over("x"), s =>
        {
            Assert.Equal("blue", s.Color);
            Assert.Equal(SpanStyle.Bold, s.Styles);
        });
    }

    [Fact]
    public void Colors_two_neighbours_separately()
    {
        var spans = Analyzed.OfDescription(":red[a] :blue[b]");

        Assert.All(spans.Over("a"), s => Assert.Equal("red", s.Color));
        Assert.All(spans.Over("b"), s => Assert.Equal("blue", s.Color));
    }

    private static void AssertNoColor(Analyzed spans)
    {
        Assert.All(spans.All, s => Assert.Null(s.Color));
    }
}
