using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Text made to show exactly as given in Markdown: a backslash before each character Markdown could read as formatting.</summary>
[Unit]
public sealed class MarkdownTextTests
{
    [Theory]
    [InlineData("*", @"\*")]
    [InlineData("_", @"\_")]
    [InlineData("`", @"\`")]
    [InlineData("~", @"\~")]
    [InlineData("[", @"\[")]
    [InlineData("]", @"\]")]
    [InlineData("(", @"\(")]
    [InlineData(")", @"\)")]
    [InlineData("<", @"\<")]
    [InlineData(">", @"\>")]
    [InlineData("#", @"\#")]
    [InlineData("!", @"\!")]
    [InlineData("|", @"\|")]
    [InlineData("+", @"\+")]
    [InlineData("-", @"\-")]
    [InlineData(".", @"\.")]
    [InlineData("&", @"\&")]
    public void Puts_a_backslash_before_a_formatting_character(string text, string expected)
    {
        Assert.Equal(expected, MarkdownText.Escape(text));
    }

    [Fact]
    public void Escapes_a_backslash_so_it_shows_as_a_backslash()
    {
        Assert.Equal(@"a\\b", MarkdownText.Escape(@"a\b"));
    }

    [Fact]
    public void Escapes_every_formatting_character_in_the_text()
    {
        Assert.Equal(@"\*\*bold\*\* \[x\]\(y\)", MarkdownText.Escape("**bold** [x](y)"));
    }

    [Fact]
    public void Escapes_a_repeated_character_each_time()
    {
        Assert.Equal(@"\_\_\_", MarkdownText.Escape("___"));
    }

    [Theory]
    [InlineData("Buy milk")]
    [InlineData("abc XYZ 0123456789")]
    [InlineData("  spaced   out  ")]
    [InlineData("café 日本語 😀")]
    [InlineData("")]
    public void Leaves_letters_digits_spaces_and_other_text_as_given(string text)
    {
        Assert.Equal(text, MarkdownText.Escape(text));
    }

    [Fact]
    public void Keeps_the_text_around_the_escaped_characters()
    {
        Assert.Equal(@"Fix \#12 in the \<b\> tag", MarkdownText.Escape("Fix #12 in the <b> tag"));
    }
}
