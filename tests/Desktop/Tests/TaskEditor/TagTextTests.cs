using System.Globalization;
using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>The rule for text typed or pasted into the tag box: lowercase, no whitespace, the same on every PC.</summary>
[Unit]
public class TagTextTests
{
    private static DataObjectPastingEventArgs Paste(DataObject data, string formatToApply) =>
        new(data, false, formatToApply);

    [Theory]
    [InlineData("Work", "work")]
    [InlineData("WORK", "work")]
    [InlineData("work", "work")]
    [InlineData("Grüße", "grüße")]
    public void Letters_become_lowercase(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Theory]
    [InlineData("to do", "todo")]
    [InlineData(" to  do ", "todo")]
    [InlineData("to\tdo", "todo")]
    [InlineData("to\r\ndo", "todo")]
    [InlineData("to\u00a0do", "todo")]
    public void Whitespace_is_removed_and_the_words_join(string text, string expected) =>
        Assert.Equal(expected, TagText.Of(text));

    [Theory]
    [InlineData(" ")]
    [InlineData(" \t\r\n")]
    [InlineData("")]
    public void Text_of_only_whitespace_becomes_empty(string text) =>
        Assert.Equal("", TagText.Of(text));

    [Fact]
    public void Characters_that_are_not_letters_or_whitespace_are_kept()
    {
        Assert.Equal("q3-2026_#1", TagText.Of("Q3-2026_#1"));
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

    [Fact]
    public void Pasted_text_is_replaced_by_its_lowercase_text_without_whitespace()
    {
        var args = Paste(new DataObject(DataFormats.UnicodeText, "To Do\r\nList"), DataFormats.UnicodeText);

        TagText.OnPaste(this, args);

        Assert.Equal("todolist", args.DataObject.GetData(DataFormats.UnicodeText));
    }

    [Fact]
    public void A_paste_without_text_is_left_as_it_was()
    {
        var original = new DataObject(DataFormats.FileDrop, new[] { "a.txt" });
        var args = Paste(original, DataFormats.FileDrop);

        TagText.OnPaste(this, args);

        Assert.Same(original, args.DataObject);
    }
}
