using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The + menu: what it lists and what clicking an item does. Which tags it lists, and in what order, is in the unit tests
/// of the tags model.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagEditorMenuTests
{
    [WpfFact]
    public void Is_closed_until_the_plus_is_clicked()
    {
        using var host = new TagEditorWindow();

        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.ClickPlus();

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Lists_the_tags_in_use_the_task_does_not_have_most_used_first_then_Custom()
    {
        using var host = new TagEditorWindow("b");
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");

        host.ClickPlus();

        Assert.Equal(["a", "c"], TagControls.MenuTags(host.Editor));
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void With_no_tag_to_list_shows_only_Custom()
    {
        using var host = new TagEditorWindow();

        host.ClickPlus();

        Assert.Empty(TagControls.MenuTags(host.Editor));
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);
    }

    [WpfFact]
    public void Lists_at_most_seven_tags()
    {
        using var host = new TagEditorWindow();
        for (var i = 1; i <= 9; i++)
        {
            host.Services.UseTag($"tag{i}", i);
        }

        host.ClickPlus();

        Assert.Equal(["tag9", "tag8", "tag7", "tag6", "tag5", "tag4", "tag3"], TagControls.MenuTags(host.Editor));
    }

    [WpfFact]
    public void Clicking_a_listed_tag_adds_it_after_the_tags_already_there_and_closes_the_menu()
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
    }

    [WpfFact]
    public void Clicking_Custom_closes_the_menu_and_shows_the_tag_box()
    {
        using var host = new TagEditorWindow();
        host.ClickPlus();

        OpenWindows.Click(TagControls.CustomItem(host.Editor));

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
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
