using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Ctrl+Shift+C and the color list in the Add window: the common path. The color logic is in the unit tests of the color picking.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowColorListTests
{
    [WpfFact]
    public void Ctrl_Shift_C_opens_a_list_of_the_palette_colors()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "urgent", 0, 6);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        Assert.Equal(["red", "orange", "green", "blue", "purple", "gray"], screen.ShownColors);
    }

    [WpfFact]
    public void Choosing_a_color_wraps_the_selection_in_it_and_puts_the_cursor_back_in_the_field()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "do urgent now", 3, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);

        screen.ClickColor("red");

        Assert.Equal("do :red[urgent] now", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Empty(screen.ShownColors);
    }
}
