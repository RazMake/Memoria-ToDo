using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The New tag box in the Add window, on the common path: the box is wired to the one tag. What Enter does to the tag is
/// also in the tag editor's own tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTagBoxTests
{
    [WpfFact]
    public void The_box_suggests_nothing_while_typing()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("hobby");
        screen.Show();

        screen.TypeTag("ho");

        Assert.DoesNotContain(EditorFields.VisibleTexts(screen.Window), text => text == "hobby");
        Assert.Null(screen.ShownTag);
    }

    [WpfFact]
    public void Enter_chooses_the_typed_text_as_the_tag_and_clears_the_line_without_saving()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");

        screen.Press(Key.Enter);

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
        Assert.Equal("urgent", screen.ShownTag);
        Assert.Equal("", screen.TypedTag);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void Enter_replaces_the_tag_the_task_has()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a");
        screen.TypeTag("urgent");

        screen.Press(Key.Enter);

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
        Assert.Equal("urgent", screen.ShownTag);
    }

    [WpfFact]
    public void Backspace_in_the_empty_Tags_line_does_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a");
        screen.OpenCustom();
        screen.Focus(EditorFields.Tags);

        screen.Press(Key.Back);

        Assert.Equal("a", screen.ShownTag);
        Assert.Equal(["a"], screen.Editor.Value.Tags);
        Assert.True(TagControls.CustomIsOpen(screen.Window));
    }

    [WpfFact]
    public void The_chosen_tag_is_saved_with_the_task_and_kept_in_the_draft_when_Add_hides()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("home");
        screen.Press(Key.Enter);
        screen.PressEscape();

        Assert.Equal(["home"], screen.KeptDraft?.Tags);

        screen.Show();
        Assert.Equal("home", screen.ShownTag);
        screen.Save();

        Assert.Equal(["home"], Assert.Single(screen.Saved).Tags);
    }
}

[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTagMenuTests
{
    [WpfFact]
    public void Clicking_a_tag_in_the_menu_replaces_the_tag_the_task_has()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo", 4);
        screen.Show();
        screen.Fill("Buy milk", "", "mine");

        OpenWindows.Click(TagControls.PillButton(screen.Window));
        OpenWindows.Click(TagControls.MenuItem(screen.Window, "echo"));

        Assert.Equal(["echo"], screen.Editor.Value.Tags);
        Assert.Equal("echo", screen.ShownTag);
    }

    [WpfFact]
    public void Clicking_the_x_clears_the_tag_from_the_task()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "a");

        OpenWindows.Click(TagControls.ClearButton(screen.Window));

        Assert.Empty(screen.Editor.Value.Tags);
        Assert.Null(screen.ShownTag);
        Assert.Equal(TagEditor.NoTagText, TagControls.PillText(screen.Window));
    }

    [WpfFact]
    public void Esc_closes_the_menu_first_and_a_second_Esc_hides_Add()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo");
        screen.Show();
        OpenWindows.Click(TagControls.PillButton(screen.Window));

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
        OpenWindows.Click(TagControls.PillButton(screen.Window));
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
    public void Save_with_text_still_in_Custom_makes_it_the_tasks_one_tag_without_writing_a_color()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");

        screen.Save();

        Assert.Equal(["urgent"], Assert.Single(screen.Saved).Tags);
        Assert.Empty(screen.Services.TagColors.Kept);
    }

    [WpfFact]
    public void Save_with_text_still_in_Custom_replaces_the_tag_the_task_had()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "old");
        screen.TypeTag("urgent");

        screen.Save();

        Assert.Equal(["urgent"], Assert.Single(screen.Saved).Tags);
    }

    [WpfFact]
    public void The_Save_button_with_text_still_in_Custom_makes_it_the_tasks_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");

        screen.ClickSave();

        Assert.Equal(["urgent"], Assert.Single(screen.Saved).Tags);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_with_text_still_in_Custom_makes_it_the_tasks_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.TypeTag("urgent");

        screen.SaveAndClear();

        Assert.Equal(["urgent"], Assert.Single(screen.Saved).Tags);
    }

    [WpfFact]
    public void A_color_clicked_in_Custom_keeps_it_and_makes_the_tag_the_tasks_tag()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "old");
        screen.TypeTag("urgent");

        screen.ClickTagColor("red-2");

        Assert.Equal(["urgent"], screen.Editor.Value.Tags);
        Assert.Equal([("urgent", "red-2")], screen.Services.TagColors.Kept);
        Assert.False(TagControls.CustomIsOpen(screen.Window));
    }

    [WpfFact]
    public void Searching_in_the_menu_and_clicking_a_match_chooses_it()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.Fill("Buy milk");

        OpenWindows.Click(TagControls.PillButton(screen.Window));
        TextFields.Type(TagControls.SearchBox(screen.Window), "hob");
        OpenWindows.Click(TagControls.SearchMatch(screen.Window, "hobby"));

        Assert.Equal(["hobby"], screen.Editor.Value.Tags);
        Assert.False(TagControls.MenuIsOpen(screen.Window));
    }

    // A task saved with several tags before the one-tag rule shows its first and saves as it showed.

    [WpfFact]
    public void A_task_given_several_older_tags_shows_its_first_tag_in_the_pill()
    {
        using var screen = new AddScreen();
        screen.Show();

        screen.Editor.Value = new TaskDraft(screen.Editor.Value.TaskId, "Buy milk", "", ["first", "second", "third"]);

        Assert.Equal("first", screen.ShownTag);
        Assert.Equal(["first"], screen.Editor.Value.Tags);
    }

    [WpfFact]
    public void Showing_the_first_of_several_older_tags_is_no_change_but_choosing_another_tag_is()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Editor.Value = new TaskDraft(screen.Editor.Value.TaskId, "Buy milk", "", ["first", "second"]);

        Assert.False(screen.Editor.HasChanges);

        screen.TypeTag("urgent");
        screen.Press(Key.Enter);

        Assert.True(screen.Editor.HasChanges);
    }

    [WpfFact]
    public void A_kept_draft_with_several_older_tags_opens_with_its_first_tag_only()
    {
        using var screen = new AddScreen();
        var draft = screen.Services.Tasks.StartDraft() with { Title = "Buy milk", Tags = ["first", "second"] };
        screen.Services.Tasks.KeepDraft(draft);

        screen.Show();

        Assert.Equal("first", screen.ShownTag);
        Assert.Equal(["first"], screen.Editor.Value.Tags);
    }
}
