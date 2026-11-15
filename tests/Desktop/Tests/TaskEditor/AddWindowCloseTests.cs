using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>What <see cref="IAddWindow.Close"/> does, as Shell uses it: it hides Add and keeps the draft, as Esc does.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowCloseTests
{
    [WpfFact]
    public void Close_hides_an_open_window_and_keeps_the_draft_for_the_next_Show()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("hom");

        screen.Add.Close();

        Assert.False(screen.Add.IsOpen);
        Assert.False(screen.IsShowing);
        var kept = screen.KeptDraft;
        Assert.NotNull(kept);
        Assert.Equal("Buy milk", kept.Title);
        Assert.Equal("Two litres", kept.Description);
        Assert.Equal(["home"], kept.Tags);

        screen.Show();

        Assert.True(screen.Add.IsOpen);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Equal("Two litres", screen.Editor.Value.Description);
        Assert.Equal(["home"], screen.Editor.Value.Tags);
        Assert.Equal("hom", screen.TypedTag);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Close_on_a_window_that_was_never_shown_does_nothing()
    {
        using var screen = new AddScreen();

        screen.Add.Close();

        Assert.False(screen.Add.IsOpen);
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Close_on_a_hidden_window_writes_no_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        var id = screen.Editor.Value.TaskId;
        screen.PressEscape();
        var newer = new TaskDraft(id, "Kept elsewhere", "", []);
        screen.Services.Tasks.KeepDraft(newer);

        screen.Add.Close();

        Assert.False(screen.Add.IsOpen);
        Assert.Equal("Kept elsewhere", screen.KeptDraft?.Title);
    }
}
