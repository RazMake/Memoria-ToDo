using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Typing "/" in the Add window's Description opens the snippet list, on the common path. A test finds the list by the
/// automation id "SnippetList". When "/" opens it, what matches, and what choosing does are in the unit tests of the snippet picking.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowSnippetPickerTests
{
    [WpfFact]
    public void Typing_a_slash_in_the_Description_lists_the_snippets_and_leaves_the_slash_in_the_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("/");

        Assert.Equal(
            ["Date stamp", "Date and time stamp", "Note", "Tip", "Warning", "Link", "Image", "Code block", "Task reference", "Table", "Horizontal line"],
            screen.ShownSnippets);
        Assert.Equal("/", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Typing_a_slash_in_the_Title_does_not_open_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "", 0);

        screen.Type("/");

        Assert.Empty(screen.ShownSnippets);
    }

    [WpfFact]
    public void Down_then_Enter_replaces_the_slash_and_what_was_typed_with_the_snippet()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/no");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("> [!NOTE]\n> ", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Clicking_a_snippet_inserts_it_where_the_slash_was_typed_and_closes_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a ", 2);
        screen.Type("/ti");

        screen.ClickSnippet("Tip");

        Assert.Equal("a \n> [!TIP]\n> ", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownSnippets);
    }

    [WpfFact]
    public void Esc_closes_the_list_first_and_a_second_Esc_closes_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/ti");

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownSnippets);
        Assert.True(screen.IsShowing);
        Assert.Equal("/ti", screen.TextOf(EditorFields.Description));

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }
}
