using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Collapsing the group of a row whose title is edited: the edit stays open, hidden with its row and its text, and shows
/// again when the group expands. It ends only as a shown edit does, and whatever puts the cursor back in it expands its group.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListCollapsedTitleEditTests
{
    private readonly ClosePrompt _prompt = new();

    private static ListServices HomeAndPlain()
    {
        var services = new ListServices();
        services.Add("Plain");
        services.Add("Home one", "", "home");
        return services;
    }

    // The title of "Home one" is being edited and its group is collapsed.
    private static TaskListScreen CollapsedEdit(ListServices services, string? typed = "Home two")
    {
        var screen = new TaskListScreen(services);
        screen.PressOn("Home one", Key.LeftShift, Key.F2);
        if (typed is not null)
        {
            screen.TypeTitle("Home one", typed);
        }

        screen.ToggleGroup("home");
        Assert.DoesNotContain("Home one", screen.RowTitles);
        return screen;
    }

    [WpfFact]
    public void Collapsing_the_group_of_a_changed_title_edit_keeps_it_and_its_text_for_when_the_group_expands()
    {
        using var screen = CollapsedEdit(HomeAndPlain());

        screen.ToggleGroup("home");

        Assert.True(screen.IsEditingTitle("Home one"));
        Assert.Equal("Home two", screen.EditorTitleText("Home one"));
    }

    [WpfFact]
    public void Collapsing_the_group_of_an_unchanged_title_edit_keeps_it_too()
    {
        using var screen = CollapsedEdit(HomeAndPlain(), typed: null);

        screen.ToggleGroup("home");

        Assert.True(screen.IsEditingTitle("Home one"));
        Assert.Equal("Home one", screen.EditorTitleText("Home one"));
    }

    [WpfFact]
    public void Esc_leaves_a_hidden_title_edit_with_no_Undo_bar_and_keeps_View_Tasks_open()
    {
        var services = HomeAndPlain();
        using var screen = CollapsedEdit(services);
        var detached = new System.Windows.Controls.TextBox();
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(screen.Window, detached);
        Assert.Same(detached, FocusManager.GetFocusedElement(screen.Window));
        Assert.Null(PresentationSource.FromVisual(detached));
        Assert.False(Keyboard.FocusedElement is DependencyObject focused
            && PresentationSource.FromDependencyObject(focused) == PresentationSource.FromVisual(screen.Window));

        screen.Press(Key.Escape);

        Assert.True(screen.View.IsOpen);
        Assert.False(screen.HasButton("Undo"));
        screen.ToggleGroup("home");
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Home one", "Plain"], services.Tasks.All.Select(task => task.Title).Order().ToList());
    }

    [WpfFact]
    public void Starting_another_edit_leaves_a_hidden_title_edit()
    {
        using var screen = CollapsedEdit(HomeAndPlain());

        screen.ChooseContextItem("Plain", "Edit Title");
        screen.ToggleGroup("home");

        Assert.False(screen.IsEditingTitle("Home one"));
        Assert.True(screen.IsEditingTitle("Plain"));
    }

    [WpfFact]
    public void Selecting_another_task_leaves_a_hidden_title_edit()
    {
        var services = HomeAndPlain();
        using var screen = CollapsedEdit(services);

        screen.ClickRow("Plain");
        screen.ToggleGroup("home");

        Assert.False(screen.ShowsTitleField);
        Assert.Contains(services.Tasks.All, task => task.Title == "Home one");
    }

    [WpfFact]
    public void Collapsing_the_edited_group_of_a_task_in_two_groups_keeps_the_edit_hidden_and_not_in_the_other_group()
    {
        using var screen = EditedInWork(out _);

        screen.ToggleGroup("work");

        Assert.False(screen.ShowsTitleField);
        Assert.False(screen.IsEditingTitle("Both", "home"));
    }

    [WpfFact]
    public void Expanding_the_edited_group_of_a_task_in_two_groups_shows_the_edit_again_in_it_with_its_text()
    {
        using var screen = EditedInWork(out _);
        screen.ToggleGroup("work");

        screen.ToggleGroup("work");

        Assert.True(screen.IsEditingTitle("Both", "work"));
        Assert.False(screen.IsEditingTitle("Both", "home"));
        Assert.Equal("Both changed", screen.EditorTitleText("Both", "work"));
    }

    [WpfFact]
    public void Cancel_in_the_close_prompt_expands_the_edited_group_of_a_task_shown_in_two_and_puts_the_cursor_back_there()
    {
        using var screen = EditedInWork(out _);
        screen.ToggleGroup("work");

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Cancel"));

        Assert.False(closed);
        Assert.Contains("Both", screen.RowTitlesIn("work"));
        Assert.True(screen.IsEditingTitle("Both", "work"));
        Assert.False(screen.IsEditingTitle("Both", "home"));
        Assert.Equal("Both changed", screen.EditorTitleText("Both", "work"));
        Assert.Equal("Title", screen.FocusedEditorField);
        Assert.True(screen.FocusIsInRow("Both", "work"));
    }

    [WpfFact]
    public void A_save_that_fails_in_the_close_prompt_expands_the_edited_group_of_a_task_shown_in_two_and_puts_the_cursor_back_there()
    {
        using var screen = EditedInWork(out var services);
        screen.ToggleGroup("work");
        services.Files.FailWritesUnder(services.TaskFolder);

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Save"));

        Assert.False(closed);
        Assert.Contains("Both", screen.RowTitlesIn("work"));
        Assert.True(screen.IsEditingTitle("Both", "work"));
        Assert.False(screen.IsEditingTitle("Both", "home"));
        Assert.Equal("Both changed", screen.EditorTitleText("Both", "work"));
        Assert.Equal("Title", screen.FocusedEditorField);
        Assert.True(screen.FocusIsInRow("Both", "work"));
    }

    // The title of "Both", which shows in the groups home and work, is edited in its work row and changed.
    private static TaskListScreen EditedInWork(out ListServices services)
    {
        services = new ListServices();
        services.AddOlder("Both", "", "work", "home");
        var screen = new TaskListScreen(services);
        screen.ClickRow("Both", "work");
        screen.Press(Key.LeftShift, Key.F2);
        screen.TypeTitle("Both", "Both changed", "work");
        return screen;
    }

    [WpfFact]
    public void Cancel_in_the_close_prompt_expands_the_group_and_puts_the_cursor_back_in_the_title()
    {
        using var screen = CollapsedEdit(HomeAndPlain());

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Cancel"));

        Assert.False(closed);
        Assert.Contains("Home one", screen.RowTitlesIn("home"));
        Assert.Equal("Home two", screen.EditorTitleText("Home one"));
        Assert.Equal("Title", screen.FocusedEditorField);
    }

    [WpfFact]
    public void Save_with_an_empty_title_in_the_close_prompt_expands_the_group_and_marks_the_title()
    {
        using var screen = CollapsedEdit(HomeAndPlain(), typed: "");

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Save"));

        Assert.False(closed);
        Assert.Contains("Home one", screen.RowTitlesIn("home"));
        Assert.True(screen.EditedTitleIsMarked);
        Assert.Equal("Title", screen.FocusedEditorField);
    }

    [WpfFact]
    public void A_save_that_fails_in_the_close_prompt_expands_the_group_and_puts_the_cursor_back_in_the_title()
    {
        var services = HomeAndPlain();
        using var screen = CollapsedEdit(services);
        services.Files.FailWritesUnder(services.TaskFolder);

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Save"));

        Assert.False(closed);
        Assert.Contains("Home one", screen.RowTitlesIn("home"));
        Assert.Equal("Home two", screen.EditorTitleText("Home one"));
        Assert.Equal("Title", screen.FocusedEditorField);
    }
}
