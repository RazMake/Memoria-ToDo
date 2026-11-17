using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The tag editor's badges: their colors, and the button that removes a tag. The rules for the tags themselves are in the
/// unit tests of the tags model; these check the control shows and reports them.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagEditorBadgeTests
{
    [WpfFact]
    public void Shows_the_tags_as_badges_in_order_each_with_a_remove_button_before_the_plus()
    {
        using var host = new TagEditorWindow("work", "home");

        Assert.Equal(["work", "home"], TagControls.BadgeTags(host.Editor));
        Assert.Equal(["work", "home"], TagControls.RemovableTags(host.Editor));
        Assert.True(TagControls.Plus(host.Editor).IsVisible);
    }

    [WpfFact]
    public void Centers_the_tag_name_and_its_remove_button_in_the_badge_both_ways()
    {
        using var host = new TagEditorWindow("work", "home");

        var badge = TagControls.BoundsIn(host.Editor, TagControls.Badge(host.Editor, "work"));
        var name = TagControls.BoundsIn(host.Editor, TagControls.BadgeText(host.Editor, "work"));
        var remove = TagControls.BoundsIn(host.Editor, TagControls.RemoveButton(host.Editor, "work"));

        Assert.InRange(name.Top - badge.Top - (badge.Bottom - name.Bottom), -1, 1);
        Assert.InRange(name.Left - badge.Left - (badge.Right - remove.Right), -1, 1);
    }

    [WpfFact]
    public void Shows_a_tag_in_its_kept_color_or_its_default_for_the_theme()
    {
        using var host = new TagEditorWindow();
        host.Services.TagColors.Seed("home", "teal-3");

        host.Editor.Tags = ["home", "work"];

        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: false), TagControls.FillOf(host.Editor, "home"));
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("work"), dark: false), TagControls.FillOf(host.Editor, "work"));
    }

    [WpfFact]
    public void Shows_the_default_color_for_a_color_name_this_app_does_not_know()
    {
        using var host = new TagEditorWindow();
        host.Services.TagColors.Seed("home", "mauve-9");

        host.Editor.Tags = ["home"];

        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Editor, "home"));
    }

    [WpfFact]
    public void Switches_every_badge_to_the_dark_fill_when_the_theme_changes()
    {
        using var host = new TagEditorWindow("home");
        host.Services.TagColors.Seed("home", "teal-3");
        host.Services.TagColors.RaiseChanged();

        host.Services.Theme.Switch(true);

        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: true), TagControls.FillOf(host.Editor, "home"));
    }

    [WpfFact]
    public void Shows_a_new_color_when_the_tag_colors_change()
    {
        using var host = new TagEditorWindow("home");

        host.Services.TagColors.SetColor("home", "pink-1");

        Assert.Equal(TagControls.Expected(TagColors.Find("pink-1")!, dark: false), TagControls.FillOf(host.Editor, "home"));
    }

    [WpfFact]
    public void The_plus_is_in_the_same_wrapping_panel_as_the_badges_right_after_the_last_one_on_its_line()
    {
        using var host = new TagEditorWindow("work", "home");

        var panel = TagControls.PanelOf(TagControls.Badge(host.Editor, "home"));
        var last = TagControls.BoundsIn(host.Editor, TagControls.Badge(host.Editor, "home"));
        var plus = TagControls.BoundsIn(host.Editor, TagControls.Plus(host.Editor));

        Assert.Same(panel, TagControls.PanelOf(TagControls.Badge(host.Editor, "work")));
        Assert.Same(panel, TagControls.PanelOf(TagControls.Plus(host.Editor)));
        Assert.True(plus.Left >= last.Right);
        Assert.InRange(Math.Abs((plus.Top + plus.Height / 2) - (last.Top + last.Height / 2)), 0, last.Height);
    }

    [WpfFact]
    public void With_no_tags_the_plus_starts_the_row_at_the_left_of_the_badges_panel()
    {
        using var host = new TagEditorWindow();
        var plus = TagControls.Plus(host.Editor);

        var panel = TagControls.PanelOf(plus);

        Assert.Equal(0, Math.Round(plus.TranslatePoint(new System.Windows.Point(0, 0), panel).X));
        Assert.Equal(0, Math.Round(plus.TranslatePoint(new System.Windows.Point(0, 0), panel).Y));
    }

    [WpfFact]
    public void When_the_badges_wrap_the_plus_comes_after_the_last_badge_in_reading_order()
    {
        var tags = Enumerable.Range(1, 12).Select(i => $"a-long-tag-name-{i}").ToArray();
        using var host = new TagEditorWindow(tags);

        var first = TagControls.BoundsIn(host.Editor, TagControls.Badge(host.Editor, tags[0]));
        var last = TagControls.BoundsIn(host.Editor, TagControls.Badge(host.Editor, tags[^1]));
        var plus = TagControls.BoundsIn(host.Editor, TagControls.Plus(host.Editor));

        Assert.True(last.Top > first.Top, "The badges should wrap onto more than one line.");
        Assert.True(plus.Top >= last.Top - 1);
        Assert.True(plus.Top > last.Top + 1 || plus.Left >= last.Right);
    }

    [WpfFact]
    public void Clicking_a_remove_button_removes_that_tag_and_raises_TagRemoved_after_Tags_no_longer_holds_it()
    {
        using var host = new TagEditorWindow("a", "b", "c");

        OpenWindows.Click(TagControls.RemoveButton(host.Editor, "b"));

        Assert.Equal(["a", "c"], host.Editor.Tags);
        Assert.Equal(["a", "c"], TagControls.BadgeTags(host.Editor));
        var (tag, tagsThen) = Assert.Single(host.Removed);
        Assert.Equal("b", tag);
        Assert.Equal(["a", "c"], tagsThen);
    }
}
