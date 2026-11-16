using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Save button the Description editor shows, under the Description and the message line, at the bottom right. It
/// asks to save as the Save shortcut does; what a save does is the host's and has its own tests. A test sees the button
/// only as an element of the editor, its properties, and where layout puts it.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class DescriptionEditorSaveButtonTests
{
    [WpfFact]
    public void Clicking_Save_asks_to_save_once_and_nothing_else()
    {
        using var host = FieldEditorHost.OpenDescription("Oat milk");

        OpenWindows.Click(host.SaveButton);

        Assert.Equal((1, 0), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void Save_has_the_accent_look_and_is_not_the_default_button()
    {
        using var host = FieldEditorHost.OpenDescription();
        host.UseTheme(dark: false);

        var save = host.SaveButton;

        Assert.Same(host.Editor.FindResource("AccentButtonStyle"), save.Style);
        Assert.False(save.IsDefault);
    }

    [WpfFact]
    public void Save_has_a_tooltip_that_names_the_Save_shortcut()
    {
        using var host = FieldEditorHost.OpenDescription();

        var tip = host.SaveButton.ToolTip is ToolTip { Content: var content } ? content?.ToString() : host.SaveButton.ToolTip?.ToString();

        Assert.Contains("Ctrl+Enter", tip);
    }

    [WpfFact]
    public void Save_is_at_the_bottom_right_below_the_Description_and_the_message_line()
    {
        using var host = FieldEditorHost.OpenDescription("Oat milk", height: 600);
        host.Editor.Save(_ => throw new IOException("The disk is full."));

        var (save, editorHeight) = host.PlaceOf(host.SaveButton);
        var (description, _) = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description));
        var message = host.PlaceOf(host.TextBlockShowing(host.Texts.Single(text => text.Contains("disk is full", StringComparison.OrdinalIgnoreCase)))).Place;

        Assert.True(save.Top >= description.Bottom, "Save is not below the Description.");
        Assert.True(save.Top >= message.Bottom, "Save is not below the message line.");
        Assert.True(save.Bottom <= editorHeight, "Save is cut off at the bottom.");
        Assert.True(editorHeight - save.Bottom < 40, "Save is not at the bottom.");
        Assert.True(host.Editor.ActualWidth - save.Right < 40, "Save is not at the right.");
        Assert.True(save.Left > host.Editor.ActualWidth / 2, "Save is not on the right half.");
    }

    [WpfFact]
    public void Tab_from_the_Description_reaches_Save()
    {
        using var host = FieldEditorHost.OpenDescription("Oat milk");
        host.Layout();

        ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

        Assert.Same(host.SaveButton, Keyboard.FocusedElement);
    }

    [WpfFact]
    public void Save_draws_a_focus_outline_when_the_keyboard_moves_the_focus_to_it()
    {
        using var host = FieldEditorHost.OpenDescription();
        host.Layout();
        OpenWindows.UseTheKeyboard();

        Keyboard.Focus(host.SaveButton);
        host.Layout();

        Assert.True(OpenWindows.DrawsFocusOutline(host.SaveButton), "No outline frames Save.");
    }

    [WpfFact]
    public void The_Save_shortcut_asks_to_save_while_Save_has_the_focus()
    {
        using var host = FieldEditorHost.OpenDescription();
        FocusSave(host);

        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal((1, 0), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void Esc_asks_to_cancel_while_Save_has_the_focus()
    {
        using var host = FieldEditorHost.OpenDescription();
        FocusSave(host);

        host.Press(Key.Escape);

        Assert.Equal((0, 1), (host.Saves, host.Cancels));
    }

    private static void FocusSave(FieldEditorHost<DescriptionEditor> host)
    {
        host.SaveButton.Focus();
        Keyboard.Focus(host.SaveButton);
        Assert.Same(host.SaveButton, Keyboard.FocusedElement);
    }
}