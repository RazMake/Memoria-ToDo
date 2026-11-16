using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The row keys work from the right side: after a click on the description gives the right side the focus, each key acts on the
/// selected task as on its row, except in an open edit's Preview, where keys keep their own meaning.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListRightSideKeysTests
{
    private static TaskListScreen WithDescriptionClicked(ListServices services, string title = "Buy milk")
    {
        var screen = new TaskListScreen(services);
        screen.ClickRow(title);
        screen.ClickDescription();
        return screen;
    }

    [WpfFact]
    public void F2_after_a_click_on_the_description_edits_the_description_of_the_selected_task()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = WithDescriptionClicked(services);

        screen.Press(Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Description", screen.FocusedEditorField);
    }

    [WpfFact]
    public void Shift_F2_after_a_click_on_the_description_edits_the_title_of_the_selected_task_in_its_row()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = WithDescriptionClicked(services);

        screen.Press(Key.LeftShift, Key.F2);

        Assert.True(screen.IsEditingTitle("Buy milk"));
    }

    [WpfFact]
    public void Delete_after_a_click_on_the_description_deletes_the_selected_task_with_an_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = WithDescriptionClicked(services);

        screen.Press(Key.Delete);

        Assert.Empty(screen.RowTitles);
        Assert.True(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Ctrl_D_after_a_click_on_the_description_resolves_the_selected_task()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = WithDescriptionClicked(services);

        screen.Press(Key.LeftCtrl, Key.D);

        Assert.NotNull(services.Tasks.All.Single().Resolved);
        Assert.Empty(screen.RowTitles);
    }

    [WpfFact]
    public void Alt_Down_after_a_click_on_the_description_moves_the_selected_task_one_place_down()
    {
        var services = new ListServices();
        services.Add("C", "Third");
        services.Add("B", "Second");
        services.Add("A", "First");
        using var screen = WithDescriptionClicked(services, "A");

        screen.Press(Key.LeftAlt, Key.Down);

        Assert.Equal(["B", "A", "C"], screen.RowTitles);
    }

    [WpfFact]
    public void Delete_while_the_Preview_of_an_open_edit_has_the_focus_keeps_the_task()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, changed");
        screen.ClickTab("Preview");
        Assert.True(screen.FocusIsInDetailsOutsideTheEditor);

        screen.Press(Key.Delete);

        Assert.Single(services.Tasks.All);
        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal("Preview", screen.SelectedTab);
        screen.ClickTab("Edit");
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk, changed", screen.EditedDescriptionText);
    }

    // "Work one" is selected, its group "work" is collapsed so no row shows it, and a click on its description gave the right side the focus.
    private static TaskListScreen WithCollapsedGroupAndDescriptionClicked(ListServices services)
    {
        services.Add("Plain");
        services.Add("Work two", "", "work");
        services.Add("Work one", "About work one", "work");
        var screen = new TaskListScreen(services);
        screen.ClickRow("Work one");
        screen.ToggleGroup("work");
        Assert.DoesNotContain("Work one", screen.RowTitles);
        screen.ClickDescription();
        return screen;
    }

    [WpfFact]
    public void F2_with_the_group_of_the_selected_task_collapsed_edits_its_description()
    {
        using var screen = WithCollapsedGroupAndDescriptionClicked(new ListServices());

        screen.Press(Key.F2);

        Assert.Equal("Description", screen.FocusedEditorField);
        Assert.Equal("About work one", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void Shift_F2_with_the_group_of_the_selected_task_collapsed_expands_it_and_edits_the_title_in_its_row()
    {
        using var screen = WithCollapsedGroupAndDescriptionClicked(new ListServices());

        screen.Press(Key.LeftShift, Key.F2);

        Assert.Contains("Work one", screen.RowTitlesIn("work"));
        Assert.True(screen.IsEditingTitle("Work one"));
    }

    [WpfFact]
    public void Alt_Down_with_the_group_of_the_selected_task_collapsed_moves_nothing()
    {
        var services = new ListServices();
        using var screen = WithCollapsedGroupAndDescriptionClicked(services);
        screen.ToggleGroup("work");
        var before = screen.RowTitles;
        screen.ToggleGroup("work");
        screen.ClickDescription();

        screen.Press(Key.LeftAlt, Key.Down);

        screen.ToggleGroup("work");
        Assert.Equal(before, screen.RowTitles);
    }

    [WpfFact]
    public void Delete_with_the_group_of_the_selected_task_collapsed_deletes_it_with_an_Undo_bar()
    {
        using var screen = WithCollapsedGroupAndDescriptionClicked(new ListServices());

        screen.Press(Key.Delete);

        Assert.True(screen.HasButton("Undo"));
        screen.ToggleGroup("work");
        Assert.Equal(["Work two"], screen.RowTitlesIn("work"));
    }

    [WpfFact]
    public void Ctrl_D_with_the_group_of_the_selected_task_collapsed_resolves_it()
    {
        var services = new ListServices();
        using var screen = WithCollapsedGroupAndDescriptionClicked(services);

        screen.Press(Key.LeftCtrl, Key.D);

        Assert.NotNull(services.Tasks.All.Single(task => task.Title == "Work one").Resolved);
    }
}
