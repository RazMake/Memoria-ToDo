using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Moving between the Add window's fields and the Description's height, on the common path.</summary>
[UX]
[Collection(ScreenTests.Collection)]
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
    public void Tab_in_the_Description_moves_the_cursor_to_the_Tags()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Description);

        screen.Press(Key.Tab);

        Assert.Equal(EditorFields.TagPill, screen.FocusedField);
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
    }
}
