using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>A formatting key in the Add window, as a user presses it. Which key runs which command is in the unit tests of the formatting keys.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowFormattingKeysTests
{
    [WpfFact]
    public void Ctrl_B_wraps_the_selection_in_bold_markers_and_keeps_the_cursor_in_the_field()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a b c", 2, 1);

        screen.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a **b** c", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.True(screen.IsShowing);
    }
}
