using System.Windows;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What Add does with its draft: it keeps the text when the window hides, restores it on the next Show, and
/// clears it on Clear or after a save. The kept draft is read back through Tasks, the draft's one home.
/// </summary>
[UX]
public class AddWindowDraftTests
{
    [WpfFact]
    public void Esc_keeps_what_was_typed_as_a_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void The_close_button_keeps_what_was_typed_as_a_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.CloseWindow();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void Another_window_becoming_active_keeps_what_was_typed_as_a_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        var window = screen.Window;
        var other = new Window { Width = 100, Height = 100 };

        other.Show();
        other.Activate();

        Assert.False(window.IsVisible);
        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
        other.Close();
    }

    [WpfFact]
    public void Closing_the_hidden_window_as_the_app_exits_leaves_the_draft_kept()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        var window = screen.Window;
        screen.PressEscape();

        window.Close();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void Hiding_with_every_field_empty_keeps_no_draft()
    {
        using var screen = new AddScreen();
        screen.Show();

        screen.PressEscape();

        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Clearing_every_field_and_hiding_removes_the_kept_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.PressEscape();
        screen.Show();
        screen.Fill("");

        screen.PressEscape();

        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Show_after_Esc_restores_the_draft_into_all_three_fields()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home", "shop");
        screen.PressEscape();

        screen.Show();

        AssertFields(screen, "Buy milk", "Two litres", ["home", "shop"]);
    }

    [WpfFact]
    public void Show_after_the_close_button_restores_the_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.CloseWindow();

        screen.Show();

        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void The_first_Show_opens_with_a_draft_kept_before_the_window_was_built()
    {
        using var screen = new AddScreen();
        var id = screen.Services.Tasks.StartDraft().TaskId;
        screen.Services.Tasks.KeepDraft(new TaskDraft(id, "From last run", "Text", ["old"]));

        screen.Show();

        AssertFields(screen, "From last run", "Text", ["old"]);
        Assert.Equal(id, screen.Editor.Value.TaskId);
    }

    [WpfFact]
    public void Show_with_a_restored_draft_puts_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres");
        screen.Focus(EditorFields.Description);
        screen.PressEscape();

        screen.Show();

        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void A_draft_keeps_its_task_id_across_hides_and_shows()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        var id = screen.Editor.Value.TaskId;
        screen.PressEscape();
        screen.Show();
        screen.PressEscape();

        screen.Show();

        Assert.Equal(id, screen.Editor.Value.TaskId);
        Assert.Equal(id, screen.KeptDraft?.TaskId);
    }

    [WpfFact]
    public void Show_with_no_draft_opens_empty_and_keeps_nothing()
    {
        using var screen = new AddScreen();

        screen.Show();

        AssertFields(screen, "", "", []);
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Show_while_open_keeps_what_was_typed()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.Show();

        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void A_kept_draft_whose_ID_is_already_a_task_is_deleted_and_Add_opens_empty()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Save();
        var savedId = Assert.Single(screen.Saved).Id;
        screen.Services.Tasks.KeepDraft(new TaskDraft(savedId, "Stale copy", "", []));

        screen.Show();

        AssertFields(screen, "", "", []);
        Assert.NotEqual(savedId, screen.Editor.Value.TaskId);
        Assert.False(screen.Services.Files.FileExists(screen.Services.DraftFile));
    }

    [WpfFact]
    public void An_unreadable_draft_opens_Add_empty_and_says_it_couldnt_be_loaded()
    {
        using var screen = new AddScreen();
        screen.Services.KeepUnreadableDraft();

        screen.Show();

        AssertFields(screen, "", "", []);
        Assert.True(screen.ShowsText("draft"));
        Assert.True(screen.ShowsText("loaded"));
    }

    [WpfFact]
    public void A_draft_that_cannot_be_read_from_the_disk_opens_Add_empty_and_says_it_couldnt_be_loaded()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.PressEscape();
        screen.Services.Files.FailReadsUnder(screen.Services.Device);

        screen.Show();

        AssertFields(screen, "", "", []);
        Assert.True(screen.ShowsText("draft"));
        Assert.True(screen.ShowsText("loaded"));
    }

    [WpfFact]
    public void A_draft_that_cannot_be_read_from_the_disk_is_left_on_the_disk_by_Show()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.PressEscape();
        screen.Services.Files.FailReadsUnder(screen.Services.Device);
        screen.Show();
        screen.Services.Files.StopFailing();

        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void Typed_but_unadded_Tags_text_survives_Esc_and_the_next_Show()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("hom");

        screen.PressEscape();
        screen.Show();

        Assert.Equal("hom", screen.TypedTag);
    }

    [WpfFact]
    public void Typed_but_unadded_Tags_text_survives_the_close_button_and_the_next_Show()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("hom");

        screen.CloseWindow();
        screen.Show();

        Assert.Equal("hom", screen.TypedTag);
    }

    [WpfFact]
    public void Typed_but_unadded_Tags_text_survives_another_window_becoming_active_and_the_next_Show()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("hom");
        var other = new Window { Width = 100, Height = 100 };

        other.Show();
        other.Activate();
        screen.Show();

        Assert.Equal("hom", screen.TypedTag);
        other.Close();
    }

    [WpfFact]
    public void Typed_but_unadded_Tags_text_is_gone_after_Clear()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("hom");

        screen.Clear();

        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void An_unreadable_draft_file_is_left_as_it_is_by_Show()
    {
        using var screen = new AddScreen();
        screen.Services.KeepUnreadableDraft();
        var before = screen.Services.Files.ReadFile(screen.Services.DraftFile);

        screen.Show();

        Assert.Equal(before, screen.Services.Files.ReadFile(screen.Services.DraftFile));
    }

    [WpfFact]
    public void The_couldnt_be_loaded_message_is_gone_when_the_next_Show_reads_a_draft()
    {
        using var screen = new AddScreen();
        screen.Services.KeepUnreadableDraft();
        screen.Show();
        screen.Fill("Buy milk");
        screen.PressEscape();

        screen.Show();

        Assert.False(screen.ShowsText("loaded"));
        AssertFields(screen, "Buy milk", "", []);
    }

    [WpfFact]
    public void Has_a_Clear_button()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.True(screen.ShowsText("Clear"));
    }

    [WpfFact]
    public void Clear_clears_the_fields_and_deletes_the_kept_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.PressEscape();
        screen.Show();

        screen.Clear();

        AssertFields(screen, "", "", []);
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Clear_starts_a_draft_with_a_new_task_id()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        var before = screen.Editor.Value.TaskId;

        screen.Clear();

        Assert.NotEqual(before, screen.Editor.Value.TaskId);
    }

    [WpfFact]
    public void Clear_keeps_the_window_open_with_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(EditorFields.Tags);

        screen.Clear();

        Assert.True(screen.IsShowing);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Clear_works_when_nothing_is_kept()
    {
        using var screen = new AddScreen();
        screen.Show();

        screen.Clear();

        AssertFields(screen, "", "", []);
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void After_Clear_hiding_and_showing_again_opens_empty()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Clear();
        screen.PressEscape();

        screen.Show();

        AssertFields(screen, "", "", []);
    }

    [WpfFact]
    public void After_a_successful_save_no_draft_is_kept_and_the_next_Show_opens_empty_with_a_new_task_id()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        var savedId = screen.Editor.Value.TaskId;
        screen.Save();

        Assert.Null(screen.KeptDraft);
        screen.Show();

        AssertFields(screen, "", "", []);
        Assert.NotEqual(savedId, screen.Editor.Value.TaskId);
    }

    [WpfFact]
    public void Saving_a_restored_draft_leaves_no_draft_behind()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.PressEscape();
        screen.Show();

        screen.Save();

        Assert.Null(screen.KeptDraft);
        Assert.False(screen.Services.Files.FileExists(screen.Services.DraftFile));
    }

    [WpfFact]
    public void Saving_and_clearing_leaves_no_draft_behind()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");

        screen.SaveAndClear();
        screen.PressEscape();

        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void With_no_repository_folder_a_draft_is_kept_and_restored()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
        screen.Show();

        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void A_failed_save_for_no_repository_folder_leaves_the_text_as_a_draft_after_Esc()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Save();

        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void A_failed_save_leaves_the_text_as_a_draft_after_Esc_and_restores_it()
    {
        using var screen = new AddScreen();
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Save();

        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
        screen.Show();
        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
    }

    [WpfFact]
    public void A_failed_save_does_not_delete_the_draft_kept_before_it()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.PressEscape();
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();

        screen.Save();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
    }

    private static void AssertKept(AddScreen screen, string title, string description, string[] tags)
    {
        var kept = screen.KeptDraft;
        Assert.NotNull(kept);
        Assert.Equal(title, kept.Title);
        Assert.Equal(description, kept.Description);
        Assert.Equal(tags, kept.Tags);
    }

    private static void AssertFields(AddScreen screen, string title, string description, string[] tags)
    {
        var value = screen.Editor.Value;
        Assert.Equal(title, value.Title);
        Assert.Equal(description, value.Description);
        Assert.Equal(tags, value.Tags);
    }
}
