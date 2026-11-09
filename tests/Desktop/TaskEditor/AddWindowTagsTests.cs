using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
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
    public void Suggestions_ignore_the_case_of_what_was_typed()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();

        screen.TypeTag("HO");

        Assert.Equal(["home"], screen.Suggestions);
    }

    [WpfFact]
    public void Suggestions_narrow_as_more_is_typed()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.TypeTag("ho");

        screen.TypeTag("hom");

        Assert.Equal(["home"], screen.Suggestions);
    }

    [WpfFact]
    public void Nothing_is_suggested_while_the_line_is_empty()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();

        Assert.Empty(screen.Suggestions);
    }

    [WpfFact]
    public void Suggestions_close_when_the_typed_text_is_erased()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.TypeTag("");

        Assert.Empty(screen.Suggestions);
    }

    [WpfFact]
    public void Nothing_is_suggested_when_no_tag_matches()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();

        screen.TypeTag("zzz");

        Assert.Empty(screen.Suggestions);
    }

    [WpfFact]
    public void A_tag_the_task_already_has_is_not_suggested()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.Fill("Buy milk", "", "home");

        screen.TypeTag("ho");

        Assert.Equal(["hobby"], screen.Suggestions);
    }

    [WpfFact]
    public void Suggestions_refresh_when_Tasks_changes()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.Services.UseTag("hobby", 2);

        Assert.Equal(["hobby", "home"], screen.Suggestions);
    }

    [WpfFact]
    public void Esc_closes_the_suggestions_first_and_Add_stays_open()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Escape);

        Assert.Empty(screen.Suggestions);
        Assert.True(screen.IsShowing);
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Esc_with_the_suggestions_closed_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");
        screen.Press(Key.Escape);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Enter_adds_the_typed_text_as_a_tag_and_clears_the_line()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("urgent");

        screen.Press(Key.Enter);

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
        Assert.Equal(["urgent"], screen.ShownTags);
        Assert.Equal("", screen.TypedTag);
        Assert.Empty(screen.Suggestions);
    }

    [WpfFact]
    public void Enter_trims_the_typed_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("  urgent  ");

        screen.Press(Key.Enter);

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Enter_spells_the_tag_as_the_tag_in_use()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("Home");
        screen.Show();
        screen.TypeTag("hOME");

        screen.Press(Key.Enter);

        Assert.Equal(["Home"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Enter_adds_the_suggestion_chosen_with_Down_instead_of_the_typed_text()
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
    public void Down_twice_chooses_the_second_suggestion()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home", 2);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Down);
        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal(["hobby"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Enter_without_a_chosen_suggestion_adds_the_typed_text_even_when_suggestions_are_open()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();
        screen.TypeTag("ho");

        screen.Press(Key.Enter);

        Assert.Equal(["ho"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Enter_on_an_empty_line_does_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "home");
        screen.Focus(EditorFields.Tags);

        screen.Press(Key.Enter);

        Assert.Equal(["home"], screen.Editor.Value.Tags);
        Assert.Equal(EditorFields.Tags, screen.FocusedField);
    }

    [WpfFact]
    public void Enter_on_a_line_of_spaces_does_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("   ");

        screen.Press(Key.Enter);

        Assert.Empty(screen.Editor.Value.Tags);
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Enter_in_the_Tags_line_does_not_save_the_task()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("home");

        screen.Press(Key.Enter);

        Assert.Empty(screen.Saved);
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Enter_on_a_tag_the_task_already_has_leaves_the_tags_and_clears_the_line()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "home");
        screen.TypeTag("HOME");

        screen.Press(Key.Enter);

        Assert.Equal(["home"], screen.Editor.Value.Tags);
        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void Tags_show_in_the_order_they_were_added()
    {
        using var screen = new AddScreen();
        screen.Show();

        foreach (var tag in new[] { "work", "home", "urgent" })
        {
            screen.TypeTag(tag);
            screen.Press(Key.Enter);
        }

        Assert.Equal(["work", "home", "urgent"], screen.ShownTags);
        Assert.Equal(["work", "home", "urgent"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Tags_set_on_the_editor_show_in_order_and_the_typed_text_is_cleared()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("typed");

        screen.Editor.Value = screen.Editor.Value with { Tags = ["b", "a"] };

        Assert.Equal(["b", "a"], screen.ShownTags);
        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void A_removed_tag_is_dropped_and_the_others_keep_their_order()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a", "b", "c");

        OpenWindows.Click(EditorFields.RemoveButton(screen.Window, "b"));

        Assert.Equal(["a", "c"], screen.ShownTags);
        Assert.Equal(["a", "c"], screen.Editor.Value.Tags);
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
    public void Backspace_in_a_Tags_line_with_text_keeps_the_tags()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a", "b");
        screen.TypeTag("ho");

        screen.Press(Key.Back);

        Assert.Equal(["a", "b"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Backspace_in_the_empty_Tags_line_of_a_task_with_no_tags_does_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Tags);

        screen.Press(Key.Back);

        Assert.Empty(screen.Editor.Value.Tags);
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Backspace_in_another_field_keeps_the_tags()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("", "", "a");
        screen.Focus(EditorFields.Title);

        screen.Press(Key.Back);

        Assert.Equal(["a"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Added_tags_are_saved_with_the_task()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("home");
        screen.Press(Key.Enter);

        screen.Save();

        Assert.Equal(["home"], Assert.Single(screen.Saved).Tags);
    }

    [WpfFact]
    public void Added_tags_are_kept_in_the_draft_when_Add_hides()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("home");
        screen.Press(Key.Enter);

        screen.PressEscape();

        Assert.Equal(["home"], screen.KeptDraft?.Tags);
    }
}

[UX]
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
    public void Clicking_a_chip_adds_its_tag_to_the_task()
    {
        using var screen = new AddScreen();
        SixTagsInUse(screen.Services);
        screen.Show();

        OpenWindows.Click(EditorFields.Chip(screen.Window, "echo"));

        Assert.Equal(["echo"], screen.Editor.Value.Tags);
        Assert.Equal(["echo"], screen.ShownTags);
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
    }

    [WpfFact]
    public void Clicking_a_chip_for_a_tag_the_task_has_changes_nothing()
    {
        using var screen = new AddScreen();
        SixTagsInUse(screen.Services);
        screen.Show();
        screen.Fill("Buy milk", "", "echo");

        OpenWindows.Click(EditorFields.Chip(screen.Window, "echo"));

        Assert.Equal(["echo"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void A_tag_the_task_has_still_shows_as_a_chip()
    {
        using var screen = new AddScreen();
        SixTagsInUse(screen.Services);
        screen.Show();

        screen.Fill("Buy milk", "", "gamma");

        Assert.Contains("gamma", screen.ChipTags);
    }

    [WpfFact]
    public void Chips_refresh_when_Tasks_changes()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();

        screen.Services.UseTag("work", 2);

        Assert.Equal(["work", "home"], screen.ChipTags);
    }

    [WpfFact]
    public void Chips_are_buttons_the_keyboard_reaches_with_Tab()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Show();

        var chip = EditorFields.Chip(screen.Window, "home");

        Assert.True(chip.IsTabStop);
        Assert.True(chip.Focusable);
        Assert.True(chip.IsEnabled);
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
