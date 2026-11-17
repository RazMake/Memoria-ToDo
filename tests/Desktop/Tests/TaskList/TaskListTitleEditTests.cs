using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Edit Title: the title is edited in its row; Enter or the Save shortcut saves, Esc leaves.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTitleEditTests
{
    private static TaskListScreen Editing(ListServices services, string title = "Buy milk")
    {
        var screen = new TaskListScreen(services);
        screen.PressOn(title, Key.LeftShift, Key.F2);
        return screen;
    }

    [WpfFact]
    public void The_right_click_menu_offers_Edit_Title_with_Shift_F2()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.Contains(("Edit Title", "Shift+F2"), screen.ContextMenuItems("Buy milk"));
    }

    [WpfFact]
    public void Edit_Title_shows_the_title_editor_in_the_right_clicked_row()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.ChooseContextItem("Buy milk", "Edit Title");

        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.False(screen.IsEditingTitle("Second"));
        Assert.Equal("Buy milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal("Title", screen.FocusedEditorField);
    }

    [WpfFact]
    public void Shift_F2_edits_the_title_of_the_selected_task()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Editing(services);

        Assert.True(screen.IsEditingTitle("Buy milk"));
    }

    [WpfFact]
    public void Enter_saves_the_new_title_and_leaves_the_edit_with_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = Editing(services);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.Enter);

        var saved = services.Tasks.All.Single();
        Assert.Equal("Buy oat milk", saved.Title);
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void The_Save_shortcut_saves_the_new_title()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Editing(services);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Esc_leaves_the_edit_without_saving_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Editing(services);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void An_empty_title_is_marked_and_the_edit_stays_open()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Editing(services);
        screen.TypeTitle("Buy milk", "");

        screen.Press(Key.Enter);

        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.True(screen.EditedTitleIsMarked);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Only_one_edit_is_open_at_a_time()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk");
        using var screen = Editing(services);

        screen.PressOn("Second", Key.LeftShift, Key.F2);

        Assert.False(screen.IsEditingTitle("Buy milk"));
        Assert.True(screen.IsEditingTitle("Second"));
    }

    [WpfFact]
    public void The_checkbox_is_off_while_the_title_is_edited()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = Editing(services);

        Assert.False(screen.CheckBoxIsEnabled("Buy milk"));
    }

    [WpfFact]
    public void Edit_Title_on_the_second_row_of_a_task_with_two_tags_edits_and_focuses_that_row()
    {
        var services = new ListServices();
        services.AddOlder("Buy milk", "", "home", "work");
        using var screen = new TaskListScreen(services);

        screen.ChooseContextItem("Buy milk", "Edit Title", group: "work");

        Assert.True(screen.IsEditingTitle("Buy milk", "work"));
        Assert.False(screen.IsEditingTitle("Buy milk", "home"));
        Assert.True(screen.FocusIsInRow("Buy milk", "work"));
    }

    [WpfFact]
    public void Edit_Title_on_a_resolved_task_edits_in_the_clicked_resolved_row_and_saves_its_title()
    {
        var services = new ListServices();
        services.Add("Open one");
        services.AddResolved("Done one");
        services.AddResolved("Done two");
        using var screen = new TaskListScreen(services);
        screen.Click(screen.ResolvedLabel!);

        screen.ChooseContextItem("Done two", "Edit Title");

        Assert.True(screen.IsEditingTitle("Done two"));
        Assert.False(screen.IsEditingTitle("Done one"));
        Assert.False(screen.IsEditingTitle("Open one"));
        Assert.Equal("Title", screen.FocusedEditorField);
        screen.TypeTitle("Done two", "Done three");
        screen.Press(Key.Enter);
        Assert.Contains(services.Tasks.All, task => task.Title == "Done three" && task.Resolved is not null);
    }
}
