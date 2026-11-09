using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class FormattingTests
{
    private static readonly string[] Palette = ["red", "orange", "green", "blue", "purple", "gray"];

    public static TheoryData<Func<TextAndSelection, TextAndSelection>, string> Wrappers => new()
    {
        { Formatting.Bold, "**" },
        { Formatting.Italic, "*" },
        { Formatting.Strikethrough, "~~" },
        { Formatting.InlineCode, "`" },
    };

    private static string Apply(Func<TextAndSelection, TextAndSelection> command, string marked) =>
        Marked.Show(command(Marked.Field(marked)));

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void A_wrapping_command_puts_its_markers_around_the_selection(Func<TextAndSelection, TextAndSelection> command, string marker)
    {
        Assert.Equal($"say {marker}«hello»{marker} now", Apply(command, "say «hello» now"));
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void A_wrapping_command_with_no_selection_inserts_both_markers_with_the_cursor_between(Func<TextAndSelection, TextAndSelection> command, string marker)
    {
        Assert.Equal($"say {marker}«»{marker} now", Apply(command, "say «» now"));
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void A_wrapping_command_removes_its_markers_from_text_that_already_has_them(Func<TextAndSelection, TextAndSelection> command, string marker)
    {
        Assert.Equal("say «hello» now", Apply(command, $"say {marker}«hello»{marker} now"));
    }

    [Fact]
    public void Bold_wraps_the_whole_text_when_all_of_it_is_selected()
    {
        Assert.Equal("**«all of it»**", Apply(Formatting.Bold, "«all of it»"));
    }

    [Fact]
    public void Color_wraps_the_selection_in_the_chosen_colors_markers()
    {
        var result = Formatting.Color(Marked.Field("an «urgent» job"), "red", Palette);

        Assert.Equal("an :red[«urgent»] job", Marked.Show(result));
    }

    [Fact]
    public void Color_with_no_selection_inserts_both_markers_with_the_cursor_between()
    {
        var result = Formatting.Color(Marked.Field("an «» job"), "green", Palette);

        Assert.Equal("an :green[«»] job", Marked.Show(result));
    }

    [Fact]
    public void Color_with_the_texts_own_color_removes_the_color()
    {
        var result = Formatting.Color(Marked.Field("an :red[«urgent»] job"), "red", Palette);

        Assert.Equal("an «urgent» job", Marked.Show(result));
    }

    [Fact]
    public void Color_with_another_color_replaces_the_texts_color()
    {
        var result = Formatting.Color(Marked.Field(":red[«urgent»]"), "blue", Palette);

        Assert.Equal(":blue[«urgent»]", Marked.Show(result));
    }

    [Fact]
    public void Color_rejects_a_name_that_is_not_in_the_palette()
    {
        Assert.Throws<ArgumentException>(() => Formatting.Color(Marked.Field("«urgent»"), "pink", Palette));
    }

    [Fact]
    public void Color_rejects_a_name_that_differs_from_a_palette_name_only_in_case()
    {
        Assert.Throws<ArgumentException>(() => Formatting.Color(Marked.Field("«urgent»"), "Red", Palette));
    }

    [Theory]
    [InlineData(1, "# ")]
    [InlineData(2, "## ")]
    [InlineData(3, "### ")]
    public void Heading_puts_its_hashes_and_a_space_at_the_start_of_the_line(int level, string prefix)
    {
        var result = Formatting.Heading(Marked.Field("buy «milk»"), level);

        Assert.Equal($"{prefix}buy «milk»", Marked.Show(result));
    }

    [Fact]
    public void Heading_changes_only_the_line_the_selection_is_on()
    {
        var result = Formatting.Heading(Marked.Field("first\nsec«ond»\nthird"), 2);

        Assert.Equal("first\n## sec«ond»\nthird", Marked.Show(result));
    }

    [Fact]
    public void Heading_with_a_cursor_in_empty_text_makes_an_empty_heading()
    {
        var result = Formatting.Heading(Marked.Field("«»"), 1);

        Assert.Equal("# «»", Marked.Show(result));
    }

    [Fact]
    public void Heading_of_the_same_level_again_removes_the_heading()
    {
        var result = Formatting.Heading(Marked.Field("## «milk»"), 2);

        Assert.Equal("«milk»", Marked.Show(result));
    }

    [Fact]
    public void Heading_of_another_level_replaces_the_heading()
    {
        var result = Formatting.Heading(Marked.Field("# «milk»"), 3);

        Assert.Equal("### «milk»", Marked.Show(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Heading_rejects_a_level_outside_1_to_3(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Formatting.Heading(Marked.Field("«milk»"), level));
    }

    [Fact]
    public void A_command_gives_the_same_result_every_time_for_the_same_field()
    {
        var field = Marked.Field("say «hello»");

        Assert.Equal(Formatting.Bold(field), Formatting.Bold(field));
        Assert.Equal("say «hello»", Marked.Show(field));
    }
}
