using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class ListsTests
{
    private static string? Show(TextAndSelection? field) => field is null ? null : Marked.Show(field);

    [Theory]
    [InlineData("- ")]
    [InlineData("* ")]
    [InlineData("+ ")]
    public void Enter_in_a_bulleted_item_starts_the_next_item_with_the_same_marker(string marker)
    {
        var result = Lists.Enter(Marked.Field($"{marker}buy milk«»"));

        Assert.Equal($"{marker}buy milk\n{marker}«»", Show(result));
    }

    [Fact]
    public void Enter_keeps_the_indent_of_the_item()
    {
        var result = Lists.Enter(Marked.Field("- a\n  - nested«»"));

        Assert.Equal("- a\n  - nested\n  - «»", Show(result));
    }

    [Fact]
    public void Enter_in_a_numbered_item_counts_on_from_the_item_above()
    {
        var result = Lists.Enter(Marked.Field("1. one\n2. two«»"));

        Assert.Equal("1. one\n2. two\n3. «»", Show(result));
    }

    [Fact]
    public void Enter_counts_on_past_a_digit_boundary()
    {
        var result = Lists.Enter(Marked.Field("9. nine«»"));

        Assert.Equal("9. nine\n10. «»", Show(result));
    }

    [Fact]
    public void Enter_on_an_empty_bulleted_item_removes_its_marker_and_ends_the_list()
    {
        var result = Lists.Enter(Marked.Field("- a\n- «»"));

        Assert.Equal("- a\n«»", Show(result));
    }

    [Fact]
    public void Enter_on_an_empty_numbered_item_removes_its_marker_and_ends_the_list()
    {
        var result = Lists.Enter(Marked.Field("1. a\n2. «»"));

        Assert.Equal("1. a\n«»", Show(result));
    }

    [Fact]
    public void Enter_leaves_the_lines_after_the_item_untouched()
    {
        var result = Lists.Enter(Marked.Field("- a«»\nafter"));

        Assert.Equal("- a\n- «»\nafter", Show(result));
    }

    [Fact]
    public void Indent_moves_a_bulleted_item_in_by_the_width_of_the_parents_marker()
    {
        var result = Lists.Indent(Marked.Field("- a\n- b«»"));

        Assert.Equal("- a\n  - b«»", Show(result));
    }

    [Fact]
    public void Indent_moves_a_numbered_item_in_by_the_width_of_the_parents_marker()
    {
        var result = Lists.Indent(Marked.Field("1. a\n2. b«»"));

        Assert.Equal("1. a\n   2. b«»", Show(result));
    }

    [Fact]
    public void Outdent_moves_a_nested_item_one_level_out()
    {
        var result = Lists.Outdent(Marked.Field("- a\n  - b«»"));

        Assert.Equal("- a\n- b«»", Show(result));
    }

    [Fact]
    public void Outdent_undoes_an_indent()
    {
        var original = Marked.Field("- a\n- b«»");

        var result = Lists.Outdent(Lists.Indent(original)!);

        Assert.Equal(original, result);
    }

    [Fact]
    public void Enter_returns_null_outside_a_list()
    {
        Assert.Null(Lists.Enter(Marked.Field("just text«»")));
    }

    [Fact]
    public void Indent_returns_null_outside_a_list()
    {
        Assert.Null(Lists.Indent(Marked.Field("just text«»")));
    }

    [Fact]
    public void Outdent_returns_null_outside_a_list()
    {
        Assert.Null(Lists.Outdent(Marked.Field("just text«»")));
    }

    [Fact]
    public void Each_command_returns_null_when_the_cursor_is_on_a_plain_line_below_a_list()
    {
        var field = Marked.Field("- a\n\nplain«»");

        Assert.Null(Lists.Enter(field));
        Assert.Null(Lists.Indent(field));
        Assert.Null(Lists.Outdent(field));
    }

    [Fact]
    public void Each_command_returns_null_in_empty_text()
    {
        var field = Marked.Field("«»");

        Assert.Null(Lists.Enter(field));
        Assert.Null(Lists.Indent(field));
        Assert.Null(Lists.Outdent(field));
    }
}
