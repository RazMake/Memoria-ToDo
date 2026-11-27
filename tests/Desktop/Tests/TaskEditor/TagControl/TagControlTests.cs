using System.IO;
using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>
/// The tag control as a user sees it: the pills in order with their sizes, the add pill, the menu, Search, Custom, the
/// keys, the tag box and the live drag. In-process, with fake services; at most 10 tests for this screen.
/// </summary>
[UX("TagControl")]
[Collection(ScreenTests.Collection)]
public sealed class TagControlTests
{
    [WpfFact]
    public void Pills_show_the_tags_in_order_with_sizes_and_an_add_pill_that_turns_off_at_fifteen()
    {
        using var host = new TagEditorWindow("work", "home", "ideas");

        Assert.Equal(["work", "home", "ideas"], host.Pills);
        Assert.Equal("+", host.AddText);
        var badge = TagControls.ShownSize(host.Editor, TagControls.TagPill(host.Editor, "home")).Height / 0.95;
        Assert.InRange(TagControls.ShownSize(host.Editor, TagControls.TagPill(host.Editor, "work")).Height / badge, 1.01, 1.09);
        Assert.InRange(TagControls.ShownSize(host.Editor, TagControls.TagPill(host.Editor, "ideas")).Height / badge, 0.91, 0.99);

        host.ClickClear("home");
        Assert.Equal(["work", "ideas"], host.Editor.TaskTags);
        Assert.Equal([["work", "ideas"]], host.Raised);

        host.Editor.TaskTags = [];
        Assert.Equal("No tag", host.AddText);

        host.Editor.TaskTags = [.. Enumerable.Range(1, 15).Select(number => $"tag-{number}")];
        Assert.False(TagControls.AddPill(host.Editor).IsEnabled);
        host.Editor.TaskTags = ["work"];
        Assert.True(TagControls.AddPill(host.Editor).IsEnabled);

        host.Editor.TaskTags = ["σ", "ς"];
        Assert.Equal(["σ", "ς"], host.Pills);
    }

    [WpfFact]
    public void Choosing_in_the_menu_replaces_the_clicked_tag_in_place_or_appends_from_the_add_pill_and_never_repeats_a_tag()
    {
        using var host = new TagEditorWindow("work", "home");
        host.Services.UseTag("ideas", 5);
        host.Services.UseTag("work", 4);

        host.ClickPill("work");
        Assert.DoesNotContain("work", TagControls.MenuTags(host.Editor));
        Assert.DoesNotContain("home", TagControls.MenuTags(host.Editor));
        OpenWindows.Click(TagControls.MenuItem(host.Editor, "ideas"));
        Assert.Equal(["ideas", "home"], host.Editor.TaskTags);

        host.ClickAdd();
        Assert.DoesNotContain("ideas", TagControls.MenuTags(host.Editor));
        host.Services.UseTag("errands", 3);
        host.Remove();
        host.Put();
        host.ClickAdd();
        OpenWindows.Click(TagControls.MenuItem(host.Editor, "errands"));
        Assert.Equal(["ideas", "home", "errands"], host.Editor.TaskTags);
        Assert.Equal([["ideas", "home"], ["ideas", "home", "errands"]], host.Raised);
    }

    [WpfFact]
    public void Search_keeps_what_was_typed_and_finds_the_normalized_tag()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("my-work", 2);

        host.ClickAdd();
        TagControls.SearchBox(host.Editor).Text = "My w";
        TextFields.Type(TagControls.SearchBox(host.Editor), "My w");

        Assert.Equal("My w", TagControls.SearchBox(host.Editor).Text);
        Assert.Equal(["my-work"], TagControls.SearchMatches(host.Editor));
    }

    [WpfFact]
    public void Custom_shows_the_colors_and_a_typed_tag_the_task_has_only_recolors_it()
    {
        using var host = new TagEditorWindow("work");

        host.OpenCustom();
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.False(TagControls.ColorsAreShowing(host.Editor));
        host.Type("work");
        Assert.True(TagControls.ColorsAreShowing(host.Editor));
        OpenWindows.Click(TagControls.ColorButton(host.Editor, TagColors.All[1].Name));

        Assert.Equal(["work"], host.Editor.TaskTags);
        Assert.Empty(host.Raised);
        Assert.Equal([("work", TagColors.All[1].Name)], host.Services.TagColors.Kept);
    }

    [WpfFact]
    public void Custom_from_a_pill_replaces_that_tag_and_Enter_adds_the_typed_tag_while_a_color_that_cant_be_kept_leaves_Custom_open()
    {
        using var host = new TagEditorWindow("work", "home");

        host.OpenCustomFrom("work");
        host.Type("Side Job");
        host.Press(Key.Enter);
        Assert.Equal(["side-job", "home"], host.Editor.TaskTags);
        Assert.False(TagControls.CustomIsOpen(host.Editor));

        host.Services.TagColors.Failure = new NoRepositoryFolderException();
        host.OpenCustom();
        host.Type("fails");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, TagColors.All[1].Name));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("fails", host.Typed);
        Assert.Contains(host.Texts, text => text.Contains("Configure", StringComparison.Ordinal));
        Assert.Equal(["side-job", "home"], host.Editor.TaskTags);

        host.Press(Key.Enter);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(["side-job", "home", "fails"], host.Editor.TaskTags);

        // A color kept before a notice failed: the tag is chosen, Custom hides, and the message stays visible under the tags.
        host.Services.TagColors.Failure = new NoticeFailedException<TaskItem>(null, [new IOException("Changed failed.")]);
        host.OpenCustom();
        host.Type("saved");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, TagColors.All[1].Name));
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(["side-job", "home", "fails", "saved"], host.Editor.TaskTags);
        Assert.Contains("Saved, but the app couldn't finish updating: Changed failed.", host.Texts);

        host.OpenCustom();
        Assert.DoesNotContain(host.Texts, text => text.StartsWith("Saved, but", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Keys_move_a_focused_pill_with_Alt_and_Tab_visits_the_pills_then_the_add_pill()
    {
        using var host = new TagEditorWindow("work", "home", "ideas");

        host.Focus(TagControls.PillId("home"));
        host.Press(Key.LeftAlt, Key.Left);
        Assert.Equal(["home", "work", "ideas"], host.Editor.TaskTags);
        Assert.Equal(TagControls.PillId("home"), TagControls.FocusedId());
        host.Press(Key.LeftAlt, Key.Right);
        host.Press(Key.LeftAlt, Key.Right);
        Assert.Equal(["work", "ideas", "home"], host.Editor.TaskTags);
        Assert.Equal(TagControls.PillId("home"), TagControls.FocusedId());
        Assert.Equal([["home", "work", "ideas"], ["work", "home", "ideas"], ["work", "ideas", "home"]], host.Raised);

        host.Editor.TaskTags = ["work", "home"];
        host.Focus(TagControls.PillId("work"));
        host.Press(Key.LeftAlt, Key.Left);
        Assert.Equal(["work", "home"], host.Editor.TaskTags);

        host.Editor.TaskTags = [.. Enumerable.Range(1, 15).Select(number => $"tag-{number}")];
        host.Focus(TagControls.ClearId("tag-15"));
        host.Press(Key.Tab);
        Assert.NotEqual(EditorFields.AddTagPill, TagControls.FocusedId());
    }

    [WpfFact]
    public void The_window_closes_the_edit_and_chooses_the_typed_text()
    {
        using var host = new TagEditorWindow("work");

        host.OpenCustom();
        host.Type("errands");
        Assert.True(host.Editor.ChooseTypedText());
        Assert.Equal(["work", "errands"], host.Editor.TaskTags);

        host.OpenCustom();
        host.Editor.TaskTags = [.. Enumerable.Range(1, 15).Select(number => $"tag-{number}")];
        host.Type("more");
        Assert.False(host.Editor.ChooseTypedText());
        Assert.Equal("more", host.Typed);
        Assert.Equal(15, host.Editor.TaskTags.Count);
        host.Editor.TaskTags = ["work", "errands"];
        host.Editor.CloseEdit();

        host.ClickAdd();
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        host.Editor.CloseEdit();
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal([["work", "errands"]], host.Raised);
    }

    [WpfFact]
    public void The_tag_box_rewrites_its_text_as_a_tag_and_keeps_a_trailing_space_until_the_next_character()
    {
        using var host = new TagEditorWindow();

        host.OpenCustom();
        host.Type("My");
        host.Type("My ");
        Assert.Equal("my ", host.Typed);
        host.Type("My W");
        Assert.Equal("my-w", host.Typed);
        host.Type("My  Work");
        Assert.Equal("my-work", host.Typed);
        host.Type(" ");
        Assert.Equal("", host.Typed);
        host.Type("a  ");
        Assert.Equal("a ", host.Typed);
    }

    [WpfFact]
    public async Task The_badges_show_the_tags_in_order_in_their_colors_follow_the_theme_and_the_tag_colors_and_catch_up_when_put_back()
    {
        using var host = new BadgesHost("work", "home", "errand");
        Assert.Equal(["work", "home", "errand"], TagControls.BadgeTags(host.Badges));
        Assert.Empty(TagControls.RemovableTags(host.Badges));
        host.Badges.Tags = ["home"];
        Assert.Equal(["home"], TagControls.BadgeTags(host.Badges));
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.Seed("home", "mauve-9");
        host.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.Seed("home", "blue-2");
        host.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: false), TagControls.FillOf(host.Badges, "home"));
        host.Services.Theme.Switch(true);
        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: true), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.SetColor("home", "green-1");
        Assert.Equal(TagControls.Expected(TagColors.Find("green-1")!, dark: true), TagControls.FillOf(host.Badges, "home"));
        host.Services.TagColors.Seed("home", "red-1");
        await Task.Run(host.Services.TagColors.RaiseChanged);
        await RenderedField.Settled();
        Assert.Equal(TagControls.Expected(TagColors.Find("red-1")!, dark: true), TagControls.FillOf(host.Badges, "home"));

        host.Remove();
        host.Services.Theme.Switch(false);
        host.Put();
        Assert.Equal(TagControls.Expected(TagColors.Find("red-1")!, dark: false), TagControls.FillOf(host.Badges, "home"));

        using var dark = new BadgesHost(dark: true, "home");
        dark.Services.TagColors.Seed("home", "red-3");
        dark.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.Find("red-3")!, dark: true), TagControls.FillOf(dark.Badges, "home"));
    }

    [WpfFact]
    public void Dragging_a_pill_reorders_live_and_raises_once_on_drop_and_nothing_when_cancelled_by_Esc_a_lost_mouse_or_tags_set_from_outside_or_dropped_in_place()
    {
        using var host = new TagEditorWindow("work", "home", "ideas");
        var system = SystemParameters.MinimumHorizontalDragDistance;

        host.PressOn("work");
        var start = host.CenterOf("work");
        host.MoveTo(new Point(start.X + system / 2, start.Y));
        Assert.Equal(["work", "home", "ideas"], host.Pills);
        Assert.False(host.EditorHoldsMouse);

        host.MoveOnto("ideas");
        Assert.True(host.EditorHoldsMouse);
        Assert.Equal(["home", "ideas", "work"], host.Pills);
        Assert.Empty(host.Raised);
        Assert.True(TagControls.ShownSize(host.Editor, TagControls.TagPill(host.Editor, "home")).Height
            > TagControls.ShownSize(host.Editor, TagControls.TagPill(host.Editor, "work")).Height);
        host.Release();
        Assert.Equal([["home", "ideas", "work"]], host.Raised);
        Assert.False(host.EditorHoldsMouse);

        host.PressOn("home");
        host.MoveOnto("work");
        host.Press(Key.Escape);
        Assert.Equal(["home", "ideas", "work"], host.Pills);
        Assert.Equal(["home", "ideas", "work"], host.Editor.TaskTags);

        host.PressOn("ideas");
        host.MoveOnto("work");
        host.LoseMouse();
        Assert.Equal(["home", "ideas", "work"], host.Pills);

        host.PressOn("ideas");
        host.MoveTo(new Point(host.CenterOf("ideas").X + system * 3, host.CenterOf("ideas").Y));
        host.MoveTo(host.CenterOf("ideas"));
        host.Release();
        Assert.Single(host.Raised);

        // Tags set from outside, such as by a sync, cancel the drag, active or pending; the release then changes nothing.
        host.PressOn("work");
        host.MoveOnto("home");
        Assert.Equal(["work", "home", "ideas"], host.Pills);
        host.Editor.TaskTags = ["ideas", "work", "home"];
        Assert.Equal(["ideas", "work", "home"], host.Pills);
        Assert.False(host.EditorHoldsMouse);
        host.Release();
        Assert.Equal(["ideas", "work", "home"], host.Editor.TaskTags);
        Assert.Equal(["ideas", "work", "home"], host.Pills);

        host.PressOn("home");
        host.MoveOnto("ideas");
        host.Editor.TaskTags = ["work", "home"];
        Assert.Equal(["work", "home"], host.Pills);
        Assert.False(host.EditorHoldsMouse);
        host.Release();
        Assert.Equal(["work", "home"], host.Editor.TaskTags);
        Assert.Single(host.Raised);

        host.PressOn("work");
        var pressedAt = host.CenterOf("work");
        host.Editor.TaskTags = ["x", "work"];
        host.MoveTo(new Point(pressedAt.X + system * 3, pressedAt.Y));
        Assert.False(host.EditorHoldsMouse);
        Assert.Equal(["x", "work"], host.Pills);
        host.LoseMouse();

        // Esc, through the editor's CloseEdit that Task View calls, cancels only the drag; Custom and its text stay for the next Esc.
        host.OpenCustom();
        host.Type("later");
        host.PressOn("work");
        host.MoveOnto("x");
        Assert.True(host.EditorHoldsMouse);
        Assert.Equal(["work", "x"], host.Pills);
        Assert.True(host.Editor.CloseEdit());
        Assert.False(host.EditorHoldsMouse);
        Assert.Equal(["x", "work"], host.Pills);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("later", host.Typed);
        host.LoseMouse();
        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Single(host.Raised);
    }
}
