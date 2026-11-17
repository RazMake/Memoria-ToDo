using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>The list keys in the Add window's Description, on the common path. The rules are in the unit tests of the list keys.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowListKeysTests
{
    [WpfFact]
    public void Enter_at_the_end_of_a_bullet_item_starts_the_next_item_and_stays_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "- a", 3);

        screen.Press(Key.Enter);

        Assert.Equal("- a\n- ", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void Tab_outside_a_list_moves_the_cursor_to_the_Tags_and_inserts_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "plain", 5);

        screen.Press(Key.Tab);

        Assert.Equal("plain", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.TagPill, screen.FocusedField);
    }
}
