using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The tag control's pill on screen: "No tag" with no x, or the tag in its color with an x; the x clears and never opens the
/// menu; a click elsewhere on the pill opens it. Which tags the menu lists and what each key does are in the menu and keys tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagPillTests
{
    [WpfFact]
    public void With_no_tag_the_pill_shows_the_words_No_tag_and_no_x()
    {
        using var host = new TagEditorWindow();

        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Null(host.Editor.TaskTag);
    }

    [WpfFact]
    public void With_a_tag_the_pill_shows_it_with_an_x()
    {
        using var host = new TagEditorWindow("work");

        Assert.Equal("work", host.PillText);
        Assert.True(TagControls.HasClearButton(host.Editor));
        Assert.Equal("work", host.Editor.TaskTag);
    }

    [WpfFact]
    public void The_pill_shows_the_tag_in_its_kept_color_and_follows_the_theme()
    {
        using var host = new TagEditorWindow();
        host.Services.TagColors.Seed("work", "teal-3");
        host.Editor.TaskTag = "work";
        var teal = TagColors.Find("teal-3")!;
        Assert.Equal(TagControls.Expected(teal, dark: false), TagControls.PillFill(host.Editor));

        host.Services.Theme.Switch(true);

        Assert.Equal(TagControls.Expected(teal, dark: true), TagControls.PillFill(host.Editor));
    }

    [WpfFact]
    public void Setting_the_tag_changes_the_pill_and_raises_nothing()
    {
        using var host = new TagEditorWindow();

        host.Editor.TaskTag = "home";
        Assert.Equal("home", host.PillText);
        Assert.True(TagControls.HasClearButton(host.Editor));

        host.Editor.TaskTag = null;
        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void Setting_the_tag_leaves_an_open_menu_as_it_is()
    {
        using var host = new TagEditorWindow();
        host.ClickPill();

        host.Editor.TaskTag = "x";

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Clicking_the_x_clears_the_tag_raises_TagChanged_with_null_once_and_leaves_the_menu_closed_with_the_focus_on_the_pill()
    {
        using var host = new TagEditorWindow("work");
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));

        host.ClickClear();

        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Null(host.Editor.TaskTag);
        var (tag, tagThen, kept) = Assert.Single(host.Changes);
        Assert.Null(tag);
        Assert.Null(tagThen);
        Assert.Empty(kept);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfFact]
    public void A_click_elsewhere_on_the_pill_opens_the_menu_and_a_second_click_closes_it()
    {
        using var host = new TagEditorWindow("work");
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));

        host.ClickPill();
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void The_No_tag_pill_opens_the_menu_when_clicked()
    {
        using var host = new TagEditorWindow();

        host.ClickPill();

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }
}
