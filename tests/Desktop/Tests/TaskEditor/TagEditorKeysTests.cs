using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The tag editor's keys: Tab through the buttons, the keys that open and move through the menu, and the keys in the colors.
/// Backspace in the empty tag box is in the Custom tests, and Esc, which the window and the editor share, is in the Add window's.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagEditorKeysTests
{
    [WpfFact]
    public void Tab_reaches_each_remove_button_in_order_then_the_plus()
    {
        using var host = new TagEditorWindow("a", "b");
        TagEditorWindow.Focus(TagControls.RemoveButton(host.Editor, "a"));

        var reached = new List<string?> { TagControls.FocusedId() };
        for (var i = 0; i < 2; i++)
        {
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            reached.Add(TagControls.FocusedId());
        }

        Assert.Equal(["Remove a", "Remove b", TagControls.AddTag], reached);
    }

    [WpfTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.Down)]
    public void The_key_on_the_plus_opens_the_menu(Key key)
    {
        using var host = new TagEditorWindow();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));

        host.Press(key);

        Assert.True(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Down_moves_through_the_menu_and_Enter_adds_the_tag_moved_to()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));
        host.Press(Key.Enter);

        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Enter);

        Assert.Equal(["b"], host.Editor.Tags);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
    }

    [WpfFact]
    public void Up_moves_back_through_the_menu()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));
        host.Press(Key.Enter);

        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Up);
        host.Press(Key.Enter);

        Assert.Equal(["a"], host.Editor.Tags);
    }

    [WpfFact]
    public void Enter_on_the_Custom_item_opens_Custom_with_the_cursor_in_the_tag_box()
    {
        using var host = ThreeTagsInUse();
        TagEditorWindow.Focus(TagControls.Plus(host.Editor));
        host.Press(Key.Enter);

        for (var i = 0; i < 4; i++)
        {
            host.Press(Key.Down);
        }

        host.Press(Key.Enter);

        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
        Assert.Empty(host.Editor.Tags);
    }

    [WpfFact]
    public void In_the_colors_the_arrow_keys_move_and_Space_picks()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
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

        Assert.Equal("orange-2", TagControls.PickedColor(host.Editor));
    }

    [WpfFact]
    public void Space_on_the_picked_color_unpicks_it_and_on_an_unpicked_color_picks_it()
    {
        using var host = new TagEditorWindow();
        host.OpenCustom();
        TagEditorWindow.Focus(TagControls.ColorButton(host.Editor, "red-1"));

        host.Press(Key.Space);
        Assert.Equal("red-1", TagControls.PickedColor(host.Editor));

        host.Press(Key.Space);
        Assert.Null(TagControls.PickedColor(host.Editor));

        host.Press(Key.Space);
        Assert.Equal("red-1", TagControls.PickedColor(host.Editor));
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
