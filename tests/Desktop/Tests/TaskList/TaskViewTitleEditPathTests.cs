using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 5, editing a title in its row: Edit Title in the right-click menu opens the editor in that row, Esc leaves it with no Undo
/// bar and no save, an empty title is marked and kept open, and Enter saves the new title.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewTitleEditPathTests
{
    [WpfFact]
    public void Editing_a_title_leaves_with_Esc_marks_an_empty_title_and_saves_with_Enter()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);

        screen.ChooseContextItem("Buy milk", "Edit Title");
        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.False(screen.IsEditingTitle("Second"));
        Assert.Equal("Title", screen.FocusedEditorField);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Escape);
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal("Buy milk", services.Stored("Buy milk").Title);

        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        Assert.True(screen.IsEditingTitle("Buy milk"));
        screen.TypeTitle("Buy milk", "");
        screen.Press(Key.Enter);
        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.True(screen.EditedTitleIsMarked);
        Assert.Equal("Buy milk", services.Stored("Buy milk").Title);

        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Enter);

        var saved = services.Stored("Buy oat milk");
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }
}
