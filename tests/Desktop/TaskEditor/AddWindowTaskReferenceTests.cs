using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Ctrl+Shift+R in the Description or Title lists the open tasks, newest first, as a list a test finds by the
/// automation id "TaskReferenceList"; its items are the task titles, each with a button, as the color list is.
/// Choosing one inserts a link to it, "[title](task:id)". Tasks are added before the window opens.
/// </summary>
[UX]
public class AddWindowTaskReferenceTests
{
    [WpfFact]
    public void The_task_list_is_closed_until_it_is_asked_for()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Buy milk");
        screen.Show();

        Assert.Empty(screen.ShownTaskReferences);
    }

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
    public void The_task_list_leaves_out_resolved_tasks()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Open one");
        screen.Services.AddResolvedTask("Done one");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.PressCtrl(true, Key.R);

        Assert.Equal(["Open one"], screen.ShownTaskReferences);
    }

    [WpfFact]
    public void The_task_list_shows_a_title_without_its_Markdown()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Buy **milk**");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.PressCtrl(true, Key.R);

        Assert.Equal(["Buy milk"], screen.ShownTaskReferences);
    }

    [WpfFact]
    public void Clicking_a_task_inserts_a_link_to_it_at_the_cursor()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "see  now", 4);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"see [Buy milk](task:{task.Id}) now", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Down_and_Enter_insert_the_second_task()
    {
        using var screen = new AddScreen();
        var first = screen.Services.AddTaskTitled("First");
        screen.Services.AddTaskTitled("Second");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.Press(Key.Down);
        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal($"[First](task:{first.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_task_after_typing_a_filter_replaces_the_typed_query_with_the_link()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "see ", 4);
        screen.PressCtrl(true, Key.R);
        screen.Type("mil");
        Assert.Equal(["Buy milk"], screen.ShownTaskReferences);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"see [Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_task_with_Enter_after_typing_a_filter_replaces_the_typed_query_with_the_link()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);
        screen.Type("mil");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal($"[Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Brackets_in_a_title_are_escaped_in_the_link()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Fix [urgent] bug");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Fix [urgent] bug");

        Assert.Equal($"[Fix \\[urgent\\] bug](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_title_with_Markdown_makes_a_link_whose_text_keeps_that_Markdown()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("*urgent* milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("urgent milk");

        Assert.Equal($"[*urgent* milk](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_title_with_bold_Markdown_keeps_its_marks_in_the_link_text()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy **milk**");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"[Buy **milk**](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void An_unbalanced_bracket_in_a_titles_Markdown_is_escaped_so_the_link_stays_whole()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("*Fix* [urgent bug");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Fix [urgent bug");

        Assert.Equal($"[*Fix* \\[urgent bug](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_Shift_R_works_in_the_Title_too()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Title, "After ", 6);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"After [Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Title));
    }

    [WpfFact]
    public void Choosing_a_task_closes_the_list_and_keeps_the_cursor_in_the_field_without_saving_or_closing()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Buy milk");
        var before = screen.Saved.Count;
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Buy milk");

        Assert.Empty(screen.ShownTaskReferences);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.True(screen.IsShowing);
        Assert.Equal(before, screen.Saved.Count);
    }

    [WpfFact]
    public void One_undo_takes_the_link_back_out()
    {
        using var screen = new AddScreen();
        screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "see ", 4);
        screen.PressCtrl(true, Key.R);
        screen.ClickTaskReference("Buy milk");

        screen.Undo(EditorFields.Description);

        Assert.Equal("see ", screen.TextOf(EditorFields.Description));
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
