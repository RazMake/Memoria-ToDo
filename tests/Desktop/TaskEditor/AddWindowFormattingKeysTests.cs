using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The keys that run Editing's formatting commands in the Title and the Description. Editing's own tests cover what each
/// command does to text; these cover that the right key runs the right command on the focused field's text and selection.
/// </summary>
[UX]
public class AddWindowFormattingKeysTests
{
    [WpfTheory]
    [InlineData(EditorFields.Description, Key.B, false, "a **b** c")]
    [InlineData(EditorFields.Description, Key.I, false, "a *b* c")]
    [InlineData(EditorFields.Description, Key.X, true, "a ~~b~~ c")]
    [InlineData(EditorFields.Description, Key.E, false, "a `b` c")]
    [InlineData(EditorFields.Title, Key.B, false, "a **b** c")]
    [InlineData(EditorFields.Title, Key.I, false, "a *b* c")]
    [InlineData(EditorFields.Title, Key.X, true, "a ~~b~~ c")]
    [InlineData(EditorFields.Title, Key.E, false, "a `b` c")]
    public void The_formatting_key_wraps_the_selection_in_its_markers(string field, Key key, bool withShift, string expected)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(field, "a b c", 2, 1);

        screen.PressCtrl(withShift, key);

        Assert.Equal(expected, screen.TextOf(field));
    }

    [WpfFact]
    public void The_formatting_key_keeps_the_wrapped_text_selected()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a b c", 2, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal((4, 1), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void The_formatting_key_with_no_selection_inserts_both_markers_with_the_cursor_between_them()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "ab", 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a****b", screen.TextOf(EditorFields.Description));
        Assert.Equal((3, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void The_formatting_key_on_already_wrapped_text_removes_the_markers()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a **b** c", 4, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a b c", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void The_formatting_key_changes_only_the_field_that_has_the_cursor()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("a b c", "a b c");
        screen.Edit(EditorFields.Description, "a b c", 2, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a b c", screen.TextOf(EditorFields.Title));
    }

    [WpfFact]
    public void The_formatting_key_keeps_the_cursor_in_the_field()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a b c", 2, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void The_formatting_key_does_not_save_or_close_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "a b c", 2, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.True(screen.IsShowing);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void One_undo_reverts_the_formatting_key()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a b c", 2, 1);
        screen.Press(Key.LeftCtrl, Key.B);

        screen.Undo(EditorFields.Description);

        Assert.Equal("a b c", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(Key.D1, "# Plan")]
    [InlineData(Key.D2, "## Plan")]
    [InlineData(Key.D3, "### Plan")]
    public void Ctrl_and_a_digit_sets_the_heading_level_of_the_Description_line(Key digit, string expected)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "Plan", 2);

        screen.Press(Key.LeftCtrl, digit);

        Assert.Equal(expected, screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_1_on_a_level_1_heading_removes_the_heading()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "# Plan", 4);

        screen.Press(Key.LeftCtrl, Key.D1);

        Assert.Equal("Plan", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_2_on_a_level_1_heading_replaces_it_with_level_2()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "# Plan", 4);

        screen.Press(Key.LeftCtrl, Key.D2);

        Assert.Equal("## Plan", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_heading_key_changes_only_the_line_with_the_cursor()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "One\nTwo", 5);

        screen.Press(Key.LeftCtrl, Key.D1);

        Assert.Equal("One\n# Two", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(Key.D1)]
    [InlineData(Key.D2)]
    [InlineData(Key.D3)]
    public void Heading_keys_do_nothing_in_the_Title(Key digit)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "Plan", 2);

        screen.Press(Key.LeftCtrl, digit);

        Assert.Equal("Plan", screen.TextOf(EditorFields.Title));
        Assert.Equal((2, 0), screen.SelectionOf(EditorFields.Title));
    }

    [WpfFact]
    public void One_undo_reverts_a_heading_key()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "Plan", 2);
        screen.Press(Key.LeftCtrl, Key.D1);

        screen.Undo(EditorFields.Description);

        Assert.Equal("Plan", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(Key.B, false)]
    [InlineData(Key.I, false)]
    [InlineData(Key.X, true)]
    [InlineData(Key.E, false)]
    [InlineData(Key.D1, false)]
    [InlineData(Key.D2, false)]
    [InlineData(Key.D3, false)]
    [InlineData(Key.C, true)]
    public void No_formatting_key_works_with_Ctrl_and_Alt_held(Key key, bool withShift)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a b c", 2, 1);

        Key[] modifiers = withShift ? [Key.LeftCtrl, Key.LeftAlt, Key.LeftShift] : [Key.LeftCtrl, Key.LeftAlt];
        screen.Press([.. modifiers, key]);

        Assert.Equal("a b c", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownColors);
    }
}
