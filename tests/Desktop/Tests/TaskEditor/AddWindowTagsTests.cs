using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Tags line in the Add window, on the common path. Which suggestions show, their order, and what Enter, Down, and
/// Backspace do to the tags are in the unit tests of the tags model; these check the line is wired to it.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTagSuggestionsTests
{
    [WpfFact]
    public void Typing_lists_the_tags_in_use_that_start_with_it_most_used_first()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("hobby");
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("work", 3);
        screen.Show();

        screen.TypeTag("ho");

        Assert.Equal(["home", "hobby"], screen.Suggestions);
    }

    [WpfFact]
    public async Task Suggestions_refresh_when_Tasks_changes_on_a_worker_thread()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        await screen.Services.UseTagOnWorkerThread("hobby", 2);
        await RenderedField.Settled();

        Assert.Equal(["hobby", "home"], screen.Suggestions);
    }

    [WpfFact]
    public void Esc_closes_the_suggestions_first_then_Custom_and_a_third_Esc_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Escape);

        Assert.Empty(screen.Suggestions);
        Assert.True(screen.IsShowing);
        Assert.True(TagControls.CustomIsOpen(screen.Window));

        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Enter_adds_the_typed_text_as_a_tag_and_clears_the_line_without_saving()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");

        screen.Press(Key.Enter);

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
        Assert.Equal(["urgent"], screen.ShownTags);
        Assert.Equal("", screen.TypedTag);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void Down_then_Enter_adds_the_chosen_suggestion_instead_of_the_typed_text()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal(["home"], screen.Editor.Value.Tags);
        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void Clicking_a_suggestion_adds_that_tag_and_hides_Custom()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.TypeTag("ho");
        screen.Focus(EditorFields.Title);

        screen.ClickSuggestion("hobby");

        Assert.Equal(["hobby"], screen.Editor.Value.Tags);
        Assert.Equal("", screen.TypedTag);
        Assert.False(TagControls.CustomIsOpen(screen.Window));
    }

    [WpfFact]
    public void Backspace_in_the_empty_Tags_line_removes_the_last_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a", "b");
        screen.OpenCustom();
        screen.Focus(EditorFields.Tags);

        screen.Press(Key.Back);

        Assert.Equal(["a"], screen.ShownTags);
        Assert.Equal(["a"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Added_tags_are_saved_with_the_task_and_kept_in_the_draft_when_Add_hides()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("home");
        screen.Press(Key.Enter);
        screen.PressEscape();

        Assert.Equal(["home"], screen.KeptDraft?.Tags);

        screen.Show();
        screen.Save();

        Assert.Equal(["home"], Assert.Single(screen.Saved).Tags);
    }
}

[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTagMenuTests
{
    [WpfFact]
    public void Clicking_a_tag_in_the_plus_menu_adds_it_after_the_tags_already_there()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo", 4);
        screen.Show();
        screen.Fill("Buy milk", "", "mine");

        OpenWindows.Click(TagControls.Plus(screen.Window));
        OpenWindows.Click(TagControls.MenuItem(screen.Window, "echo"));

        Assert.Equal(["mine", "echo"], screen.Editor.Value.Tags);
        Assert.Equal(["mine", "echo"], TagControls.BadgeTags(screen.Window));
    }

    [WpfFact]
    public void Clicking_a_remove_button_removes_the_tag_from_the_task()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a", "b");

        OpenWindows.Click(TagControls.RemoveButton(screen.Window, "a"));

        Assert.Equal(["b"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Esc_closes_the_menu_first_and_a_second_Esc_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo");
        screen.Show();
        OpenWindows.Click(TagControls.Plus(screen.Window));

        screen.Press(Key.Escape);

        Assert.False(TagControls.MenuIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Esc_with_Custom_open_and_the_cursor_in_the_Title_closes_Custom_first_and_a_second_Esc_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("urgent");
        screen.Focus(EditorFields.Title);

        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Esc_with_Custom_open_and_the_cursor_on_Save_closes_Custom_first_and_Add_stays()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("urgent");
        TagEditorWindow.Focus(screen.SaveButton);

        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Esc_with_the_menu_open_and_the_cursor_in_the_Title_closes_the_menu_first_and_Add_stays()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo");
        screen.Show();
        OpenWindows.Click(TagControls.Plus(screen.Window));
        screen.Focus(EditorFields.Title);

        screen.Press(Key.Escape);

        Assert.False(TagControls.MenuIsOpen(screen.Window));
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Esc_in_Custom_drops_the_typed_text_and_keeps_Add_showing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("urgent");

        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);
        Assert.Empty(screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Save_with_a_typed_tag_whose_color_cant_be_kept_saves_nothing_and_keeps_Custom_open_with_the_reason()
    {
        using var screen = new AddScreen();
        screen.Services.TagColors.Failure = new NoRepositoryFolderException();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");
        screen.PickColor("red-2");

        screen.Save();

        Assert.Empty(screen.Saved);
        Assert.True(TagControls.CustomIsOpen(screen.Window));
        Assert.Contains(EditorFields.VisibleTexts(screen.Window), text => text.Contains("Configure", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Save_with_a_typed_tag_keeps_its_picked_color_and_saves_the_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");
        screen.PickColor("red-2");

        screen.Save();

        Assert.Equal(["urgent"], Assert.Single(screen.Saved).Tags);
        Assert.Equal([("urgent", "red-2")], screen.Services.TagColors.Kept);
    }
}
