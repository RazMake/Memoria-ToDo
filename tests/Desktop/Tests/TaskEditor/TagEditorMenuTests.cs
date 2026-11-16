using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The + menu on screen, on the common paths: open it, add a top tag, search and click a match, choose Custom. Which tags it
/// lists, what Search matches, and what each key does are in the unit tests of the menu model and the tags model.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagEditorMenuTests
{
    [WpfFact]
    public void Is_closed_until_the_plus_is_clicked_and_a_second_click_closes_it()
    {
        using var host = new TagEditorWindow();

        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.ClickPlus();
        Assert.True(TagControls.MenuIsOpen(host.Editor));

        host.ClickPlus();
        Assert.False(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Lists_the_top_tags_the_task_lacks_as_colored_badges_then_Search_then_Custom()
    {
        using var host = new TagEditorWindow("b");
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");
        host.Services.TagColors.Seed("a", "teal-3");

        host.ClickPlus();

        Assert.Equal(["a", "c"], TagControls.MenuTags(host.Editor));
        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: false), TagControls.MenuFillOf(host.Editor, "a"));
        Assert.True(TagControls.SearchBox(host.Editor).IsVisible);
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void With_no_top_tag_the_menu_starts_with_Search()
    {
        using var host = new TagEditorWindow();

        host.ClickPlus();

        Assert.Empty(TagControls.MenuTags(host.Editor));
        Assert.True(TagControls.SearchBox(host.Editor).IsVisible);
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void Clicking_a_top_tag_adds_it_after_the_tags_already_there_closes_the_menu_and_returns_the_focus_to_the_plus()
    {
        using var host = new TagEditorWindow("mine");
        host.Services.UseTag("echo", 2);
        host.ClickPlus();

        OpenWindows.Click(TagControls.MenuItem(host.Editor, "echo"));

        Assert.Equal(["mine", "echo"], host.Editor.Tags);
        Assert.Equal(["mine", "echo"], TagControls.BadgeTags(host.Editor));
        var (tag, tagsThen, kept) = Assert.Single(host.Added);
        Assert.Equal("echo", tag);
        Assert.Equal(["mine", "echo"], tagsThen);
        Assert.Empty(kept);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.AddTag, TagControls.FocusedId());
    }

    [WpfFact]
    public void Typing_in_Search_shows_the_matches_and_clicking_one_adds_it_and_closes_both_menus()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("errand", 2);
        host.Services.UseTag("echo");
        host.Services.UseTag("work");
        host.ClickPlus();

        TextFields.Type(TagControls.SearchBox(host.Editor), "e");
        Assert.Equal(["errand", "echo"], TagControls.SearchMatches(host.Editor));
        OpenWindows.Click(TagControls.SearchMatch(host.Editor, "echo"));

        Assert.Equal(["echo"], host.Editor.Tags);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
        Assert.Equal(TagControls.AddTag, TagControls.FocusedId());
    }

    [WpfFact]
    public void A_search_with_no_match_shows_one_dimmed_line_that_is_not_a_button()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("work");
        host.ClickPlus();

        TextFields.Type(TagControls.SearchBox(host.Editor), "zzz");

        Assert.NotNull(TagControls.NoMatchLine(host.Editor));
        Assert.Equal("No matching tags", OpenWindows.TextOf(TagControls.NoMatchLine(host.Editor)!));
        Assert.Empty(TagControls.SearchMatches(host.Editor));
    }

    [WpfFact]
    public void Search_text_is_not_a_change_and_closing_the_menu_drops_it()
    {
        using var host = new TagEditorWindow();
        host.ClickPlus();
        TextFields.Type(TagControls.SearchBox(host.Editor), "Big Errand");

        host.Press(Key.Escape);
        host.ClickPlus();

        Assert.Empty(host.Editor.Tags);
        Assert.Equal("", TagControls.SearchBox(host.Editor).Text);
    }

    [WpfFact]
    public void Clicking_Custom_closes_the_menu_and_shows_the_New_tag_box_with_the_cursor_in_it()
    {
        using var host = new TagEditorWindow();
        host.ClickPlus();

        OpenWindows.Click(TagControls.CustomItem(host.Editor));

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
    }

    [WpfFact]
    public void Closes_with_its_submenu_when_the_window_deactivates()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("work");
        host.ClickPlus();
        TextFields.Type(TagControls.SearchBox(host.Editor), "w");
        Assert.True(TagControls.SearchMatchesAreOpen(host.Editor));

        host.Deactivate();

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
    }

    [WpfFact]
    public void Closes_with_its_submenu_when_the_window_deactivates_while_the_cursor_is_in_the_Search_box()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("work");
        host.ClickPlus();
        TagEditorWindow.Focus(TagControls.SearchBox(host.Editor));
        TextFields.Type(TagControls.SearchBox(host.Editor), "w");
        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
        Assert.True(TagControls.SearchMatchesAreOpen(host.Editor));

        host.Deactivate();

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
    }

    [WpfFact]
    public void Stays_open_when_the_cursor_moves_into_the_Search_box()
    {
        using var host = new TagEditorWindow();
        host.ClickPlus();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));

        host.Press(Key.Down);

        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Scrolls_the_highlighted_match_into_view_as_Down_moves_past_the_end_of_what_shows()
    {
        using var host = ManyMatches();
        Assert.True(TagControls.SearchMatchesScroll(host.Editor));

        host.Press([.. Enumerable.Repeat(Key.Down, 100)]);

        var highlighted = TagControls.HighlightedSearchMatch(host.Editor);
        Assert.Equal(TagControls.SearchMatches(host.Editor)[99], highlighted);
        Assert.True(TagControls.SearchMatchIsInView(host.Editor, highlighted!));
    }

    [WpfFact]
    public void Scrolls_the_highlighted_match_into_view_as_Up_moves_back_to_the_first()
    {
        using var host = ManyMatches();
        host.Press([.. Enumerable.Repeat(Key.Down, 100)]);

        host.Press([.. Enumerable.Repeat(Key.Up, 99)]);

        var highlighted = TagControls.HighlightedSearchMatch(host.Editor);
        Assert.Equal(TagControls.SearchMatches(host.Editor)[0], highlighted);
        Assert.True(TagControls.SearchMatchIsInView(host.Editor, highlighted!));
    }

    // More matches for "t" than the submenu has room for, with the cursor in the Search box.
    private static TagEditorWindow ManyMatches()
    {
        var host = new TagEditorWindow();
        for (var i = 0; i < 120; i++)
        {
            host.Services.UseTag($"t{i:000}");
        }

        host.ClickPlus();
        TextFields.Type(TagControls.SearchBox(host.Editor), "t");
        return host;
    }

    [WpfFact]
    public void Setting_the_tags_leaves_an_open_menu_open()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("a");
        host.ClickPlus();

        host.Editor.Tags = ["x"];

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void An_open_menu_repaints_its_badges_when_the_theme_changes_and_keeps_its_highlight()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("echo", 2);
        host.Services.UseTag("foxtrot");
        host.Services.TagColors.Seed("echo", "teal-3");
        host.ClickPlus();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));
        host.Press(Key.Down);
        Assert.Equal("echo", TagControls.HighlightedMenuTag(host.Editor));

        host.Services.Theme.Switch(true);

        Assert.True(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: true), TagControls.MenuFillOf(host.Editor, "echo"));
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("foxtrot"), dark: true), TagControls.MenuFillOf(host.Editor, "foxtrot"));
        Assert.Equal("echo", TagControls.HighlightedMenuTag(host.Editor));
    }

    [WpfFact]
    public void An_open_menu_repaints_a_badge_when_the_tag_colors_change_and_keeps_its_highlight()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("echo", 2);
        host.ClickPlus();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));
        host.Press(Key.Down);

        host.Services.TagColors.SetColor("echo", "pink-1");

        Assert.True(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Expected(TagColors.Find("pink-1")!, dark: false), TagControls.MenuFillOf(host.Editor, "echo"));
        Assert.Equal("echo", TagControls.HighlightedMenuTag(host.Editor));
    }
}
