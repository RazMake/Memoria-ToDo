using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The two calls a window around the tag control makes while the control joins its edit: <c>CloseEdit</c>, which Esc uses to
/// close the menu, then Custom, one per call, and <c>ChooseTypedText</c>, which the edit's save uses to choose what is typed
/// in Custom as Enter there does.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagPillEditHooksTests
{
    [WpfFact]
    public void CloseEdit_returns_false_and_changes_nothing_when_neither_the_menu_nor_Custom_is_open()
    {
        using var host = new TagEditorWindow("home");

        Assert.False(host.Editor.CloseEdit());

        Assert.Equal("home", host.Editor.TaskTag);
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void CloseEdit_closes_the_open_menu_and_returns_true()
    {
        using var host = new TagEditorWindow();
        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));

        Assert.True(host.Editor.CloseEdit());

        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void CloseEdit_closes_Custom_and_drops_what_was_typed_there()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("half");

        Assert.True(host.Editor.CloseEdit());

        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Empty(host.Changes);
        host.OpenCustom();
        Assert.Equal("", host.Typed);
    }

    [WpfFact]
    public void CloseEdit_closes_the_menu_before_Custom_one_part_per_call()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("half");
        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));

        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));

        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.CustomIsOpen(host.Editor));

        Assert.False(host.Editor.CloseEdit());
    }

    [WpfFact]
    public void ChooseTypedText_chooses_the_text_typed_in_Custom_as_the_tag_as_Enter_does()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");

        host.Editor.ChooseTypedText();

        var (tag, tagThen, _) = Assert.Single(host.Changes);
        Assert.Equal("errand", tag);
        Assert.Equal("errand", tagThen);
        Assert.Equal("errand", host.Editor.TaskTag);
        Assert.Equal("errand", host.PillText);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
    }

    [WpfFact]
    public void ChooseTypedText_does_nothing_when_Custom_is_closed()
    {
        using var host = new TagEditorWindow("home");

        host.Editor.ChooseTypedText();

        Assert.Equal("home", host.Editor.TaskTag);
        Assert.Empty(host.Changes);
    }

    [WpfFact]
    public void ChooseTypedText_does_nothing_when_Custom_s_box_is_empty()
    {
        using var host = new TagEditorWindow("home");
        host.OpenCustom();

        host.Editor.ChooseTypedText();

        Assert.Equal("home", host.Editor.TaskTag);
        Assert.Empty(host.Changes);
    }
}
