using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Custom on screen, on the common paths: type a tag and click a color, or press Enter. Either replaces the task's tag.
/// When the colors show, which one is marked, when a color is kept, and what a failure leaves are in the unit tests of the
/// tags model; these check the control is wired to it, and how the colors are laid out.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagPillCustomTests
{
    [WpfFact]
    public void Opening_Custom_puts_the_cursor_in_the_New_tag_box()
    {
        using var host = new TagEditorWindow();

        host.OpenCustom();

        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
    }

    [WpfFact]
    public void The_colors_show_only_while_the_box_holds_text()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        Assert.False(TagControls.ColorsAreShowing(host.Editor));

        host.Type("errand");
        Assert.True(TagControls.ColorsAreShowing(host.Editor));

        host.Type("");
        Assert.False(TagControls.ColorsAreShowing(host.Editor));
    }

    [WpfFact]
    public void Shows_the_30_colors_in_Renderings_order()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("errand");

        Assert.Equal(TagColors.All.Select(color => color.Name), TagControls.ColorButtons(host.Editor).Select(color => color.Name));
    }

    [WpfFact]
    public void Lays_the_colors_out_as_ten_columns_of_hues_and_three_rows_of_shades()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("errand");
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
        host.Type("errand");
        var red = TagColors.Find("red-2")!;
        Assert.Equal(red.Light.Background, TagControls.SwatchOf(host.Editor, "red-2"));

        host.Services.Theme.Switch(true);

        Assert.Equal(red.Dark.Background, TagControls.SwatchOf(host.Editor, "red-2"));
    }

    [WpfFact]
    public void Marks_the_color_a_tag_in_use_has_now()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("home");
        host.Services.TagColors.Seed("home", "teal-2");
        host.OpenCustom();

        host.Type("home");

        Assert.Equal("teal-2", TagControls.MarkedColor(host.Editor));
    }

    [WpfFact]
    public void Clicking_a_color_keeps_it_for_the_tag_before_TagChanged_then_replaces_the_tag_hides_Custom_and_returns_the_focus_to_the_pill()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");

        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));

        var (tag, tagThen, keptThen) = Assert.Single(host.Changes);
        Assert.Equal("errand", tag);
        Assert.Equal("errand", tagThen);
        Assert.Equal([("errand", "red-2")], keptThen);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.PillText);
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfFact]
    public void Enter_in_the_box_replaces_the_tag_and_writes_no_color()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Equal("errand", host.Editor.TaskTag);
        Assert.Equal("errand", Assert.Single(host.Changes).Tag);
        Assert.Empty(host.Services.TagColors.Kept);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
    }

    [WpfFact]
    public void Enter_in_the_box_spells_the_tag_as_the_matching_tag_in_use()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("Work");
        host.OpenCustom();
        host.Type("WORK");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Equal("Work", host.Editor.TaskTag);
    }

    [WpfFact]
    public void Choosing_the_current_tag_in_Custom_raises_nothing_ignoring_case()
    {
        using var host = new TagEditorWindow("Work");
        host.OpenCustom();
        host.Type("work");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.Equal("Work", host.Editor.TaskTag);
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void Choosing_the_current_tag_in_Custom_hides_Custom_and_the_pill_keeps_the_tag()
    {
        using var host = new TagEditorWindow("Work");
        host.OpenCustom();
        host.Type("work");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Enter);

        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("Work", host.PillText);
    }

    [WpfFact]
    public void A_color_that_cant_be_kept_chooses_no_tag_and_Custom_keeps_the_text_and_says_why_under_the_colors()
    {
        using var host = new TagEditorWindow("mine");
        host.Services.TagColors.Failure = new NoRepositoryFolderException();
        host.OpenCustom();
        host.Type("errand");

        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));

        Assert.Empty(host.Changes);
        Assert.Equal("mine", host.Editor.TaskTag);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.Typed);
        Assert.Contains(host.Texts, text => text.Contains("Configure", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Setting_the_tag_leaves_Custom_open()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();

        host.Editor.TaskTag = "x";

        Assert.True(TagControls.CustomIsOpen(host.Editor));
    }

    [WpfFact]
    public void Backspace_in_the_empty_tag_box_does_nothing()
    {
        using var host = new TagEditorWindow("a");
        host.OpenCustom();
        host.Focus(EditorFields.Tags);

        host.Press(Key.Back);

        Assert.Equal("a", host.Editor.TaskTag);
        Assert.Equal("a", host.PillText);
        Assert.Empty(host.Changes);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
    }
}
