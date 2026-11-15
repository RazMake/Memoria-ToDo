using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
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
    public void Esc_closes_the_suggestions_first_and_a_second_Esc_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Escape);

        Assert.Empty(screen.Suggestions);
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
    public void Clicking_a_suggestion_adds_that_tag_and_keeps_the_cursor_in_the_Tags_line()
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
        Assert.Equal(EditorFields.Tags, screen.FocusedField);
    }

    [WpfFact]
    public void Backspace_in_the_empty_Tags_line_removes_the_last_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a", "b");
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
public class AddWindowTagChipsTests
{
    [WpfFact]
    public void The_five_most_used_tags_show_as_chips_below_the_Tags_line_most_used_first()
    {
        using var screen = new AddScreen();
        SixTagsInUse(screen.Services);
        screen.Show();

        Assert.Equal(["gamma", "echo", "alpha", "beta", "delta"], screen.ChipTags);
    }

    [WpfFact]
    public void Clicking_a_chip_adds_its_tag_after_the_tags_already_there()
    {
        using var screen = new AddScreen();
        SixTagsInUse(screen.Services);
        screen.Show();
        screen.Fill("Buy milk", "", "mine");

        OpenWindows.Click(EditorFields.Chip(screen.Window, "echo"));

        Assert.Equal(["mine", "echo"], screen.Editor.Value.Tags);
        Assert.Equal(["mine", "echo"], screen.ShownTags);
    }

    private static void SixTagsInUse(EditorServices services)
    {
        services.UseTag("alpha", 3);
        services.UseTag("beta", 2);
        services.UseTag("gamma", 5);
        services.UseTag("delta");
        services.UseTag("echo", 4);
        services.UseTag("zulu");
    }
}
