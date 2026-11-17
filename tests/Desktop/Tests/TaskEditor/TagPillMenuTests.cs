using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The pill's menu on screen, on the common paths: open it, choose a top tag, search and click a match. The menu leaves out
/// the task's current tag; choosing replaces the tag and raises TagChanged once. Which tags it lists and what Search matches
/// are in the unit tests of the menu model and the tags model.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagPillMenuTests
{
    [WpfFact]
    public void Lists_the_top_tags_other_than_the_current_one_as_colored_badges_then_Search_then_Custom()
    {
        using var host = new TagEditorWindow("b");
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");
        host.Services.TagColors.Seed("a", "teal-3");

        host.ClickPill();

        Assert.Equal(["a", "c"], TagControls.MenuTags(host.Editor));
        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: false), TagControls.MenuFillOf(host.Editor, "a"));
        Assert.True(TagControls.SearchBox(host.Editor).IsVisible);
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void With_no_other_tag_in_use_the_menu_starts_with_Search()
    {
        using var host = new TagEditorWindow("only");
        host.Services.UseTag("only");

        host.ClickPill();

        Assert.Empty(TagControls.MenuTags(host.Editor));
        Assert.True(TagControls.SearchBox(host.Editor).IsVisible);
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void Clicking_a_top_tag_replaces_the_tag_raises_TagChanged_once_closes_the_menu_and_returns_the_focus_to_the_pill()
    {
        using var host = new TagEditorWindow("mine");
        host.Services.UseTag("echo", 2);
        host.ClickPill();

        OpenWindows.Click(TagControls.MenuItem(host.Editor, "echo"));

        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.Equal("echo", host.PillText);
        var (tag, tagThen, kept) = Assert.Single(host.Changes);
        Assert.Equal("echo", tag);
        Assert.Equal("echo", tagThen);
        Assert.Empty(kept);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfFact]
    public void Choosing_a_tag_for_a_task_with_none_shows_the_tag_with_an_x()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("echo", 2);
        host.ClickPill();

        OpenWindows.Click(TagControls.MenuItem(host.Editor, "echo"));

        Assert.Equal("echo", host.PillText);
        Assert.True(TagControls.HasClearButton(host.Editor));
        Assert.Equal("echo", Assert.Single(host.Changes).Tag);
    }

    [WpfFact]
    public void Typing_in_Search_shows_the_matches_without_the_current_tag_and_clicking_one_replaces_the_tag_and_closes_both_menus()
    {
        using var host = new TagEditorWindow("errand");
        host.Services.UseTag("errand", 2);
        host.Services.UseTag("echo");
        host.Services.UseTag("work");
        host.ClickPill();

        TextFields.Type(TagControls.SearchBox(host.Editor), "e");
        Assert.Equal(["echo"], TagControls.SearchMatches(host.Editor));
        OpenWindows.Click(TagControls.SearchMatch(host.Editor, "echo"));

        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.Equal("echo", Assert.Single(host.Changes).Tag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfFact]
    public void A_search_with_no_match_shows_one_dimmed_line_that_is_not_a_button()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("work");
        host.ClickPill();

        TextFields.Type(TagControls.SearchBox(host.Editor), "zzz");

        Assert.NotNull(TagControls.NoMatchLine(host.Editor));
        Assert.Equal("No matching tags", OpenWindows.TextOf(TagControls.NoMatchLine(host.Editor)!));
        Assert.Empty(TagControls.SearchMatches(host.Editor));
    }

    [WpfFact]
    public void A_search_for_only_the_current_tag_finds_no_match()
    {
        using var host = new TagEditorWindow("work");
        host.Services.UseTag("work");
        host.ClickPill();

        TextFields.Type(TagControls.SearchBox(host.Editor), "wor");

        Assert.Empty(TagControls.SearchMatches(host.Editor));
        Assert.NotNull(TagControls.NoMatchLine(host.Editor));
    }

    [WpfFact]
    public void Search_text_is_not_a_change_and_closing_the_menu_drops_it()
    {
        using var host = new TagEditorWindow();
        host.ClickPill();
        TextFields.Type(TagControls.SearchBox(host.Editor), "Big Errand");

        host.Press(Key.Escape);
        host.ClickPill();

        Assert.Null(host.Editor.TaskTag);
        Assert.Empty(host.Changes);
        Assert.Equal("", TagControls.SearchBox(host.Editor).Text);
    }

    [WpfFact]
    public void Clicking_Custom_closes_the_menu_and_shows_the_New_tag_box_with_the_cursor_in_it()
    {
        using var host = new TagEditorWindow();

        host.OpenCustom();

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
    }

    [WpfFact]
    public void Choosing_a_tag_from_the_menu_while_Custom_shows_leaves_Custom_with_its_typed_text()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("echo", 2);
        host.OpenCustom();
        host.Type("half");
        host.ClickPill();

        OpenWindows.Click(TagControls.MenuItem(host.Editor, "echo"));

        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("half", host.Typed);
    }

    [WpfFact]
    public void The_menu_follows_the_tags_in_use_while_open_and_still_leaves_out_the_current_tag()
    {
        using var host = new TagEditorWindow("home");
        host.Services.UseTag("home");
        host.ClickPill();

        host.Services.UseTag("work", 2);
        host.Services.UseTag("home", 3);

        Assert.Equal(["work"], TagControls.MenuTags(host.Editor));
    }
}
