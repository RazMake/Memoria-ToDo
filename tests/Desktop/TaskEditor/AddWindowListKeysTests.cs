using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The keys that continue and re-indent lists in the Description. Editing's own tests cover the list rules; these cover
/// that Enter, Tab, and Shift+Tab run them in a list item, and keep their usual meaning anywhere else. Texts use "\n"
/// line breaks.
/// </summary>
[UX]
public class AddWindowListKeysTests
{
    [WpfFact]
    public void Enter_at_the_end_of_a_bullet_item_starts_the_next_item()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a", 3);

        screen.Press(Key.Enter);

        Assert.Equal("- a\n- ", screen.TextOf(EditorFields.Description));
        Assert.Equal((6, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Enter_in_a_numbered_item_counts_on_from_it()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "1. a", 4);

        screen.Press(Key.Enter);

        Assert.Equal("1. a\n2. ", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Enter_on_an_empty_item_ends_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a\n- ", 6);

        screen.Press(Key.Enter);

        Assert.Equal("- a\n", screen.TextOf(EditorFields.Description));
        Assert.Equal((4, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Enter_outside_a_list_breaks_the_line_as_usual()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "ab", 1);

        screen.Press(Key.Enter);

        Assert.Equal("a\nb", Normalized(screen.TextOf(EditorFields.Description)));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Enter_before_the_marker_of_an_item_breaks_the_line_as_usual()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a", 0);

        screen.Press(Key.Enter);

        Assert.Equal("\n- a", Normalized(screen.TextOf(EditorFields.Description)));
    }

    [WpfFact]
    public void Enter_in_a_list_does_not_save_or_leave_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title");
        screen.Edit(EditorFields.Description, "- a", 3);

        screen.Press(Key.Enter);

        Assert.Empty(screen.Saved);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Tab_in_a_list_item_indents_it_under_the_item_above()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a\n- b", 7);

        screen.Press(Key.Tab);

        Assert.Equal("- a\n  - b", screen.TextOf(EditorFields.Description));
        Assert.Equal((9, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Tab_in_a_list_item_keeps_the_cursor_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a\n- b", 7);

        screen.Press(Key.Tab);

        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Tab_on_the_first_item_changes_nothing_and_keeps_the_cursor_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a", 3);

        screen.Press(Key.Tab);

        Assert.Equal("- a", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Shift_Tab_in_a_nested_item_moves_it_back_out()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a\n  - b", 9);

        screen.Press(Key.LeftShift, Key.Tab);

        Assert.Equal("- a\n- b", screen.TextOf(EditorFields.Description));
        Assert.Equal((7, 0), screen.SelectionOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Tab_outside_a_list_moves_the_cursor_to_the_Tags_and_inserts_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "plain", 5);

        screen.Press(Key.Tab);

        Assert.Equal("plain", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Tags, screen.FocusedField);
    }

    [WpfFact]
    public void The_list_keys_do_nothing_special_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "- a", 3);

        screen.Press(Key.Enter);

        Assert.Equal("- a", screen.TextOf(EditorFields.Title));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void One_undo_reverts_Enter_in_a_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a", 3);
        screen.Press(Key.Enter);

        screen.Undo(EditorFields.Description);

        Assert.Equal("- a", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void One_undo_reverts_Tab_in_a_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a\n- b", 7);
        screen.Press(Key.Tab);

        screen.Undo(EditorFields.Description);

        Assert.Equal("- a\n- b", screen.TextOf(EditorFields.Description));
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n");
}
