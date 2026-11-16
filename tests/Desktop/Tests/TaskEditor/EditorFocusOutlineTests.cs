using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// No focus outline frames the editor or its Title and Description when the keyboard moves the focus there; the buttons
/// and the tag badges keep theirs. WPF draws an outline as an adorner over the focused element, and only when the last
/// input came from the keyboard, so a test sends a key through the input system, moves the focus, and looks for the adorner.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class EditorFocusOutlineTests
{
    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    public void No_focus_outline_is_drawn_when_the_keyboard_moves_the_focus_to_a_text_field(string field)
    {
        using var host = new EditorHost();
        host.UseTheme(dark: false);
        UseTheKeyboard(host);

        var element = (UIElement)EditorFields.Field(host.Editor, field);
        Keyboard.Focus(host.SaveButton); // Away from where the host put the focus, so the move to the field is a change.
        Keyboard.Focus(element);

        Assert.False(DrawsFocusOutline(host, element), "An outline frames the field.");
        Assert.False(DrawsFocusOutline(host, (UIElement)Keyboard.FocusedElement), "An outline frames the element that took the focus.");
        Assert.False(OpenWindows.Descendants(host.Editor).OfType<UIElement>().Any(e => DrawsFocusOutline(host, e)), "An outline frames a part of the editor.");
    }

    [WpfFact]
    public void The_tag_plus_button_draws_a_focus_outline_when_the_keyboard_moves_the_focus_to_it()
    {
        using var host = new EditorHost();

        Assert.True(OutlineAfterKeyboardFocus(host, TagControls.Plus(host.Editor)), "No outline frames +.");
    }

    [WpfFact]
    public void A_tag_badge_button_draws_a_focus_outline_when_the_keyboard_moves_the_focus_to_it()
    {
        using var host = new EditorHost();
        host.Editor.Value = host.Services.Tasks.StartDraft() with { Tags = ["home"] };
        host.Layout();

        Assert.True(OutlineAfterKeyboardFocus(host, TagControls.RemoveButton(host.Editor, "home")), "No outline frames the badge button.");
    }

    [WpfFact]
    public void Save_draws_a_focus_outline_when_the_keyboard_moves_the_focus_to_it()
    {
        using var host = new EditorHost();

        Assert.True(OutlineAfterKeyboardFocus(host, host.SaveButton), "No outline frames Save.");
    }

    private static bool OutlineAfterKeyboardFocus(EditorHost host, UIElement element)
    {
        UseTheKeyboard(host);
        Keyboard.Focus(element);
        return DrawsFocusOutline(host, element);
    }

    private static void UseTheKeyboard(EditorHost host)
    {
        host.Layout();
        OpenWindows.UseTheKeyboard();
    }

    private static bool DrawsFocusOutline(EditorHost host, UIElement element)
    {
        host.Layout();
        return OpenWindows.DrawsFocusOutline(element);
    }

    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    public void Ctrl_Tab_in_a_field_is_not_taken_by_the_editors_keys(string field)
    {
        using var host = new EditorHost();
        host.Focus(field);
        var target = (UIElement)Keyboard.FocusedElement;
        var takenBeforeTheField = false;
        target.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => takenBeforeTheField |= e.Key == Key.Tab && e.Handled), handledEventsToo: true);

        SimulatedKey.Raise(target, Key.LeftCtrl, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
        SimulatedKey.Raise(target, Key.Tab, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

        Assert.False(takenBeforeTheField, "The editor's keys took Ctrl+Tab, so WPF can't move to the next control.");
    }
}
