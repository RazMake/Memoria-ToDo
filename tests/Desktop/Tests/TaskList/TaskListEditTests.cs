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
    // Tall enough for the editor with a 140-tall Description, too short for one 400 tall.
    private const double MidHeight = 520;

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

    // The editor's Save button, at the bottom right of the right side.

    [WpfFact]
    public void No_Save_button_shows_on_the_right_side_while_no_edit_is_open()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        Assert.False(screen.ShowsSave);
    }

    [WpfFact]
    public void While_an_edit_is_open_Save_shows_at_the_bottom_right_of_the_right_side_below_the_Tags()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);

        var save = screen.SaveButton;
        var details = screen.Details;
        Assert.True(screen.TopOf(save) >= screen.BottomOf(screen.TagsPlus) - 0.5, "Save isn't below the Tags.");
        Assert.True(screen.BottomOf(save) <= screen.BottomOf(details) + 0.5, "Save is cut off at the bottom.");
        Assert.True(screen.BottomOf(details) - screen.BottomOf(save) < 40, "Save isn't at the bottom of the right side.");
        Assert.True(screen.RightOf(details) - screen.RightOf(save) < 40, "Save isn't at the right of the right side.");
        Assert.True(screen.LeftOf(save) > screen.LeftOf(details) + details.ActualWidth / 2, "Save isn't on the right half.");
    }

    [WpfFact]
    public void Clicking_Save_saves_the_edit_and_leaves_it_with_the_task_selected_and_its_description_formatted()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Click("Save");

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.ShowsSave);
        Assert.Equal(["Buy oat milk"], screen.SelectedTitles);
        Assert.Contains("Oat milk", screen.Web.Page);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Clicking_Save_with_an_empty_Title_saves_nothing_and_keeps_the_editor_open_with_the_Title_marked()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "");

        screen.Click("Save");

        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.EditedTitleIsMarked);
    }

    [WpfFact]
    public void Clicking_Save_when_the_save_fails_keeps_the_editor_open_with_the_text_and_the_message()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Click("Save");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy oat milk", screen.EditedTitleText);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }

    // The editor fills the right side's height.

    [WpfFact]
    public void The_Description_stretches_down_to_the_Tags_row_above_Save()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);

        var description = screen.EditedDescription;
        var plus = screen.TagsPlus;
        Assert.True(screen.BottomOf(description) <= screen.TopOf(plus) + 0.5, "The Description overlaps the Tags.");
        Assert.True(screen.TopOf(plus) - screen.BottomOf(description) <= 56, "The Description stops short of the Tags.");
        Assert.True(description.ActualHeight > 250, $"The Description is only {description.ActualHeight} tall.");
    }

    [WpfFact]
    public void The_right_side_does_not_scroll_as_a_whole_while_a_long_Description_scrolls_inside_itself()
    {
        var services = new ListServices();
        services.Add("Buy milk", string.Join("\n", Enumerable.Range(1, 200).Select(n => $"Line {n}")));
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);

        Assert.True(screen.ShowsSave);
        Assert.Equal(0, screen.EditorOuterScrollRange, 0.5);
        Assert.True(screen.DescriptionScrollsItself);
        Assert.True(screen.BottomOf(screen.SaveButton) <= screen.BottomOf(screen.Details) + 0.5, "Save is cut off at the bottom.");
    }

    [WpfFact]
    public void When_the_window_fits_the_editors_smallest_height_but_not_its_tallest_the_right_side_does_not_scroll_as_a_whole()
    {
        var services = new ListServices();
        services.Add("Buy milk", string.Join("\n", Enumerable.Range(1, 200).Select(n => $"Line {n}")));
        using var screen = new TaskListScreen(services);
        screen.ResizeToHeight(MidHeight);

        screen.PressOn("Buy milk", Key.F2);

        Assert.True(screen.ShowsSave);
        Assert.Equal(0, screen.EditorOuterScrollRange, 0.5);
        Assert.True(screen.DescriptionScrollsItself);
        Assert.True(screen.BottomOf(screen.SaveButton) <= screen.BottomOf(screen.Details) + 0.5, "Save is cut off at the bottom.");
    }

    [WpfFact]
    public void No_focus_outline_frames_the_right_side_when_the_keyboard_moves_the_focus_while_editing()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        Assert.False(screen.OutlineFramesTheRightSideAfterTheKeyboardMovesTheFocus(), "An outline frames the right side.");
    }

    [WpfFact]
    public void When_the_window_is_shorter_than_the_editors_smallest_height_the_right_side_scrolls()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.ResizeToHeight(300);

        screen.PressOn("Buy milk", Key.F2);

        Assert.True(screen.ShowsSave);
        Assert.True(screen.EditorOuterScrollRange > 0, "The right side doesn't scroll.");
    }
}
