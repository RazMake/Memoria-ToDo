using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Description editor the Task list uses to edit a description alone: its keys, images, requests, save steps, and change
/// report, on the common path. Its keys, styling, and snippets come from the same part as Add's Description, whose rules
/// are tested there.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class DescriptionEditorTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    [WpfFact]
    public void Shows_the_value_and_the_task_id_it_was_given()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Editor.TaskId = "task-7";

        Assert.Equal("Two litres", host.Editor.Value);
        Assert.Equal("Two litres", host.Text);
        Assert.Equal("task-7", host.Editor.TaskId);
    }

    [WpfFact]
    public void HasChanges_is_false_until_the_text_differs_from_the_value_last_set()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        Assert.False(host.Editor.HasChanges);

        host.Edit("Two litres!");
        Assert.True(host.Editor.HasChanges);

        host.Edit("Two litres");
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Setting_the_value_starts_HasChanges_over()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Edit("Three litres");

        host.Editor.Value = "Three litres";

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void The_Save_shortcut_asks_to_save_once_and_nothing_else()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal((1, 0), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void A_new_Save_shortcut_applies_to_the_open_editor_and_the_old_one_stops_saving()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);

        host.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal(0, host.Saves);

        host.Press(Key.LeftCtrl, Key.S);
        Assert.Equal(1, host.Saves);
    }

    [WpfFact]
    public void Enter_breaks_the_line_and_does_not_ask_to_save()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        host.Press(Key.Enter);

        Assert.Equal((0, 0), (host.Saves, host.Cancels));
        Assert.True(host.FieldHasFocus);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_does_nothing_since_there_is_no_save_and_clear()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.Equal((0, 0), (host.Saves, host.Cancels));
        Assert.Equal("Two litres", host.Text);
    }

    [WpfFact]
    public void Esc_asks_to_cancel_once_and_nothing_else()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        host.Press(Key.Escape);

        Assert.Equal((0, 1), (host.Saves, host.Cancels));
    }

    [WpfFact]
    public void Esc_closes_an_open_snippet_list_first_and_only_the_next_Esc_asks_to_cancel()
    {
        using var host = FieldEditorHost.OpenDescription();
        host.Edit("", 0);
        host.Type("/");
        Assert.NotEmpty(EditorFields.ShownSnippets(host.Editor));

        host.Press(Key.Escape);

        Assert.Empty(EditorFields.ShownSnippets(host.Editor));
        Assert.Equal(0, host.Cancels);

        host.Press(Key.Escape);

        Assert.Equal(1, host.Cancels);
    }

    [WpfFact]
    public void Typing_a_slash_opens_the_snippet_list()
    {
        using var host = FieldEditorHost.OpenDescription();
        host.Edit("", 0);

        host.Type("/");

        Assert.NotEmpty(EditorFields.ShownSnippets(host.Editor));
    }

    [WpfFact]
    public void Ctrl_B_wraps_the_selection_in_bold_markers_as_in_Add()
    {
        using var host = FieldEditorHost.OpenDescription("a b c");
        host.Edit("a b c", 2, 1);

        host.Press(Key.LeftCtrl, Key.B);

        Assert.Equal("a **b** c", host.Text);
    }

    [WpfFact]
    public void Ctrl_2_makes_the_line_a_heading_as_in_Add()
    {
        using var host = FieldEditorHost.OpenDescription("Plan");
        host.Edit("Plan", 0);

        host.Press(Key.LeftCtrl, Key.D2);

        Assert.Equal("## Plan", host.Text);
    }

    [WpfFact]
    public void Ctrl_V_with_an_image_on_the_clipboard_inserts_a_link_to_an_image_kept_for_the_task_id()
    {
        using var host = FieldEditorHost.OpenDescription("ab");
        host.Edit("ab", 1);
        host.Services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

        host.Press(Key.LeftCtrl, Key.V);

        var link = Assert.Single(ImageLink.Matches(host.Text));
        Assert.Equal("a" + link.Value + "b", host.Text);
        Assert.Contains(host.Editor.TaskId, link.Groups[2].Value);
        Assert.True(host.Services.Files.FileExists(host.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void An_image_that_cannot_be_kept_shows_an_error_under_the_field_and_keeps_the_text()
    {
        using var host = FieldEditorHost.OpenDescription("ab");
        host.Edit("ab", 1);
        host.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");

        host.Press(Key.LeftCtrl, Key.V);

        Assert.True(host.ShowsText("image"));
        Assert.Equal("ab", host.Text);
    }

    [WpfFact]
    public void FocusEnd_puts_the_cursor_at_the_end_of_the_description()
    {
        using var host = FieldEditorHost.OpenDescription("first line\nsecond line");

        host.Editor.FocusEnd();

        Assert.True(host.FieldHasFocus);
        Assert.Equal((host.Text.Length, 0), host.Selection);
    }

    [WpfFact]
    public void SetImageWidth_adds_the_width_to_the_numbered_image_and_reports_changes()
    {
        using var host = FieldEditorHost.OpenDescription("![a](images/a.png) ![b](images/b.png)");

        host.Editor.SetImageWidth(1, 200);

        Assert.Equal("![a](images/a.png) ![b|200](images/b.png)", host.Text);
        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void SetImageWidth_replaces_a_width_the_image_already_has()
    {
        using var host = FieldEditorHost.OpenDescription("![photo|100](images/a.png)");

        host.Editor.SetImageWidth(0, 250);

        Assert.Equal("![photo|250](images/a.png)", host.Text);
        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void SetImageWidth_with_the_width_the_text_already_has_changes_nothing()
    {
        using var host = FieldEditorHost.OpenDescription("![photo|300](images/a.png)");

        host.Editor.SetImageWidth(0, 300);

        Assert.Equal("![photo|300](images/a.png)", host.Text);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Undo_restores_the_text_from_before_SetImageWidth_in_one_step()
    {
        using var host = FieldEditorHost.OpenDescription("![photo|100](images/a.png)");
        host.Editor.SetImageWidth(0, 250);

        Assert.True(EditorFields.Undo(host.Editor, EditorFields.Description));

        Assert.Equal("![photo|100](images/a.png)", host.Text);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfTheory]
    [InlineData(1, 300)]
    [InlineData(-1, 300)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(0, 10001)]
    public void SetImageWidth_for_a_missing_image_or_a_width_outside_1_to_10000_throws_and_keeps_the_text(int image, int width)
    {
        using var host = FieldEditorHost.OpenDescription("![photo](images/a.png)");

        Assert.Throws<ArgumentOutOfRangeException>(() => host.Editor.SetImageWidth(image, width));

        Assert.Equal("![photo](images/a.png)", host.Text);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfTheory]
    [InlineData(1)]
    [InlineData(10000)]
    public void SetImageWidth_accepts_the_smallest_and_largest_width(int width)
    {
        using var host = FieldEditorHost.OpenDescription("![photo](images/a.png)");

        host.Editor.SetImageWidth(0, width);

        Assert.Equal($"![photo|{width}](images/a.png)", host.Text);
    }

    [WpfFact]
    public void Save_calls_the_function_with_the_description_and_returns_true()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Edit("Three litres");
        string? saved = null;

        var result = host.Editor.Save(description => saved = description);

        Assert.True(result);
        Assert.Equal("Three litres", saved);
        Assert.Empty(host.Texts);
    }

    [WpfFact]
    public void Save_accepts_an_empty_description()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Edit("");
        string? saved = null;

        var result = host.Editor.Save(description => saved = description);

        Assert.True(result);
        Assert.Equal("", saved);
        Assert.False(host.FieldIsMarked);
    }

    [WpfFact]
    public void A_known_save_failure_is_shown_under_the_field_and_the_text_is_kept()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        var result = host.Editor.Save(_ => throw new IOException("The disk is full."));

        Assert.False(result);
        Assert.True(host.ShowsText("The disk is full."));
        Assert.Equal("Two litres", host.Editor.Value);
    }

    [WpfFact]
    public void A_task_that_no_longer_exists_says_to_copy_the_changes()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        var result = host.Editor.Save(_ => throw new TaskNotFoundException());

        Assert.False(result);
        Assert.True(host.ShowsText("no longer exists"));
    }

    [WpfFact]
    public void A_failure_Tasks_is_not_known_to_throw_propagates()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");

        Assert.Throws<InvalidOperationException>(() => host.Editor.Save(_ => throw new InvalidOperationException("bug")));
    }

    [WpfFact]
    public void A_save_that_works_after_a_failed_one_clears_the_message()
    {
        using var host = FieldEditorHost.OpenDescription("Two litres");
        host.Editor.Save(_ => throw new IOException("The disk is full."));

        host.Editor.Save(_ => { });

        Assert.False(host.ShowsText("The disk is full."));
    }
}
