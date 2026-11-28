using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Enter, Tab and Shift+Tab in the Description. What the list commands do is Editing's, tested there.</summary>
[Unit]
public class ListKeysTests
{
    private int _left;
    private int _leftBack;

    private (FakeTextField Field, KeyMap Keys) Description(string text, int cursor)
    {
        var field = new FakeTextField(text, cursor);
        var keys = new KeyMap();
        ListKeys.AddTo(keys, field, () => _left++, () => _leftBack++);
        return (field, keys);
    }

    [Fact]
    public void Enter_at_the_end_of_a_bullet_item_starts_the_next_item_and_is_handled()
    {
        var (field, keys) = Description("- a", 3);

        Assert.True(keys.TryRun(ModifierKeys.None, Key.Enter));

        Assert.Equal("- a\n- ", field.Text);
        Assert.Equal(6, field.CaretOffset);
    }

    [Fact]
    public void Enter_outside_a_list_is_not_handled_so_it_breaks_the_line_as_usual()
    {
        var (field, keys) = Description("ab", 1);

        Assert.False(keys.TryRun(ModifierKeys.None, Key.Enter));

        Assert.Equal("ab", field.Text);
    }

    [Fact]
    public void Tab_in_a_list_item_indents_it_under_the_item_above_and_does_not_leave_the_field()
    {
        var (field, keys) = Description("- a\n- b", 7);

        Assert.True(keys.TryRun(ModifierKeys.None, Key.Tab));

        Assert.Equal("- a\n  - b", field.Text);
        Assert.Equal(9, field.CaretOffset);
        Assert.Equal(0, _left);
    }

    [Fact]
    public void Tab_on_the_first_item_changes_nothing_and_does_not_leave_the_field()
    {
        var (field, keys) = Description("- a", 3);

        Assert.True(keys.TryRun(ModifierKeys.None, Key.Tab));

        Assert.Equal("- a", field.Text);
        Assert.Equal(0, _left);
    }

    [Fact]
    public void Tab_outside_a_list_leaves_the_field_forward_and_inserts_nothing()
    {
        var (field, keys) = Description("plain", 5);

        Assert.True(keys.TryRun(ModifierKeys.None, Key.Tab));

        Assert.Equal("plain", field.Text);
        Assert.Equal((1, 0), (_left, _leftBack));
    }

    [Fact]
    public void Shift_Tab_in_a_nested_item_moves_it_back_out_and_does_not_leave_the_field()
    {
        var (field, keys) = Description("- a\n  - b", 9);

        Assert.True(keys.TryRun(ModifierKeys.Shift, Key.Tab));

        Assert.Equal("- a\n- b", field.Text);
        Assert.Equal((0, 0), (_left, _leftBack));
    }

    [Fact]
    public void Shift_Tab_outside_a_list_leaves_the_field_back()
    {
        var (_, keys) = Description("plain", 5);

        Assert.True(keys.TryRun(ModifierKeys.Shift, Key.Tab));

        Assert.Equal((0, 1), (_left, _leftBack));
    }

    [Fact]
    public void One_undo_reverts_Enter_in_a_list()
    {
        var (field, keys) = Description("- a", 3);
        keys.TryRun(ModifierKeys.None, Key.Enter);

        field.Undo();

        Assert.Equal("- a", field.Text);
    }
}
