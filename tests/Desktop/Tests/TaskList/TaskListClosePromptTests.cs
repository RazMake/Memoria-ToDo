using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Closing Task View with <c>IViewTasksWindow.Close</c>, the close button, and Alt+F4. An edit with no changes closes
/// at once; a changed edit asks Save, Discard, or Cancel in a modal prompt (driven by <see cref="ClosePrompt"/>, which
/// answers it while the close call waits). What each answer means is unit-tested in CloseDecisionTests; these tests check
/// the real prompt and what the user sees. Every test that could show the prompt answers it, so a missing prompt
/// can't hang the run.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListClosePromptTests
{
    private readonly ClosePrompt _prompt = new();

    private static TaskListScreen ChangedEdit(ListServices services)
    {
        services.Add("Buy milk");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        return screen;
    }

    private static TaskListScreen ChangedDescriptionEdit(ListServices services)
    {
        services.Add("Buy milk");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk");
        return screen;
    }

    private bool Close(TaskListScreen screen, Action<System.Windows.Window> answer) => _prompt.Run(() => screen.View.Close(), answer);

    [WpfFact]
    public void Close_with_no_edit_closes_without_asking_and_returns_true()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        var closed = Close(screen, ClosePrompt.Click("Cancel"));

        Assert.True(closed);
        Assert.False(screen.View.IsOpen);
        Assert.Equal(0, _prompt.Shown);
    }

    [WpfFact]
    public void Close_with_an_edit_that_has_no_changes_closes_without_asking_and_returns_true()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        var closed = Close(screen, ClosePrompt.Click("Cancel"));

        Assert.True(closed);
        Assert.False(screen.View.IsOpen);
        Assert.Equal(0, _prompt.Shown);
    }

    [WpfFact]
    public void Close_finishes_a_pending_Undo_so_the_deleted_task_is_gone_for_good()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);

        var closed = Close(screen, ClosePrompt.Click("Cancel"));

        Assert.True(closed);
        Assert.Empty(services.Tasks.All);
    }

    [WpfFact]
    public void Close_with_a_changed_edit_asks_in_a_dialog_owned_by_the_window_and_centered_on_it()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        var window = screen.Window;

        Close(screen, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.Same(window, _prompt.Owner);
        Assert.True(_prompt.Centered);
        Assert.Contains("Save your changes to this task?", _prompt.Texts);
        Assert.Contains("Save", _prompt.Texts);
        Assert.Contains("Discard", _prompt.Texts);
        Assert.Contains("Cancel", _prompt.Texts);
    }

    [WpfFact]
    public void Save_saves_the_edit_closes_Task_View_and_returns_true()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);

        var closed = Close(screen, ClosePrompt.Click("Save"));

        Assert.True(closed);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Enter_picks_Save_since_it_is_the_default_button()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);

        var closed = Close(screen, ClosePrompt.PressEnter);

        Assert.True(closed);
        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Save_with_an_empty_Title_saves_nothing_marks_the_Title_and_keeps_Task_View_open()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        screen.TypeTitle("Buy milk", "");

        var closed = Close(screen, ClosePrompt.Click("Save"));

        Assert.False(closed);
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.IsEditingAny);
        Assert.True(screen.EditedTitleIsMarked);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Save_that_cannot_write_keeps_Task_View_open_with_the_text_and_the_editors_message()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        services.Files.FailWritesUnder(services.TaskFolder);

        var closed = Close(screen, ClosePrompt.Click("Save"));

        Assert.False(closed);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", screen.EditedTitleText);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }

    [WpfFact]
    public void Discard_deletes_the_edits_images_saves_nothing_closes_and_returns_true()
    {
        var services = new ListServices();
        using var screen = ChangedDescriptionEdit(services);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);

        var closed = Close(screen, ClosePrompt.Click("Discard"));

        Assert.True(closed);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
        Assert.All(images, image => Assert.False(services.Files.FileExists(image)));
    }

    [WpfFact]
    public void Cancel_keeps_Task_View_open_with_the_edit_as_it_was_and_the_cursor_in_the_editor()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        screen.Select("Buy milk");

        var closed = Close(screen, ClosePrompt.Click("Cancel"));

        Assert.False(closed);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.True(screen.FocusIsInEditor);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Esc_in_the_prompt_picks_Cancel()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);

        var closed = Close(screen, ClosePrompt.PressEscape);

        Assert.False(closed);
        Assert.True(screen.IsEditingAny);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void The_prompts_own_close_button_picks_Cancel()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);

        var closed = Close(screen, ClosePrompt.CloseIt);

        Assert.False(closed);
        Assert.True(screen.IsEditingAny);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void The_close_button_and_Alt_F4_ask_the_same_prompt_and_Cancel_keeps_Task_View_open()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.IsEditingAny);
    }

    [WpfFact]
    public void A_plain_WM_CLOSE_with_a_changed_edit_closes_without_asking_and_discards_the_edit_and_its_images()
    {
        var services = new ListServices();
        using var screen = ChangedDescriptionEdit(services);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.SendPlainClose(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(0, _prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
        Assert.All(images, image => Assert.False(services.Files.FileExists(image)));
    }

    [WpfFact]
    public void A_plain_WM_CLOSE_finishes_a_pending_Undo_so_the_deleted_task_is_gone_for_good()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.SendPlainClose(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.False(screen.View.IsOpen);
        Assert.Empty(services.Tasks.All);
    }

    [WpfFact]
    public void A_plain_WM_CLOSE_while_the_prompt_shows_closes_the_prompt_as_Cancel_then_closes_Task_View_and_discards_the_edit()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, _ => ClosePrompt.SendPlainClose(window));

        Assert.Equal(1, _prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Discard_from_the_close_button_closes_Task_View_without_saving()
    {
        var services = new ListServices();
        using var screen = ChangedEdit(services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, ClosePrompt.Click("Discard"));

        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }
}
