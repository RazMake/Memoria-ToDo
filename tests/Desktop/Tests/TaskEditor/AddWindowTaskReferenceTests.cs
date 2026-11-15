using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Ctrl+Shift+R lists the open tasks, as a list a test finds by the automation id "TaskReferenceList"; choosing one
/// inserts a link to it. The listing, the narrowing, and the link are in the unit tests of the task-reference picking.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTaskReferenceTests
{
    [WpfFact]
    public void Ctrl_Shift_R_lists_the_open_tasks_newest_first()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("First");
        screen.Services.AddTaskTitled("Second");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.PressCtrl(true, Key.R);

        Assert.Equal(["Second", "First"], screen.ShownTaskReferences);
    }

    [WpfFact]
    public void Clicking_a_task_inserts_a_link_to_it_at_the_cursor_and_closes_the_list()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "see  now", 4);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"see [Buy milk](task:{task.Id}) now", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownTaskReferences);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Typing_a_filter_then_Down_and_Enter_replaces_the_typed_query_with_the_link()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Services.AddTaskTitled("Call mom");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);
        screen.Type("mil");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal($"[Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Esc_closes_the_task_list_first_and_a_second_Esc_closes_the_window()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownTaskReferences);
        Assert.True(screen.IsShowing);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }
}
