using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Esc in Task View goes back one step, then closes the window: it closes an open list, then leaves an edit, then closes
/// Task View. The rule for which edits show the Undo bar is unit-tested in EditCleanupTests, and Esc on a changed edit with
/// its Undo is in TaskListEditTests. Task View has the Description editor's snippet list and the tag control's + menu and its Custom box;
/// Esc with the menu or Custom open is in TaskListTagEscTests. Esc from the real
/// description web view is an integration test, ViewTasksEscWithRealWebViewTests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListEscTests
{
    [WpfFact]
    public void Esc_with_the_snippet_list_open_closes_only_the_list()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeInDescription("/");
        Assert.NotEmpty(screen.ShownSnippets);

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownSnippets);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_after_the_list_closed_closes_Task_View()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeInDescription("/");
        screen.Press(Key.Escape);
        screen.Press(Key.Escape);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_a_changed_edit_and_the_focus_in_Search_leaves_the_edit_with_its_Undo_bar_and_keeps_the_window()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        screen.FocusSearchBox();
        Assert.True(screen.SearchHasFocus);

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_a_changed_edit_and_the_focus_on_the_Sync_button_leaves_the_edit_with_its_Undo_bar_and_keeps_the_window()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        screen.FocusSyncButton();

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void A_second_Esc_after_leaving_a_changed_edit_from_Search_closes_Task_View()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        screen.FocusSearchBox();
        screen.Press(Key.Escape);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_the_snippet_list_open_and_the_focus_in_Search_closes_only_the_list()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeInDescription("/");
        Assert.NotEmpty(screen.ShownSnippets);
        screen.FocusSearchBox();

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownSnippets);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_in_an_edit_without_changes_leaves_it_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Closing_from_an_edit_with_changes_takes_two_presses_and_shows_no_prompt()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        screen.Press(Key.Escape);
        Assert.True(screen.View.IsOpen);

        var prompt = new ClosePrompt();
        prompt.Run(() =>
        {
            screen.PressToClose(Key.Escape);
            return screen.View.IsOpen;
        }, ClosePrompt.CloseIt);

        Assert.False(screen.View.IsOpen);
        Assert.Equal(0, prompt.Shown);
    }

    [WpfFact]
    public void Esc_with_nothing_open_closes_Task_View()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_closes_Task_View_so_a_pending_delete_finishes()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);
        Assert.True(screen.HasButton("Undo"));

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
        Assert.Empty(services.Tasks.All);
    }

    [WpfFact]
    public void Esc_in_the_search_box_closes_Task_View_instead_of_clearing_the_search()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.Press(Key.LeftCtrl, Key.F);
        screen.Search("milk");
        Assert.True(screen.SearchHasFocus);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_a_task_only_selected_closes_Task_View_and_does_not_clear_the_selection_first()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        Assert.Equal(["Buy milk"], screen.SelectedTitles);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }
}
