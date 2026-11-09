using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Making text safe as the label of a Markdown link or image.</summary>
[Unit]
public sealed class MarkdownLabelTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Buy milk", "Buy milk")]
    [InlineData("*urgent* milk", "*urgent* milk")]
    [InlineData("_a_ `b` ~~c~~ <d> (e) #f", "_a_ `b` ~~c~~ <d> (e) #f")]
    public void Keeps_text_without_brackets_or_backslashes_as_it_is(string label, string expected)
    {
        Assert.Equal(expected, MarkdownLabel.Escape(label));
    }

    [Theory]
    [InlineData("A [bracketed] name", @"A \[bracketed\] name")]
    [InlineData("open [ only", @"open \[ only")]
    [InlineData("close ] only", @"close \] only")]
    [InlineData("[[]]", @"\[\[\]\]")]
    public void Puts_a_backslash_before_each_square_bracket(string label, string expected)
    {
        Assert.Equal(expected, MarkdownLabel.Escape(label));
    }

    [Theory]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData(@"ends with\", @"ends with\\")]
    [InlineData(@"\\", @"\\\\")]
    public void Doubles_each_backslash(string label, string expected)
    {
        Assert.Equal(expected, MarkdownLabel.Escape(label));
    }

    [Fact]
    public void Escapes_a_backslash_and_the_bracket_after_it_separately()
    {
        Assert.Equal(@"\\\[", MarkdownLabel.Escape(@"\["));
    }

    [Fact]
    public void Keeps_the_markdown_around_escaped_brackets()
    {
        Assert.Equal(@"*fix* \[a\] **bug**", MarkdownLabel.Escape("*fix* [a] **bug**"));
    }

    [Fact]
    public void Refuses_a_missing_label()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownLabel.Escape(null!));
    }
}
