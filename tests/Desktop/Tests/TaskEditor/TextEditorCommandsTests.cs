using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Running Editing's commands on a text field's text and selection.</summary>
[Unit]
public class TextEditorCommandsTests
{
    [Fact]
    public void A_command_gets_the_text_and_selection_of_the_field()
    {
        var field = new FakeTextField("hello world", 6, 5);
        TextAndSelection? seen = null;

        field.Run(text =>
        {
            seen = text;
            return null;
        });

        Assert.Equal(new TextAndSelection("hello world", 6, 5), seen);
    }

    [Fact]
    public void Line_breaks_reach_the_command_as_LF_with_the_selection_moved_to_match()
    {
        var field = new FakeTextField("a\r\nb\r\ncd", 6, 1);
        TextAndSelection? seen = null;

        field.Run(text =>
        {
            seen = text;
            return null;
        });

        Assert.Equal(new TextAndSelection("a\nb\ncd", 4, 1), seen);
    }

    [Fact]
    public void The_result_replaces_the_text_and_becomes_the_selection()
    {
        var field = new FakeTextField("a b c", 2, 1);

        var applied = field.Run(text => new TextAndSelection("a **b** c", 4, 1));

        Assert.True(applied);
        Assert.Equal("a **b** c", field.Text);
        Assert.Equal((4, 1), (field.SelectionStart, field.SelectionLength));
    }

    [Fact]
    public void A_command_that_does_not_apply_changes_nothing_and_says_so()
    {
        var field = new FakeTextField("a b c", 2, 1);

        var applied = field.Run(_ => null);

        Assert.False(applied);
        Assert.Equal("a b c", field.Text);
        Assert.Equal((2, 1), (field.SelectionStart, field.SelectionLength));
    }

    [Fact]
    public void A_result_in_a_text_with_line_breaks_gives_the_text_the_command_returned()
    {
        var field = new FakeTextField("one\r\ntwo", 5, 0);

        field.Run(text => new TextAndSelection("one\n# two", 7, 0));

        Assert.Equal("one\n# two", field.Text);
        Assert.Equal(7, field.CaretOffset);
    }

    [Fact]
    public void One_undo_reverts_the_whole_command()
    {
        var field = new FakeTextField("a b c", 2, 1);
        field.Run(text => new TextAndSelection("a **b** c", 4, 1));

        field.Undo();

        Assert.Equal("a b c", field.Text);
    }

    [Fact]
    public void A_result_equal_to_the_text_only_moves_the_selection()
    {
        var field = new FakeTextField("abc", 0, 0);

        field.Run(text => new TextAndSelection(text.Text, 1, 2));

        Assert.Equal("abc", field.Text);
        Assert.Equal((1, 2), (field.SelectionStart, field.SelectionLength));
    }

    [Fact]
    public void An_offset_in_the_field_converts_to_its_position_in_the_LF_text()
    {
        var field = new FakeTextField("a\r\nbc");

        Assert.Equal(3, field.LfOffset(4));
    }
}
