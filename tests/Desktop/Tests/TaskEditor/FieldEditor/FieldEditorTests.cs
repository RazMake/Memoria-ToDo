using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.FieldEditor;

/// <summary>
/// The Title editor and the Description editor the Task list uses to edit one field in place, as a window that hosts them shows
/// them. Each test follows one path. Their keys, styling, and snippets come from the same part as Add's fields, whose rules the
/// Add window's tests check. Save results, change reports, and image widths are in the unit tests.
/// </summary>
[UX("FieldEditor")]
[Collection(ScreenTests.Collection)]
public class FieldEditorTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));
    private static readonly string LongText = string.Join("\n\n", Enumerable.Range(1, 200).Select(i => $"line {i}"));

    [WpfFact]
    public void The_Description_editor_shows_its_value_and_its_keys_and_Save_button_ask_to_save_or_cancel()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.UseTheme(dark: false);
        host.Editor.TaskId = "task-7";
        Assert.Equal("Two litres", host.Editor.Value);
        Assert.Equal("Two litres", host.Text);
        Assert.Equal("task-7", host.Editor.TaskId);

        var save = host.SaveButton;
        Assert.Same(host.Editor.FindResource("AccentButtonStyle"), save.Style);
        Assert.False(save.IsDefault);
        var tip = save.ToolTip is ToolTip { Content: var content } ? content?.ToString() : save.ToolTip?.ToString();
        Assert.Contains("Ctrl+Enter", tip);

        host.Edit("Two litres!");
        Assert.True(host.Editor.HasChanges);
        host.Edit("Two litres");
        Assert.False(host.Editor.HasChanges);

        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal((1, 0), (host.Saves, host.Cancels));

        host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);
        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal(1, host.Saves);
        host.Press(Key.LeftCtrl, Key.S);
        Assert.Equal(2, host.Saves);

        host.Press(Key.Enter);
        Assert.Equal((2, 0), (host.Saves, host.Cancels));
        Assert.True(host.FieldHasFocus);
        var text = host.Text;
        host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);
        Assert.Equal((2, 0), (host.Saves, host.Cancels));
        Assert.Equal(text, host.Text);

        host.Press(Key.Escape);
        Assert.Equal((2, 1), (host.Saves, host.Cancels));

        OpenWindows.Click(host.SaveButton);
        Assert.Equal((3, 1), (host.Saves, host.Cancels));

        host.Layout();
        host.Focus();
        ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        Assert.Same(host.SaveButton, Keyboard.FocusedElement);
        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal((4, 1), (host.Saves, host.Cancels));
        host.Press(Key.Escape);
        Assert.Equal((4, 2), (host.Saves, host.Cancels));

        host.Layout();
        OpenWindows.UseTheKeyboard();
        Keyboard.Focus(host.SaveButton);
        host.Layout();
        Assert.True(OpenWindows.DrawsFocusOutline(host.SaveButton), "No outline frames Save.");
    }

    [WpfFact]
    public void Typing_in_the_Description_editor_opens_the_snippet_list_styles_the_text_and_keeps_pasted_images()
    {
        using var host = FieldEditorHost.OpenDescription("a b c");
        host.Edit("a b c", 2, 1);
        host.Press(Key.LeftCtrl, Key.B);
        Assert.Equal("a **b** c", host.Text);

        host.Edit("Plan", 0);
        host.Press(Key.LeftCtrl, Key.D2);
        Assert.Equal("## Plan", host.Text);

        host.Edit("", 0);
        host.Type("/");
        Assert.NotEmpty(EditorFields.ShownSnippets(host.Editor));
        host.Press(Key.Escape);
        Assert.Empty(EditorFields.ShownSnippets(host.Editor));
        Assert.Equal(0, host.Cancels);
        host.Press(Key.Escape);
        Assert.Equal(1, host.Cancels);

        host.Edit("ab", 1);
        host.Services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        host.Press(Key.LeftCtrl, Key.V);
        var link = Assert.Single(ImageLink.Matches(host.Text));
        Assert.Equal("a" + link.Value + "b", host.Text);
        Assert.Contains(host.Editor.TaskId, link.Groups[2].Value);
        Assert.True(host.Services.Files.FileExists(host.Services.DraftImagePath(link.Groups[2].Value)));

        host.Edit("![a](images/a.png) ![b](images/b.png)");
        host.Editor.SetImageWidth(1, 200);
        Assert.Equal("![a](images/a.png) ![b|200](images/b.png)", host.Text);
        Assert.True(EditorFields.Undo(host.Editor, EditorFields.Description));
        Assert.Equal("![a](images/a.png) ![b](images/b.png)", host.Text);

        host.Edit("first line\nsecond line");
        host.Editor.FocusEnd();
        Assert.True(host.FieldHasFocus);
        Assert.Equal((host.Text.Length, 0), host.Selection);

        host.Edit("ab", 1);
        host.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");
        host.Press(Key.LeftCtrl, Key.V);
        Assert.True(host.ShowsText("image"));
        Assert.Equal("ab", host.Text);
    }

    [WpfFact]
    public void The_Description_editor_fills_the_height_it_is_given_and_shows_a_failed_save_above_Save()
    {
        using var host = FieldEditorHost.OpenDescription(height: 900);
        var description = EditorFields.Field(host.Editor, EditorFields.Description);
        var (filled, _) = host.PlaceOf(description);
        var (save, _) = host.PlaceOf(host.SaveButton);
        Assert.True(filled.Height > 400, $"The Description is only {filled.Height} tall.");
        Assert.True(save.Top - filled.Bottom < 60, "The Description stops far above Save.");

        host.Editor.Value = LongText;
        var (scrolling, editorHeight) = host.PlaceOf(description);
        Assert.Equal(filled.Height, scrolling.Height, 0.5);
        Assert.True(host.PlaceOf(host.SaveButton).Place.Bottom <= editorHeight, "Save is pushed out of the editor.");

        host.Editor.Value = "Oat milk";
        var before = host.PlaceOf(description).Place.Height;
        Assert.False(host.Editor.Save(_ => throw new IOException("The disk is full.")));
        Assert.True(host.ShowsText("The disk is full."));
        var (shorter, height) = host.PlaceOf(description);
        var (below, _) = host.PlaceOf(host.SaveButton);
        var message = host.PlaceOf(host.TextBlockShowing(host.Texts.Single(text => text.Contains("disk is full", StringComparison.OrdinalIgnoreCase)))).Place;
        Assert.True(shorter.Height < before, "The Description did not give way to the message line.");
        Assert.True(below.Bottom <= height, "Save is pushed out of the editor.");
        Assert.True(below.Top >= shorter.Bottom, "Save is not below the Description.");
        Assert.True(below.Top >= message.Bottom, "Save is not below the message line.");
        Assert.True(height - below.Bottom < 40, "Save is not at the bottom.");
        Assert.True(host.Editor.ActualWidth - below.Right < 40, "Save is not at the right.");
        Assert.True(below.Left > host.Editor.ActualWidth / 2, "Save is not on the right half.");

        Assert.True(host.Editor.Save(_ => { }));
        Assert.False(host.ShowsText("The disk is full."));
        Assert.False(host.Editor.Save(_ => throw new TaskNotFoundException()));
        Assert.True(host.ShowsText("no longer exists"));
    }

    [WpfFact]
    public void The_Title_editor_keeps_the_title_on_one_line_and_its_keys_ask_to_save_or_cancel()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        Assert.Equal("Buy milk", host.Editor.Value);
        Assert.Equal("Buy milk", host.Text);
        host.Edit("Buy oat milk");
        Assert.Equal("Buy oat milk", host.Editor.Value);
        Assert.True(host.Editor.HasChanges);
        host.Edit("Buy milk", 8);

        host.Press(Key.Enter);
        Assert.Equal((1, 0), (host.Saves, host.Cancels));
        Assert.Equal("Buy milk", host.Text);

        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal((2, 0), (host.Saves, host.Cancels));

        host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);
        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal(2, host.Saves);
        host.Press(Key.LeftCtrl, Key.S);
        Assert.Equal(3, host.Saves);

        host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);
        Assert.Equal((3, 0), (host.Saves, host.Cancels));
        Assert.Equal("Buy milk", host.Text);

        host.Press(Key.Escape);
        Assert.Equal((3, 1), (host.Saves, host.Cancels));

        host.Edit("do urgent now", 3, 6);
        host.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        Assert.NotEmpty(EditorFields.ShownColors(host.Editor));
        host.Press(Key.Escape);
        Assert.Empty(EditorFields.ShownColors(host.Editor));
        Assert.Equal(1, host.Cancels);
        host.Press(Key.Escape);
        Assert.Equal(2, host.Cancels);

        host.Edit("a b c", 2, 1);
        host.Press(Key.LeftCtrl, Key.B);
        Assert.Equal("a **b** c", host.Text);

        var textArea = ((TextEditor)EditorFields.Field(host.Editor, EditorFields.Title)).TextArea;
        var args = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "first\r\nsecond"), false, DataFormats.UnicodeText)
        {
            RoutedEvent = DataObject.PastingEvent,
        };
        textArea.RaiseEvent(args);
        Assert.Equal("first second", args.DataObject.GetData(DataFormats.UnicodeText));

        host.Edit("Buy milk");
        host.Editor.FocusEnd();
        Assert.True(host.FieldHasFocus);
        Assert.Equal((8, 0), host.Selection);
    }

    [WpfFact]
    public void Saving_the_Title_marks_an_empty_title_and_shows_a_failed_save_under_the_field()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Edit("Buy oat milk");
        string? saved = null;
        Assert.True(host.Editor.Save(title => saved = title));
        Assert.Equal("Buy oat milk", saved);
        Assert.Empty(host.Texts);

        foreach (var empty in new[] { "", "   " })
        {
            host.Edit(empty);
            Assert.False(host.Editor.Save(_ => throw new InvalidOperationException("Saved an empty title.")));
            Assert.True(host.FieldIsMarked);
            Assert.Equal(empty, host.Text);
        }

        host.Edit("Buy milk");
        Assert.False(host.FieldIsMarked);

        Assert.False(host.Editor.Save(_ => throw new IOException("The disk is full.")));
        Assert.True(host.ShowsText("The disk is full."));
        Assert.Equal("Buy milk", host.Text);
        Assert.Equal("Buy milk", host.Editor.Value);

        Assert.True(host.Editor.Save(_ => { }));
        Assert.False(host.ShowsText("The disk is full."));
        Assert.False(host.Editor.Save(_ => throw new TaskNotFoundException()));
        Assert.True(host.ShowsText("no longer exists"));
    }
}
