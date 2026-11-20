using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What Add does with its draft, on the common path: it keeps the text when the window hides, restores it on the next
/// Show, and clears it on Clear or after a save. The rules (an unreadable draft, a draft whose task exists, the keep
/// failing) are in the unit tests of the add draft; the kept draft is read back through Tasks, the draft's one home.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowDraftTests
{
    [WpfFact]
    public void Esc_keeps_what_was_typed_and_the_next_Show_restores_it_with_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Focus(EditorFields.Description);

        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);

        screen.Show();

        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
        Assert.Equal("home", screen.ShownTag);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void The_first_Esc_drops_the_text_typed_in_the_tag_box_and_the_second_keeps_the_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("hom");
        screen.Focus(EditorFields.Description);

        screen.PressEscape();
        Assert.Null(screen.KeptDraft);

        screen.PressEscape();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
        screen.Show();
        Assert.True(string.IsNullOrEmpty(screen.TypedTag));
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
    public void A_failed_save_leaves_the_text_as_a_draft_after_Esc_and_restores_it()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Save();

        screen.PressEscape();
        screen.Show();

        AssertKept(screen, "Buy milk", "Two litres", ["home"]);
        AssertFields(screen, "Buy milk", "Two litres", ["home"]);
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
