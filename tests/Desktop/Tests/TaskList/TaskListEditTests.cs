using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Edit Description: the right side turns into the Description editor; the title and the tags stay as they are.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListEditTests
{
    [WpfFact]
    public void F2_turns_the_right_side_of_the_selected_task_into_the_Description_editor_with_its_text()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Press(Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk", screen.EditedDescriptionText);
        Assert.False(screen.IsEditingTitle("Buy milk"));
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
    public void The_Description_editor_shows_the_tasks_tags_as_read_only_badges()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home", "errand");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Press(Key.F2);

        Assert.Equal(["home", "errand"], screen.BadgeTags("Buy milk"));
        Assert.Empty(screen.ControlTags("Buy milk"));
        Assert.Equal(0, screen.TagControlCount);
    }

    [WpfFact]
    public void The_Description_editor_has_no_Title_field()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);

        Assert.False(screen.ShowsTitleField);
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
    public void The_Save_shortcut_saves_the_description_with_the_title_and_tags_unchanged_and_leaves_the_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two litres");

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Tasks.All.Single();
        Assert.Equal("Oat milk, two litres", saved.Description);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void The_Save_shortcut_follows_the_platforms_Save_keys_and_Ctrl_Enter_then_types_a_line_break()
    {
        var services = new ListServices();
        services.SaveKeys.Set(ModifierKeys.Control, Key.S);
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Changed");

        screen.Press(Key.LeftCtrl, Key.Enter);
        Assert.True(screen.IsEditingAny);
        Assert.Equal("Oat milk", services.Tasks.All.Single().Description);
        screen.Press(Key.LeftCtrl, Key.S);

        Assert.Equal("\nChanged", services.Tasks.All.Single().Description);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_does_nothing_in_the_Description_editor()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Changed");

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk", services.Tasks.All.Single().Description);
    }

    [WpfFact]
    public void Esc_on_a_changed_edit_discards_it_and_shows_the_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("", services.Tasks.All.Single().Description);
    }

    [WpfFact]
    public void Undo_reopens_the_discarded_edit_with_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        screen.Press(Key.Escape);

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk", screen.EditedDescriptionText);
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
        screen.TypeDescription("A changed");
        screen.Press(Key.Escape);
        screen.PressOn("Task B", Key.F2);
        screen.TypeDescription("B changed");

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Task A"));
        Assert.Equal("A changed", screen.EditedDescriptionText);
        Assert.Equal(1, screen.CountButtons("Undo"));
        screen.Click("Undo");
        Assert.True(screen.IsEditing("Task B"));
        Assert.Equal("B changed", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void Undo_of_a_discarded_edit_while_another_task_is_edited_keeps_the_images_of_that_edit_while_its_Undo_bar_shows()
    {
        var services = new ListServices();
        services.Add("Task B");
        services.Add("Task A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Task A", Key.F2);
        screen.TypeDescription("A changed");
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

    // Edit Title after a changed Description edit leaves that edit as Esc does, so its Undo bar and images stay.

    [WpfFact]
    public void Edit_Title_after_a_changed_description_edit_keeps_the_Description_Undo_bar_and_its_images()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);
        var draft = screen.EditedDescriptionText;

        screen.ChooseContextItem("Buy milk", "Edit Title");

        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
        screen.Click("Undo");
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal(draft, screen.EditedDescriptionText);
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }

    // Checking the task being edited saves the edit first, as Save does, then resolves the task.

    [WpfFact]
    public void Checking_the_task_being_edited_saves_the_edit_and_resolves_the_task_which_hides_at_once()
    {
        var services = new ListServices();
        services.Add("Other");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two litres");

        screen.ClickCheckBox("Buy milk");

        var saved = services.Tasks.All.Single(task => task.Title == "Buy milk");
        Assert.NotNull(saved.Resolved);
        Assert.Equal("Oat milk, two litres", saved.Description);
        Assert.Equal(["Other"], screen.RowTitles);
        Assert.Equal("1 resolved", screen.ResolvedLabel);
        Assert.False(screen.IsEditingAny);
    }
}
