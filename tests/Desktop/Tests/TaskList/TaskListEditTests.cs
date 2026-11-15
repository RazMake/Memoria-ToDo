using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListEditTests
{
    [WpfFact]
    public void F2_turns_the_right_side_of_the_selected_task_into_the_editor_with_its_text()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Press(Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal(["Buy milk", "Second"], screen.RowTitles);
    }

    [WpfFact]
    public void Edit_Description_in_the_right_click_menu_selects_the_task_and_starts_the_edit_with_the_cursor_at_the_end_of_the_Description()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.ChooseContextItem("Buy milk", "Edit Description");

        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Description", screen.FocusedEditorField);
        Assert.True(screen.DescriptionCursorIsAtEnd);
    }

    [WpfFact]
    public void Selecting_another_task_leaves_the_edit_and_shows_that_tasks_description()
    {
        var services = new ListServices();
        services.Add("Call Ana", "About the invoice");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        screen.ClickRow("Call Ana");

        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Contains("About the invoice", screen.Web.Page);
    }

    [WpfFact]
    public void Ctrl_Enter_saves_the_edit_and_leaves_it_with_the_task_selected_and_its_description_formatted()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy oat milk"], screen.SelectedTitles);
        Assert.Contains("Oat milk", screen.Web.Page);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_also_saves_the_edit_and_leaves_it()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy oat milk"], screen.SelectedTitles);
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

    // Undo of a discarded edit of A while B is being edited: B is left as Esc leaves it, so B stays recoverable.

    [WpfFact]
    public void Undo_of_a_discarded_edit_while_another_task_is_edited_leaves_that_edit_with_its_own_Undo_bar_and_text()
    {
        var services = new ListServices();
        services.Add("Task B");
        services.Add("Task A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Task A", Key.F2);
        screen.TypeTitle("Task A", "A changed");
        screen.Press(Key.Escape);
        screen.PressOn("Task B", Key.F2);
        screen.TypeTitle("Task B", "B changed");

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Task A"));
        Assert.Equal("A changed", screen.EditorTitleText("Task A"));
        Assert.Equal(1, screen.CountButtons("Undo"));
        screen.Click("Undo");
        Assert.True(screen.IsEditing("Task B"));
        Assert.Equal("B changed", screen.EditorTitleText("Task B"));
    }

    [WpfFact]
    public void Undo_of_a_discarded_edit_while_another_task_is_edited_keeps_the_images_of_that_edit_while_its_Undo_bar_shows()
    {
        var services = new ListServices();
        services.Add("Task B");
        services.Add("Task A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Task A", Key.F2);
        screen.TypeTitle("Task A", "A changed");
        screen.Press(Key.Escape);
        screen.PressOn("Task B", Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);

        screen.Click("Undo");

        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }

    // Checking the task being edited saves the edit first, as Ctrl+Enter does, then resolves the task.

    [WpfFact]
    public void Checking_the_task_being_edited_saves_the_edit_and_resolves_the_task_which_hides_at_once()
    {
        var services = new ListServices();
        services.Add("Other");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.ClickCheckBox("Buy milk");

        var saved = services.Tasks.All.Single(task => task.Title == "Buy oat milk");
        Assert.NotNull(saved.Resolved);
        Assert.Equal(["Other"], screen.RowTitles);
        Assert.Equal("1 resolved", screen.ResolvedLabel);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Checking_the_task_being_edited_with_an_empty_Title_leaves_it_open_unchecked_with_the_editor_open_and_the_Title_marked()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "");

        screen.ClickCheckBox("Buy milk");

        Assert.Null(services.Tasks.All.Single().Resolved);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.False(screen.CheckBoxIsTicked("Buy milk"));
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.EditedTitleIsMarked);
    }
}
