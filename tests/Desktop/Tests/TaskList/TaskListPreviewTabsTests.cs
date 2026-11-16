using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The Edit and Preview tabs of Edit Description: what each shows, how the user switches, where the keyboard focus goes, and
/// what puts the user back on Edit. Resizing in Preview is checked in <see cref="TaskListImageResizeTests"/>.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListPreviewTabsTests
{
    private readonly ClosePrompt _prompt = new();

    private static TaskListScreen Editing(ListServices services, string description = "Oat milk", params string[] tags)
    {
        services.Add("Buy milk", description, tags);
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        return screen;
    }

    [WpfFact]
    public void An_edit_shows_the_headers_Edit_and_Preview_and_opens_on_Edit()
    {
        var services = new ListServices();
        using var screen = Editing(services);

        Assert.Equal(["Edit", "Preview"], screen.TabHeaders);
        Assert.Equal("Edit", screen.SelectedTab);
        Assert.Equal("Description", screen.FocusedEditorField);
    }

    [WpfFact]
    public void No_tab_headers_show_outside_an_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");

        Assert.Empty(screen.TabHeaders);
        Assert.False(screen.PageIsPreview);
    }

    [WpfFact]
    public void A_click_on_Preview_shows_the_draft_formatted_and_hides_the_editor()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Oat milk, two litres");

        screen.ClickTab("Preview");

        Assert.Equal("Preview", screen.SelectedTab);
        Assert.True(screen.PageIsPreview);
        Assert.Contains("Oat milk, two litres", screen.Web.Page);
        Assert.False(screen.IsEditing("Buy milk"));
    }

    [WpfFact]
    public void A_click_on_Edit_shows_the_editor_again_with_the_draft()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Oat milk, two litres");
        screen.ClickTab("Preview");

        screen.ClickTab("Edit");

        Assert.Equal("Edit", screen.SelectedTab);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk, two litres", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void Typing_and_switching_to_Preview_again_shows_the_new_draft()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.ClickTab("Preview");
        screen.ClickTab("Edit");

        screen.TypeDescription("Almond milk");
        screen.ClickTab("Preview");

        Assert.Contains("Almond milk", screen.Web.Page);
    }

    [WpfFact]
    public void An_empty_draft_shows_No_description_in_Preview()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("");

        screen.ClickTab("Preview");

        Assert.True(screen.ShowsText("No description."));
    }

    [WpfFact]
    public void Ctrl_Tab_switches_to_the_other_tab_from_the_editor_and_from_the_preview()
    {
        var services = new ListServices();
        using var screen = Editing(services);

        screen.Press(Key.LeftCtrl, Key.Tab);
        Assert.Equal("Preview", screen.SelectedTab);
        screen.Press(Key.LeftCtrl, Key.Tab);

        Assert.Equal("Edit", screen.SelectedTab);
    }

    [WpfFact]
    public void Ctrl_Shift_Tab_switches_to_the_other_tab_from_the_editor_and_from_the_preview()
    {
        var services = new ListServices();
        using var screen = Editing(services);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.Tab);
        Assert.Equal("Preview", screen.SelectedTab);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.Tab);

        Assert.Equal("Edit", screen.SelectedTab);
    }

    [WpfTheory]
    [InlineData(false, "Edit")]
    [InlineData(false, "Preview")]
    [InlineData(true, "Edit")]
    [InlineData(true, "Preview")]
    public void Ctrl_Tab_or_Ctrl_Shift_Tab_set_as_the_Save_keys_save_the_description_from_the_tab_instead_of_switching(bool withShift, string tab)
    {
        var services = new ListServices();
        services.SaveKeys.Set(withShift ? ModifierKeys.Control | ModifierKeys.Shift : ModifierKeys.Control, Key.Tab);
        using var screen = Editing(services);
        screen.TypeDescription("Changed");
        if (tab == "Preview")
        {
            screen.ClickTab("Preview");
        }

        Key[] chord = withShift ? [Key.LeftCtrl, Key.LeftShift, Key.Tab] : [Key.LeftCtrl, Key.Tab];
        screen.Press(chord);

        Assert.Equal("Changed", services.Tasks.All.Single().Description);
        Assert.False(screen.IsEditingAny);
        Assert.Empty(screen.TabHeaders);
    }

    [WpfFact]
    public void The_headers_are_not_keyboard_stops_and_draw_no_focus_rectangle()
    {
        var services = new ListServices();
        using var screen = Editing(services);

        Assert.False(screen.TabHeadersAreTabStops);
        Assert.False(screen.TabHeadersDrawFocusRectangles);
    }

    [WpfFact]
    public void Switching_to_Preview_moves_the_focus_to_the_right_side_and_back_to_Edit_puts_the_cursor_where_it_was()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.PutCursorInDescription(3);

        screen.ClickTab("Preview");
        Assert.True(screen.FocusIsInDetailsOutsideTheEditor);
        screen.ClickTab("Edit");

        Assert.Equal("Description", screen.FocusedEditorField);
        Assert.Equal((3, 0), screen.DescriptionSelection);
    }

    [WpfFact]
    public void Esc_in_Preview_abandons_the_edit_with_the_Undo_bar()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Almond milk");
        screen.ClickTab("Preview");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("Oat milk", services.Tasks.All.Single().Description);
    }

    [WpfFact]
    public void The_Save_shortcut_in_Preview_saves_the_draft()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Almond milk");
        screen.ClickTab("Preview");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Almond milk", services.Tasks.All.Single().Description);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void A_new_edit_opens_on_Edit_even_when_the_last_one_ended_in_Preview()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.ClickTab("Preview");
        screen.Press(Key.Escape);

        screen.PressOn("Buy milk", Key.F2);

        Assert.Equal("Edit", screen.SelectedTab);
        Assert.True(screen.IsEditing("Buy milk"));
    }

    [WpfFact]
    public void The_tags_show_as_read_only_badges_under_both_tabs()
    {
        var services = new ListServices();
        using var screen = Editing(services, "Oat milk", "home", "errand");

        Assert.Equal(["home", "errand"], screen.BadgeTags("Buy milk"));
        screen.ClickTab("Preview");

        Assert.Equal(["home", "errand"], screen.BadgeTags("Buy milk"));
        Assert.Empty(screen.ControlTags("Buy milk"));
    }

    [WpfFact]
    public void Leaving_the_edit_from_Preview_shows_the_description_without_resize_corners()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.ClickTab("Preview");

        screen.Press(Key.Escape);

        Assert.False(screen.PageIsPreview);
        Assert.Empty(screen.TabHeaders);
    }

    [WpfFact]
    public void Saving_from_Preview_shows_the_description_without_resize_corners()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.ClickTab("Preview");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.False(screen.PageIsPreview);
    }

    [WpfFact]
    public void Selecting_another_task_from_Preview_shows_its_description_without_resize_corners()
    {
        var services = new ListServices();
        services.Add("Call Ana", "About the invoice");
        using var screen = Editing(services);
        screen.ClickTab("Preview");

        screen.ClickRow("Call Ana");

        Assert.False(screen.PageIsPreview);
        Assert.Contains("About the invoice", screen.Web.Page);
    }

    [WpfFact]
    public void Undo_of_a_discarded_edit_switches_to_Edit_with_the_cursor_in_the_Description()
    {
        var services = new ListServices();
        services.Add("Task B");
        services.Add("Task A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Task A", Key.F2);
        screen.TypeDescription("A changed");
        screen.Press(Key.Escape);
        screen.PressOn("Task B", Key.F2);
        screen.ClickTab("Preview");

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Task A"));
        Assert.Equal("Edit", screen.SelectedTab);
        Assert.Equal("Description", screen.FocusedEditorField);
    }

    [WpfFact]
    public void Cancel_in_the_close_prompt_switches_to_Edit_with_the_cursor_in_the_Description()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Almond milk");
        screen.ClickTab("Preview");

        var closed = _prompt.Run(() => screen.View.Close(), ClosePrompt.Click("Cancel"));

        Assert.False(closed);
        Assert.Equal("Edit", screen.SelectedTab);
        Assert.Equal("Description", screen.FocusedEditorField);
        Assert.Equal("Almond milk", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void A_save_that_fails_in_Preview_switches_to_Edit_and_says_why()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.TypeDescription("Almond milk");
        screen.ClickTab("Preview");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Edit", screen.SelectedTab);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal("Description", screen.FocusedEditorField);
    }

    [WpfFact]
    public void When_the_web_view_failed_Preview_shows_the_draft_as_Markdown_source()
    {
        var services = new ListServices();
        using var screen = Editing(services);
        screen.Web.Fail("no browser");
        screen.TypeDescription("Almond **milk**");

        screen.ClickTab("Preview");

        Assert.Contains("Almond **milk**", screen.ReadOnlyFieldTexts);
    }
}
