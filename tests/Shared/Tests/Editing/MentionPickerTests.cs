using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class MentionPickerTests
{
    private const string Mention = "[Aayushi Mohan](mailto:aayushi@example.com)";

    // Opening

    [Theory]
    [InlineData("@", 0)]
    [InlineData("a @", 2)]
    [InlineData("a\n@", 2)]
    [InlineData("(@", 1)]
    [InlineData("a-@", 2)]
    [InlineData("a\t@", 2)]
    [InlineData("a.@", 2)]
    [InlineData("@@", 1)]
    public void Opens_for_an_at_sign_at_the_start_or_after_a_character_that_is_not_a_letter_or_a_digit(string text, int atIndex)
    {
        Assert.True(MentionPicker.OpensAt(text, atIndex));
    }

    [Theory]
    [InlineData("a@b.com", 1)]
    [InlineData("Z@", 1)]
    [InlineData("5@", 1)]
    [InlineData("é@", 1)]
    public void Does_not_open_for_an_at_sign_right_after_a_letter_or_a_digit(string text, int atIndex)
    {
        Assert.False(MentionPicker.OpensAt(text, atIndex));
    }

    [Theory]
    [InlineData("a b", 1)]
    [InlineData("@a", 1)]
    [InlineData("@", -1)]
    [InlineData("@", 1)]
    [InlineData("@", 100)]
    [InlineData("", 0)]
    public void Does_not_open_when_the_index_is_not_an_at_sign_or_is_outside_the_text(string text, int atIndex)
    {
        Assert.False(MentionPicker.OpensAt(text, atIndex));
    }

    [Fact]
    public void Opening_rejects_missing_text()
    {
        Assert.Throws<ArgumentNullException>(() => MentionPicker.OpensAt(null!, 0));
    }

    [Fact]
    public void Opens_after_a_slash_where_the_snippet_picker_does_not()
    {
        Assert.True(MentionPicker.OpensAt("a/@", 2));
        Assert.False(SnippetPicker.OpensAt("a/@", 2));
        Assert.True(SnippetPicker.OpensAt("/", 0));
        Assert.True(MentionPicker.OpensAt("(@", 1));
        Assert.False(SnippetPicker.OpensAt("(/", 1));
    }

    // Query

    [Theory]
    [InlineData("@«»", 0, "")]
    [InlineData("@Aa«»", 0, "Aa")]
    [InlineData("@Aayushi Mo«»", 0, "Aayushi Mo")]
    [InlineData("@Aayushi «»", 0, "Aayushi ")]
    [InlineData("call @Aa«» now", 5, "Aa")]
    [InlineData("a\n(@Aa«»", 3, "Aa")]
    [InlineData("@@«»", 1, "")]
    public void Query_is_the_text_between_the_at_sign_and_the_cursor_spaces_included(string marked, int atIndex, string query)
    {
        Assert.Equal(query, MentionPicker.QueryOf(Marked.Field(marked), atIndex));
    }

    [Fact]
    public void Query_ignores_text_after_the_cursor()
    {
        Assert.Equal("Aa", MentionPicker.QueryOf(Marked.Field("@Aa«»yushi"), 0));
    }

    [Theory]
    [InlineData("«»@Aa", 0)]
    [InlineData("«»a @Aa", 2)]
    [InlineData("@«»Aa", 1)]
    [InlineData("ab«»@Aa", 2)]
    public void Query_is_null_when_the_cursor_is_at_or_before_the_at_sign(string marked, int atIndex)
    {
        Assert.Null(MentionPicker.QueryOf(Marked.Field(marked), atIndex));
    }

    [Theory]
    [InlineData("Aa«»", 0)]
    [InlineData("a«»b", 1)]
    [InlineData("a@Aa«»", 1)]
    [InlineData("5@Aa«»", 1)]
    [InlineData("@Aa«»", 3)]
    [InlineData("@Aa«»", -1)]
    [InlineData("@Aa«»", 100)]
    public void Query_is_null_when_the_at_sign_is_gone_or_no_longer_opens_a_list(string marked, int atIndex)
    {
        Assert.Null(MentionPicker.QueryOf(Marked.Field(marked), atIndex));
    }

    [Theory]
    [InlineData("@Aa\nBb«»", 0)]
    [InlineData("@\n«»", 0)]
    public void Query_is_null_when_a_line_break_lies_between_the_at_sign_and_the_cursor(string marked, int atIndex)
    {
        Assert.Null(MentionPicker.QueryOf(Marked.Field(marked), atIndex));
    }

    [Fact]
    public void Query_ignores_a_line_break_after_the_cursor()
    {
        Assert.Equal("Aa", MentionPicker.QueryOf(Marked.Field("@Aa«»\nnext"), 0));
    }

    [Fact]
    public void Query_rejects_a_missing_field()
    {
        Assert.Throws<ArgumentNullException>(() => MentionPicker.QueryOf(null!, 0));
    }

    // Inserting

    [Fact]
    public void Inserting_replaces_the_at_sign_and_the_text_typed_after_it_and_puts_the_cursor_after_the_mention()
    {
        var result = MentionPicker.Insert(Marked.Field("ask @Aay«» today"), 4, Mention);

        Assert.Equal($"ask {Mention}«» today", Marked.Show(result));
    }

    [Fact]
    public void Inserting_with_only_the_at_sign_typed_replaces_just_the_at_sign()
    {
        var result = MentionPicker.Insert(Marked.Field("@«»"), 0, Mention);

        Assert.Equal($"{Mention}«»", Marked.Show(result));
    }

    [Fact]
    public void Inserting_keeps_the_typed_name_with_spaces_out_of_the_result()
    {
        var result = MentionPicker.Insert(Marked.Field("(@Aayushi Mo«»)"), 1, Mention);

        Assert.Equal($"({Mention}«»)", Marked.Show(result));
    }

    [Fact]
    public void Inserting_keeps_the_mention_exactly_as_given()
    {
        var result = MentionPicker.Insert(Marked.Field("@«»"), 0, " [a]\n(b) ");

        Assert.Equal(" [a]\n(b) «»", Marked.Show(result));
    }

    [Fact]
    public void Inserting_leaves_the_text_before_the_at_sign_unchanged()
    {
        var result = MentionPicker.Insert(Marked.Field("line1\nline2 @A«»"), 12, Mention);

        Assert.Equal($"line1\nline2 {Mention}«»", Marked.Show(result));
    }

    [Fact]
    public void Inserting_replaces_the_at_sign_through_the_end_of_a_selection_after_the_typed_text()
    {
        var result = MentionPicker.Insert(Marked.Field("@A«na» rest"), 0, Mention);

        Assert.Equal($"{Mention}«» rest", Marked.Show(result));
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void Inserting_leaves_a_collapsed_selection()
    {
        var result = MentionPicker.Insert(Marked.Field("@A«»"), 0, Mention);

        Assert.Equal(0, result.SelectionLength);
        Assert.Equal(Mention.Length, result.SelectionStart);
    }

    [Theory]
    [InlineData("a@A«»", 1)]
    [InlineData("@A«»", 1)]
    [InlineData("@A«»", -1)]
    [InlineData("@A«»", 100)]
    [InlineData("ab«»@A", 2)]
    [InlineData("@«»A", 1)]
    [InlineData("«»@A", 0)]
    [InlineData("@A\nB«»", 0)]
    public void Inserting_rejects_a_list_that_would_be_closed(string marked, int atIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MentionPicker.Insert(Marked.Field(marked), atIndex, Mention));
    }

    [Fact]
    public void Inserting_rejects_an_empty_mention()
    {
        var exception = Assert.Throws<ArgumentException>(() => MentionPicker.Insert(Marked.Field("@A«»"), 0, ""));

        Assert.IsNotType<ArgumentNullException>(exception);
    }

    [Fact]
    public void Inserting_rejects_a_missing_mention()
    {
        Assert.Throws<ArgumentNullException>(() => MentionPicker.Insert(Marked.Field("@A«»"), 0, null!));
    }

    [Fact]
    public void Inserting_rejects_a_missing_field()
    {
        Assert.Throws<ArgumentNullException>(() => MentionPicker.Insert(null!, 0, Mention));
    }
}
