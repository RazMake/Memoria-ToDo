using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class SnippetPickerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 14, 7, 9, TimeSpan.Zero);

    /// <summary>The snippets matching a field written as <c>text/typed«»</c>, with the "/" at <paramref name="slashIndex"/>.</summary>
    private static IReadOnlyList<ISnippet> Match(string marked, int slashIndex) =>
        SnippetPicker.Matching(Marked.Field(marked), slashIndex);

    // Opening

    [Theory]
    [InlineData("/", 0, true)]
    [InlineData("a /", 2, true)]
    [InlineData("a\n/", 2, true)]
    [InlineData("a/", 1, false)]
    [InlineData("a//", 2, false)]
    [InlineData("a/b", 1, false)]
    [InlineData("a b", 1, false)]
    [InlineData("a\t/", 2, false)]
    public void Opens_for_a_slash_at_a_line_start_or_after_a_space_and_not_otherwise(string text, int slashIndex, bool opens)
    {
        Assert.Equal(opens, SnippetPicker.OpensAt(text, slashIndex));
    }

    [Fact]
    public void Does_not_open_for_a_character_that_is_not_a_slash()
    {
        Assert.False(SnippetPicker.OpensAt("a b", 0));
    }

    // Matching

    [Fact]
    public void Matches_every_snippet_in_the_catalogs_order_when_nothing_is_typed_after_the_slash()
    {
        Assert.Equal(Snippets.All, Match("/«»", 0));
    }

    [Fact]
    public void Matches_a_snippet_whose_name_starts_with_the_typed_text()
    {
        Assert.Equal([Snippets.Note], Match("/no«»", 0));
    }

    [Fact]
    public void Matches_ignoring_case()
    {
        Assert.Equal([Snippets.Note], Match("/NoT«»", 0));
    }

    [Fact]
    public void Matches_a_snippet_when_any_word_of_its_name_starts_with_the_typed_text()
    {
        Assert.Equal([Snippets.HorizontalLine], Match("/line«»", 0));
        Assert.Equal([Snippets.DateStamp, Snippets.DateAndTimeStamp], Match("/stamp«»", 0));
    }

    [Fact]
    public void Does_not_match_text_from_the_middle_of_a_word()
    {
        Assert.Empty(Match("/ote«»", 0));
    }

    [Fact]
    public void Matches_several_snippets_in_the_catalogs_order()
    {
        Assert.Equal([Snippets.DateStamp, Snippets.DateAndTimeStamp], Match("/da«»", 0));
    }

    [Fact]
    public void Matches_nothing_for_text_no_name_starts_with()
    {
        Assert.Empty(Match("/zzz«»", 0));
    }

    [Fact]
    public void Matches_by_the_text_between_the_slash_and_the_cursor_only()
    {
        Assert.Equal([Snippets.Note], Match("buy /no«»thing else", 4));
    }

    [Fact]
    public void Matches_a_slash_that_follows_other_text_on_the_line()
    {
        Assert.Equal([Snippets.CodeBlock], Match("buy /code«»", 4));
    }

    [Theory]
    [InlineData("/no«»", 1)]
    [InlineData("/no«»", -1)]
    [InlineData("/no«»", 5)]
    [InlineData("/no«»", 100)]
    public void Matching_rejects_an_index_that_is_not_a_slash(string marked, int slashIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Match(marked, slashIndex));
    }

    [Fact]
    public void Matching_rejects_a_slash_that_is_after_the_cursor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Match("ab«»/no", 2));
    }

    // Inserting

    [Fact]
    public void Inserting_replaces_the_slash_and_the_text_typed_after_it()
    {
        var field = Marked.Field("due /da«» soon");

        var result = SnippetPicker.Insert(field, 4, Snippets.DateStamp, new SnippetInput { Now = Now });

        Assert.Equal("due 2026-03-05«» soon", Marked.Show(result));
    }

    [Fact]
    public void Inserting_with_only_the_slash_typed_replaces_just_the_slash()
    {
        var result = SnippetPicker.Insert(Marked.Field("/«»"), 0, Snippets.DateStamp, new SnippetInput { Now = Now });

        Assert.Equal("2026-03-05«»", Marked.Show(result));
    }

    [Fact]
    public void Inserting_a_block_removes_the_typed_text_and_adds_the_block()
    {
        var result = SnippetPicker.Insert(Marked.Field("/no«»"), 0, Snippets.Note, new SnippetInput());

        Assert.StartsWith("> [!NOTE]", result.Text);
        Assert.DoesNotContain("/no", result.Text);
    }

    [Fact]
    public void Inserting_rejects_an_index_that_is_not_a_slash()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SnippetPicker.Insert(Marked.Field("/da«»"), 1, Snippets.DateStamp, new SnippetInput { Now = Now }));
    }

    [Fact]
    public void Inserting_rejects_a_slash_that_is_after_the_cursor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SnippetPicker.Insert(Marked.Field("ab«»/da"), 2, Snippets.DateStamp, new SnippetInput { Now = Now }));
    }

    [Fact]
    public void Inserting_rejects_missing_input_the_snippet_needs()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SnippetPicker.Insert(Marked.Field("/da«»"), 0, Snippets.DateStamp, new SnippetInput()));
    }
}
