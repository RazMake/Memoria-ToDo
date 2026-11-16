using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Save button the editor itself shows, under the Tags and the message line, at the bottom right. It asks to save
/// as Ctrl+Enter does; what a save does is the host's and has its own tests. A test sees the button only as an element
/// of the editor, its properties, and where layout puts it.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class EditorSaveButtonTests
{
    [WpfFact]
    public void Clicking_Save_asks_to_save_once_and_nothing_else()
    {
        using var host = new EditorHost();

        OpenWindows.Click(host.SaveButton);

        Assert.Equal((1, 0, 0), host.Counts);
    }

    [WpfFact]
    public void Clicking_Save_adds_the_typed_tag_text_before_it_asks_to_save()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.OpenCustom();
        host.Focus(EditorFields.Tags);
        host.Type(EditorFields.Tags, "errand");
        IReadOnlyList<string>? tagsWhenAsked = null;
        host.Editor.SaveRequested += (_, _) => tagsWhenAsked = host.Editor.Value.Tags;

        OpenWindows.Click(host.SaveButton);

        Assert.Equal(["errand"], tagsWhenAsked);
    }

    [WpfFact]
    public void Save_has_the_accent_look_and_is_not_the_default_button()
    {
        using var host = new EditorHost();
        host.UseTheme(dark: false);

        var save = host.SaveButton;

        Assert.Same(((FrameworkElement)host.Editor).FindResource("AccentButtonStyle"), save.Style);
        Assert.False(save.IsDefault);
    }

    [WpfFact]
    public void Save_has_a_tooltip_that_names_Ctrl_Enter()
    {
        using var host = new EditorHost();

        var tip = host.SaveButton.ToolTip is ToolTip { Content: var content } ? content?.ToString() : host.SaveButton.ToolTip?.ToString();

        Assert.Contains("Ctrl+Enter", tip);
    }

    [WpfFact]
    public void Save_is_at_the_bottom_right_below_the_Tags_and_the_message_line()
    {
        using var host = new EditorHost(height: 600);
        host.Editor.Value = host.Services.Tasks.StartDraft() with { Tags = ["home"] };
        host.ShowMessage("Pick a repository folder.");

        var (save, editorHeight) = host.PlaceOf(host.SaveButton);
        var (plus, _) = host.PlaceOf(TagControls.Plus(host.Editor));
        var (message, _) = host.PlaceOf(host.TextBlockShowing("Pick a repository folder."));

        Assert.True(save.Top >= plus.Bottom, "Save is not below the Tags.");
        Assert.True(save.Top >= message.Bottom, "Save is not below the message line.");
        Assert.True(save.Bottom <= editorHeight, "Save is cut off at the bottom.");
        Assert.True(editorHeight - save.Bottom < 40, "Save is not at the bottom.");
        Assert.True(host.Editor.ActualWidth - save.Right < 40, "Save is not at the right.");
        Assert.True(save.Left > host.Editor.ActualWidth / 2, "Save is not on the right half.");
    }

    [WpfFact]
    public void Tab_reaches_Save_after_the_tags_and_it_is_the_last_stop_in_the_editor()
    {
        using var host = new EditorHost(height: 600);
        host.Editor.Value = host.Services.Tasks.StartDraft() with { Tags = ["home"] };
        host.Layout();
        host.Focus(EditorFields.Description);
        var sequence = new List<DependencyObject>();

        for (var i = 0; i < 30; i++)
        {
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            var focused = (DependencyObject)Keyboard.FocusedElement;
            sequence.Add(focused);
            if (focused == host.SaveButton)
            {
                break;
            }
        }

        var remove = sequence.IndexOf(TagControls.RemoveButton(host.Editor, "home"));
        var plus = sequence.IndexOf(TagControls.Plus(host.Editor));
        var save = sequence.IndexOf(host.SaveButton);
        Assert.True(remove >= 0 && remove < plus && plus < save, "Save does not come after the tags in the tab order.");
        Assert.Equal(sequence.Count - 1, save);
    }

    [WpfTheory]
    [InlineData(false, 1, 0, 0)]
    [InlineData(true, 0, 1, 0)]
    public void Ctrl_Enter_and_Ctrl_Shift_Enter_ask_to_save_while_Save_has_the_focus(bool withShift, int save, int saveAndClear, int cancel)
    {
        using var host = new EditorHost();
        FocusSave(host);

        host.Press(withShift ? [Key.LeftCtrl, Key.LeftShift, Key.Enter] : [Key.LeftCtrl, Key.Enter]);

        Assert.Equal((save, saveAndClear, cancel), host.Counts);
    }

    [WpfFact]
    public void Esc_asks_to_cancel_while_Save_has_the_focus()
    {
        using var host = new EditorHost();
        FocusSave(host);

        host.Press(Key.Escape);

        Assert.Equal((0, 0, 1), host.Counts);
    }

    private static void FocusSave(EditorHost host)
    {
        host.SaveButton.Focus();
        Keyboard.Focus(host.SaveButton);
        Assert.Same(host.SaveButton, Keyboard.FocusedElement);
    }
}
