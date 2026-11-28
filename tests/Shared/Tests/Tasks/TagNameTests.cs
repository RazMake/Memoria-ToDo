using System.Globalization;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The one rule that turns text into a tag, and the group tag of a task.</summary>
[Unit]
public sealed class TagNameTests
{
    [Theory]
    [InlineData("work", "work")]
    [InlineData(" WORK ", "work")]
    [InlineData("Work", "work")]
    [InlineData("My  Work", "my-work")]
    [InlineData("to\ndo", "to-do")]
    [InlineData("to\r\ndo", "to-do")]
    [InlineData("a \t \n b", "a-b")]
    [InlineData("  My   Big \n Work  ", "my-big-work")]
    [InlineData("already-hyphenated", "already-hyphenated")]
    [InlineData("a-  -b", "a---b")]
    public void Trims_lowercases_and_turns_each_whitespace_run_into_one_hyphen(string text, string expected)
    {
        Assert.Equal(expected, TagName.Normalize(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t\r\n ")]
    public void Gives_an_empty_tag_for_text_with_only_whitespace(string text)
    {
        Assert.Equal("", TagName.Normalize(text));
    }

    [Fact]
    public void Lowercases_by_the_invariant_culture_whatever_the_current_culture_is()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("title", TagName.Normalize("TITLE"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Lowercases_a_letter_outside_the_basic_plane_that_a_single_char_cannot_hold()
    {
        Assert.Equal("\U00010428", TagName.Normalize("\U00010400"));
        Assert.Equal("a-\U00010428", TagName.Normalize(" A \U00010400 "));
    }

    [Fact]
    public void Gives_a_normalized_tag_back_unchanged()
    {
        var once = TagName.Normalize("  Some  Tag ");

        Assert.Equal(once, TagName.Normalize(once));
    }

    [Fact]
    public void A_task_group_tag_is_its_first_tag()
    {
        Assert.Equal("a", Task("a", "b").GroupTag);
    }

    [Fact]
    public void A_task_group_tag_is_null_when_it_has_no_tags()
    {
        Assert.Null(Task().GroupTag);
    }

    private static TaskItem Task(params string[] tags) =>
        new("id", "T", "", "a0", tags, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
}
