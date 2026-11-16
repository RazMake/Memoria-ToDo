using System.IO;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Title editor the Task list uses to edit a title in place: its keys, requests, save steps, and change report, on the
/// common path. Its keys, styling, and snippets come from the same part as Add's Title, whose rules are tested there.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TitleEditorTests
{
    [WpfFact]
    public void Shows_the_value_it_was_given()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        Assert.Equal("Buy milk", host.Editor.Value);
        Assert.Equal("Buy milk", host.Text);
    }

    [WpfFact]
    public void Value_follows_what_is_typed()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Edit("Buy oat milk");

        Assert.Equal("Buy oat milk", host.Editor.Value);
    }

    [WpfFact]
    public void HasChanges_is_false_until_the_text_differs_from_the_value_last_set()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        Assert.False(host.Editor.HasChanges);

        host.Edit("Buy milk!");
        Assert.True(host.Editor.HasChanges);

        host.Edit("Buy milk");
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Setting_the_value_starts_HasChanges_over()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Edit("Buy tea");

        host.Editor.Value = "Buy tea";

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void HasChanges_counts_a_change_of_case_or_of_a_space()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Edit("buy milk");
        Assert.True(host.Editor.HasChanges);

        host.Edit("Buy milk ");
        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Enter_asks_to_save_once_and_keeps_the_title_on_one_line()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Edit("Buy milk", 8);

        host.Press(Key.Enter);

        Assert.Equal((1, 0), (host.Saves, host.Cancels));
        Assert.Equal("Buy milk", host.Text);
    }

    [WpfFact]
    public void The_Save_shortcut_asks_to_save_once_and_nothing_else()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal((1, 0), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void A_new_Save_shortcut_applies_to_the_open_editor()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);

        host.Press(Key.LeftCtrl, Key.S);

        Assert.Equal(1, host.Saves);
    }

    [WpfFact]
    public void The_old_Save_shortcut_stops_saving_once_it_is_changed()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);

        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal(0, host.Saves);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_does_nothing_since_there_is_no_save_and_clear()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.Equal((0, 0), (host.Saves, host.Cancels));
        Assert.Equal("Buy milk", host.Text);
    }

    [WpfFact]
    public void Esc_asks_to_cancel_once_and_nothing_else()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Press(Key.Escape);

        Assert.Equal((0, 1), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void Esc_closes_an_open_color_list_first_and_only_the_next_Esc_asks_to_cancel()
    {
        using var host = FieldEditorHost.OpenTitle("do urgent now");
        host.Edit("do urgent now", 3, 6);
        host.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        Assert.NotEmpty(EditorFields.ShownColors(host.Editor));

        host.Press(Key.Escape);

        Assert.Empty(EditorFields.ShownColors(host.Editor));
        Assert.Equal(0, host.Cancels);

        host.Press(Key.Escape);

        Assert.Equal(1, host.Cancels);
    }

    [WpfFact]
    public void Ctrl_B_wraps_the_selection_in_bold_markers_as_in_Add()
    {
        using var host = FieldEditorHost.OpenTitle("a b c");
        host.Edit("a b c", 2, 1);

        host.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a **b** c", host.Text);
    }

    [WpfFact]
    public void Pasted_lines_are_joined_with_spaces_into_one_line()
    {
        using var host = FieldEditorHost.OpenTitle();
        var textArea = ((TextEditor)EditorFields.Field(host.Editor, EditorFields.Title)).TextArea;
        var args = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "first\r\nsecond"), false, DataFormats.UnicodeText)
        {
            RoutedEvent = DataObject.PastingEvent,
        };

        textArea.RaiseEvent(args);

        Assert.Equal("first second", args.DataObject.GetData(DataFormats.UnicodeText));
    }

    [WpfFact]
    public void FocusEnd_puts_the_cursor_at_the_end_of_the_title()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        host.Editor.FocusEnd();

        Assert.True(host.FieldHasFocus);
        Assert.Equal((8, 0), host.Selection);
    }

    [WpfFact]
    public void Save_calls_the_function_with_the_title_and_returns_true()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Edit("Buy oat milk");
        string? saved = null;

        var result = host.Editor.Save(title => saved = title);

        Assert.True(result);
        Assert.Equal("Buy oat milk", saved);
        Assert.Empty(host.Texts);
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("   ")]
    public void Save_refuses_an_empty_title_marks_it_and_calls_nothing(string title)
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Edit(title);
        var called = false;

        var result = host.Editor.Save(_ => called = true);

        Assert.False(result);
        Assert.False(called);
        Assert.True(host.FieldIsMarked);
        Assert.Equal(title, host.Text);
    }

    [WpfFact]
    public void The_mark_on_an_empty_title_goes_when_the_title_is_edited()
    {
        using var host = FieldEditorHost.OpenTitle();
        host.Editor.Save(_ => { });
        Assert.True(host.FieldIsMarked);

        host.Edit("Buy milk");

        Assert.False(host.FieldIsMarked);
    }

    [WpfFact]
    public void A_known_save_failure_is_shown_under_the_field_and_the_text_is_kept()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        var result = host.Editor.Save(_ => throw new IOException("The disk is full."));

        Assert.False(result);
        Assert.True(host.ShowsText("The disk is full."));
        Assert.Equal("Buy milk", host.Text);
        Assert.Equal("Buy milk", host.Editor.Value);
    }

    [WpfFact]
    public void A_task_that_no_longer_exists_says_to_copy_the_changes()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        var result = host.Editor.Save(_ => throw new TaskNotFoundException());

        Assert.False(result);
        Assert.True(host.ShowsText("no longer exists"));
    }

    [WpfFact]
    public void A_failure_Tasks_is_not_known_to_throw_propagates()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");

        Assert.Throws<InvalidOperationException>(() => host.Editor.Save(_ => throw new InvalidOperationException("bug")));
    }

    [WpfFact]
    public void A_save_that_works_after_a_failed_one_clears_the_message()
    {
        using var host = FieldEditorHost.OpenTitle("Buy milk");
        host.Editor.Save(_ => throw new IOException("The disk is full."));

        host.Editor.Save(_ => { });

        Assert.False(host.ShowsText("The disk is full."));
    }
}
