using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Ctrl+Shift+C opens a list of the palette's colors; choosing one runs Editing's Color command. A test finds the list as
/// the element whose automation id is "ColorList", whose items are the color names, each with a button, as the tag suggestions are.
/// </summary>
[UX]
public class AddWindowColorListTests
{
    private static readonly string[] PaletteNames = ["red", "orange", "green", "blue", "purple", "gray"];

    [WpfTheory]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.Title)]
    public void Ctrl_Shift_C_opens_a_list_of_the_palette_colors(string field)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(field, "urgent", 0, 6);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        Assert.Equal(PaletteNames, screen.ShownColors);
    }

    [WpfFact]
    public void The_color_list_is_closed_until_asked_for()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.Empty(screen.ShownColors);
    }

    [WpfFact]
    public void Opening_the_color_list_changes_no_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        Assert.Equal("urgent", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.Title)]
    public void Choosing_a_color_wraps_the_selection_in_it(string field)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(field, "do urgent now", 3, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Equal("do :red[urgent] now", screen.TextOf(field));
    }

    [WpfTheory]
    [InlineData("orange")]
    [InlineData("green")]
    [InlineData("blue")]
    [InlineData("purple")]
    [InlineData("gray")]
    public void Choosing_any_palette_color_uses_its_name(string color)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor(color);

        Assert.Equal($":{color}[urgent]", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_color_keeps_the_colored_text_selected()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Equal((5, 6), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_another_color_replaces_the_text_s_own_color()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, ":red[urgent]", 5, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("blue");

        Assert.Equal(":blue[urgent]", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_the_text_s_own_color_removes_it()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, ":red[urgent]", 5, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Equal("urgent", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_color_closes_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Empty(screen.ShownColors);
    }

    [WpfTheory]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.Title)]
    public void Choosing_a_color_puts_the_cursor_back_in_the_field(string field)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(field, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Equal(field, screen.FocusedField);
    }

    [WpfFact]
    public void Choosing_a_color_does_not_save_or_close_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.True(screen.IsShowing);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void One_undo_reverts_a_chosen_color()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        screen.ClickColor("red");

        screen.Undo(EditorFields.Description);

        Assert.Equal("urgent", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Esc_closes_the_color_list_first_and_leaves_the_window_open_and_the_text_alone()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownColors);
        Assert.True(screen.IsShowing);
        Assert.Equal("urgent", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_second_Esc_closes_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        screen.Press(Key.Escape);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Opening_the_color_list_again_after_Esc_shows_the_colors_again()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        screen.Press(Key.Escape);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        Assert.Equal(PaletteNames, screen.ShownColors);
    }
}
