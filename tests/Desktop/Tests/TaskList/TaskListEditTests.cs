using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListEditTests
{
    [WpfFact]
    public void F2_turns_the_open_task_into_the_editor_in_its_place_with_its_text()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.PressOn("Buy milk", Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal(["Buy milk", "Second"], screen.RowTitles);
    }

    [WpfFact]
    public void Ctrl_Enter_saves_the_edit_and_leaves_it_with_the_task_open_and_formatted()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy oat milk"], screen.ExpandedTitles);
        Assert.Contains("Oat milk", screen.Web.Page);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Esc_on_a_changed_edit_discards_it_and_shows_the_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Undo_reopens_the_discarded_edit_with_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Escape);

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

}
