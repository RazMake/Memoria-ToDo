using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class AddWindowFieldsTests
{
    [WpfFact]
    public void Enter_in_the_Title_moves_the_cursor_to_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");

        screen.Press(Key.Enter);

        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
    }

    [WpfFact]
    public void Tab_in_the_Title_moves_the_cursor_to_the_Description_and_inserts_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");

        screen.Press(Key.Tab);

        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
    }

    [WpfFact]
    public void Tab_in_the_Description_moves_the_cursor_to_the_Tags()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Description);

        screen.Press(Key.Tab);

        Assert.Equal(EditorFields.Tags, screen.FocusedField);
    }

    [WpfFact]
    public void The_Description_grows_taller_as_its_content_gets_more_lines()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title", "one line");
        var few = screen.DescriptionHeight;

        screen.Fill("Title", string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"line {i}")));

        Assert.True(screen.DescriptionHeight > few, "The Description did not grow with its content.");
        Assert.True(screen.DescriptionHeight > 140, "The Description stayed at the old fixed height.");
    }

    [WpfFact]
    public void The_Description_shrinks_again_when_its_content_gets_shorter()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title", string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"line {i}")));
        var many = screen.DescriptionHeight;

        screen.Fill("Title", "one line");

        Assert.True(screen.DescriptionHeight < many);
    }

    [WpfFact]
    public void The_fields_hold_what_was_set_in_the_editor()
    {
        using var screen = new AddScreen();
        screen.Show();

        screen.Fill("Title", "Some description", "home", "urgent");

        var value = screen.Editor.Value;
        Assert.Equal("Title", value.Title);
        Assert.Equal("Some description", value.Description);
        Assert.Equal(["home", "urgent"], value.Tags);
    }
}
