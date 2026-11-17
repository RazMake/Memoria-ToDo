using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The tag control's keys: Tab through the pill and its x, the keys that open and move through the menu, and the keys in the
/// colors. Backspace in the empty tag box is in the Custom tests, and Esc, which the window and the editor share, is in the Add window's.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagPillKeysTests
{
    [WpfFact]
    public void Tab_reaches_the_pill_and_then_its_x_in_that_order()
    {
        using var host = new TagEditorWindow("a");
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));

        var reached = new List<string?> { TagControls.FocusedId() };
        ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        reached.Add(TagControls.FocusedId());

        Assert.Equal([TagControls.Pill, TagControls.ClearTag], reached);
    }

    [WpfFact]
    public void With_no_tag_the_pill_is_the_one_Tab_stop()
    {
        using var host = new TagEditorWindow();

        Assert.False(TagControls.HasClearButton(host.Editor));
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.Down)]
    public void The_key_on_the_pill_opens_the_menu(Key key)
    {
        using var host = new TagEditorWindow("a");
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));

        host.Press(key);

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void Enter_or_Space_on_the_x_clears_the_tag_without_opening_the_menu(Key key)
    {
        using var host = new TagEditorWindow("work");
        TagEditorWindow.Focus(TagControls.ClearButton(host.Editor));

        host.Press(key);

        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.Null(host.Editor.TaskTag);
        var (tag, _, _) = Assert.Single(host.Changes);
        Assert.Null(tag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Down_moves_through_the_menu_and_Enter_chooses_the_tag_moved_to()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);

        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Enter);

        Assert.Equal("b", host.Editor.TaskTag);
        Assert.Equal("b", Assert.Single(host.Changes).Tag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Up_moves_back_through_the_menu()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);

        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Up);
        host.Press(Key.Enter);

        Assert.Equal("a", host.Editor.TaskTag);
    }

    [WpfFact]
    public void Enter_on_the_Custom_item_opens_Custom_with_the_cursor_in_the_tag_box()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);

        for (var i = 0; i < 5; i++)
        {
            host.Press(Key.Down);
        }

        host.Press(Key.Enter);

        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
        Assert.Null(host.Editor.TaskTag);
    }

    [WpfFact]
    public void Down_to_Search_puts_the_cursor_in_its_box_and_Enter_chooses_the_first_match()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        for (var i = 0; i < 4; i++)
        {
            host.Press(Key.Down);
        }

        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
        TextFields.Type(TagControls.SearchBox(host.Editor), "b");
        host.Press(Key.Enter);

        Assert.Equal("b", host.Editor.TaskTag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void A_letter_typed_in_the_menu_outside_Search_goes_into_the_Search_box()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        host.Press(Key.Down);

        EditorFields.TypeOnFocused("c");

        Assert.Equal("c", TagControls.SearchBox(host.Editor).Text);
        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
    }

    [WpfFact]
    public void Tab_moves_from_the_New_tag_box_to_the_colors()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("errand");
        host.Focus(EditorFields.Tags);

        host.Press(Key.Tab);

        Assert.StartsWith("Color ", TagControls.FocusedId());
    }

    [WpfFact]
    public void In_the_colors_the_arrow_keys_move_and_Space_chooses_the_tag_in_the_color_moved_to()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");
        TagEditorWindow.Focus(TagControls.ColorButton(host.Editor, "red-1"));

        host.Press(Key.Right);
        Assert.Equal("Color orange-1", TagControls.FocusedId());

        host.Press(Key.Down);
        Assert.Equal("Color orange-2", TagControls.FocusedId());

        host.Press(Key.Left);
        Assert.Equal("Color red-2", TagControls.FocusedId());

        host.Press(Key.Up);
        Assert.Equal("Color red-1", TagControls.FocusedId());

        host.Press(Key.Right);
        host.Press(Key.Down);
        host.Press(Key.Space);

        Assert.Equal("errand", host.Editor.TaskTag);
        Assert.Equal([("errand", "orange-2")], host.Services.TagColors.Kept);
    }

    [WpfFact]
    public void Enter_on_a_color_chooses_the_tag_in_that_color()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        host.Type("errand");
        TagEditorWindow.Focus(TagControls.ColorButton(host.Editor, "blue-3"));

        host.Press(Key.Enter);

        Assert.Equal("errand", host.Editor.TaskTag);
        Assert.Equal([("errand", "blue-3")], host.Services.TagColors.Kept);
    }

    private static TagEditorWindow ThreeTagsInUse()
    {
        var host = new TagEditorWindow();
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");
        return host;
    }
}
