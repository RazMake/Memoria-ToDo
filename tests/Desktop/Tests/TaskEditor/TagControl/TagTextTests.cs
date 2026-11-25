using System.Globalization;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>
/// The rewrite of the whole text of the New tag box after every change: the tag as <c>TagName.Normalize</c> spells it, except that a
/// space typed at the end stays a space until the next character turns it into a hyphen. A leading or doubled space does nothing.
/// </summary>
[Unit]
public class TagTextTests
{
    [Theory]
    [InlineData("Work", "work")]
    [InlineData("WORK", "work")]
    [InlineData("Grüße", "grüße")]
    [InlineData("Q3-2026_#1", "q3-2026_#1")]
    public void Letters_become_lowercase_and_other_characters_are_kept(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Theory]
    [InlineData("to do", "to-do")]
    [InlineData("My  Work", "my-work")]
    [InlineData("to\tdo", "to-do")]
    [InlineData("to\r\ndo", "to-do")]
    [InlineData("to\u00a0do", "to-do")]
    public void Whitespace_between_words_becomes_one_hyphen(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Theory]
    [InlineData("to ", "to ")]
    [InlineData("to  ", "to ")]
    [InlineData("to-do ", "to-do ")]
    [InlineData("to \t", "to ")]
    public void A_trailing_space_stays_one_space_until_the_next_character(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Theory]
    [InlineData(" ", "")]
    [InlineData("  ", "")]
    [InlineData(" \t\r\n", "")]
    [InlineData("", "")]
    [InlineData(" to", "to")]
    [InlineData("   to  do", "to-do")]
    public void A_leading_space_does_nothing_and_text_of_only_whitespace_becomes_empty(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Fact]
    public void Rewriting_the_text_it_returned_changes_nothing()
    {
        foreach (var text in new[] { "to ", "my-work", "a ", "" })
        {
            Assert.Equal(text, TagText.Of(text));
        }
    }

    [Fact]
    public void Lowercasing_follows_the_invariant_culture_not_the_PCs()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("id", TagText.Of("ID"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
