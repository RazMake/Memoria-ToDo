using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Two snippet keys in the Add window, as a user presses them: one that needs the time, one that needs the clipboard. The rest are in the unit tests of the snippet keys.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowSnippetKeysTests
{
    [WpfFact]
    public void Ctrl_semicolon_inserts_the_local_date_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Time.Advance(TimeSpan.FromHours(10.5));
        screen.Edit(EditorFields.Description, "on ", 3);

        screen.Press(Key.LeftCtrl, Key.OemSemicolon);

        Assert.Equal("on " + screen.LocalNow.ToString("yyyy-MM-dd"), screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Ctrl_K_makes_the_selection_the_text_of_a_link_to_the_URL_on_the_clipboard()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = "https://example.com/docs";
        screen.Edit(EditorFields.Description, "see docs now", 4, 4);

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal("see [docs](https://example.com/docs) now", screen.TextOf(EditorFields.Description));
    }
}
