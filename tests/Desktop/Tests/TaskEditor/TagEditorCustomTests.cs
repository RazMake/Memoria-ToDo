using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Custom: the tag box and the 30 colors. When a color is kept, and what a failure leaves, are in the unit tests of the
/// tags model; these check the control is wired to it, and how the colors are laid out.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagEditorCustomTests
{
    [WpfFact]
    public void Opening_Custom_puts_the_cursor_in_the_tag_box()
    {
        using var host = new TagEditorWindow();

        host.OpenCustom();

        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
    }

    [WpfFact]
    public void Shows_the_30_colors_in_Renderings_order()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();

        Assert.Equal(TagColors.All.Select(color => color.Name), TagControls.ColorButtons(host.Editor).Select(color => color.Name));
    }

    [WpfFact]
    public void Lays_the_colors_out_as_ten_columns_of_hues_and_three_rows_of_shades()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        var names = TagColors.All.Select(color => color.Name).ToList();

        var positions = names.Select(name => TagControls.PositionOf(host.Editor, name)).ToList();

        var columns = positions.Select(p => Math.Round(p.X)).Distinct().Order().ToList();
        var rows = positions.Select(p => Math.Round(p.Y)).Distinct().Order().ToList();
        Assert.Equal(10, columns.Count);
        Assert.Equal(3, rows.Count);
        for (var k = 0; k < names.Count; k++)
        {
            Assert.Equal(columns[k / 3], Math.Round(positions[k].X));
            Assert.Equal(rows[k % 3], Math.Round(positions[k].Y));
        }
    }

    [WpfFact]
    public void Shows_the_colors_for_the_current_theme_and_follows_a_switch()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        var red = TagColors.Find("red-2")!;
        Assert.Equal(red.Light.Background, TagControls.SwatchOf(host.Editor, "red-2"));

        host.Services.Theme.Switch(true);

        Assert.Equal(red.Dark.Background, TagControls.SwatchOf(host.Editor, "red-2"));
    }

    [WpfFact]
    public void Clicking_a_color_picks_it_and_only_it()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        Assert.Null(TagControls.PickedColor(host.Editor));

        OpenWindows.Click(TagControls.ColorButton(host.Editor, "blue-1"));
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "blue-3"));

        Assert.Equal("blue-3", TagControls.PickedColor(host.Editor));
    }

    [WpfFact]
    public void Clicking_the_picked_color_unpicks_it()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "blue-1"));
        Assert.Equal("blue-1", TagControls.PickedColor(host.Editor));

        OpenWindows.Click(TagControls.ColorButton(host.Editor, "blue-1"));

        Assert.Null(TagControls.PickedColor(host.Editor));
    }

    [WpfFact]
    public void Typing_a_tag_in_use_shows_its_color_as_picked()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("home");
        host.Services.TagColors.Seed("home", "teal-2");
        host.OpenCustom();

        host.Type("home");

        Assert.Equal("teal-2", TagControls.PickedColor(host.Editor));
    }

    [WpfFact]
    public void Enter_keeps_the_picked_color_before_raising_TagAdded_then_hides_Custom()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        var (tag, tagsThen, keptThen) = Assert.Single(host.Added);
        Assert.Equal("errand", tag);
        Assert.Equal(["mine", "errand"], tagsThen);
        Assert.Equal([("errand", "red-2")], keptThen);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(["mine", "errand"], TagControls.BadgeTags(host.Editor));
    }

    [WpfFact]
    public void Enter_with_no_color_picked_adds_the_tag_and_writes_no_color()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("errand");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Equal(["errand"], host.Editor.Tags);
        Assert.Empty(host.Services.TagColors.Kept);
    }

    [WpfFact]
    public void Without_a_repository_Enter_adds_no_tag_and_Custom_keeps_the_text_and_color_and_points_to_Configure()
    {
        using var host = new TagEditorWindow();
        host.Services.TagColors.Failure = new NoRepositoryFolderException();
        host.OpenCustom();
        host.Type("errand");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Empty(host.Added);
        Assert.Empty(host.Editor.Tags);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.Typed);
        Assert.Equal("red-2", TagControls.PickedColor(host.Editor));
        Assert.Contains(host.Texts, text => text.Contains("Configure", StringComparison.Ordinal));
    }

    [WpfFact]
    public void A_color_that_cant_be_written_adds_no_tag_and_says_why()
    {
        using var host = new TagEditorWindow();
        host.Services.TagColors.Failure = new IOException("disk full");
        host.OpenCustom();
        host.Type("errand");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Empty(host.Added);
        Assert.Contains(host.Texts, text => text.Contains("disk full", StringComparison.Ordinal));
    }

    [WpfFact]
    public void A_tag_the_task_already_has_raises_nothing_but_its_new_color_is_kept()
    {
        using var host = new TagEditorWindow("Home");
        host.OpenCustom();
        host.Type("home");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "green-3"));
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Empty(host.Added);
        Assert.Equal("green-3", host.Services.TagColors.ColorOf("Home"));
    }

    [WpfFact]
    public void Setting_the_tags_leaves_Custom_open()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();

        host.Editor.Tags = ["x"];

        Assert.True(TagControls.CustomIsOpen(host.Editor));
    }

    [WpfFact]
    public void Setting_the_tags_keeps_the_typed_text_and_leaves_no_color_picked_after_the_user_unpicked_the_kept_one()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("home");
        host.Services.TagColors.Seed("home", "teal-2");
        host.OpenCustom();
        host.Type("home");
        Assert.Equal("teal-2", TagControls.PickedColor(host.Editor));
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "teal-2"));
        Assert.Null(TagControls.PickedColor(host.Editor));

        host.Editor.Tags = ["x"];

        Assert.Equal("home", host.Typed);
        Assert.Null(TagControls.PickedColor(host.Editor));
    }

    [WpfFact]
    public void Choosing_a_suggestion_by_click_keeps_the_picked_color_and_hides_Custom()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("home");
        host.OpenCustom();
        host.Type("ho");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));

        EditorFields.ClickSuggestion(host.Editor, "home");

        Assert.Equal(["home"], host.Editor.Tags);
        Assert.Equal([("home", "red-2")], host.Services.TagColors.Kept);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
    }

    [WpfFact]
    public void Backspace_in_the_empty_tag_box_removes_the_last_tag()
    {
        using var host = new TagEditorWindow("a", "b");
        host.OpenCustom();
        host.Focus(EditorFields.Tags);

        host.Press(Key.Back);

        Assert.Equal(["a"], host.Editor.Tags);
        Assert.Equal("b", Assert.Single(host.Removed).Tag);
    }
}
