using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The save keys, on the common path: Ctrl+Enter saves and hides Add, Ctrl+Shift+Enter saves and starts the next task.
/// Why a save is refused and what each refusal says are in the unit tests of the editor save and its failures; what
/// the draft does after a save is in the add draft's.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowSaveTests
{
    [WpfFact]
    public void Ctrl_Enter_saves_the_task_with_the_tag_text_still_typed_and_hides_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("errand");

        screen.Save();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Two litres", saved.Description);
        Assert.Equal(["home", "errand"], saved.Tags);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Changed_Save_keys_save_in_an_open_Add_window_and_the_old_keys_stop_saving()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Services.SaveKeys.Set(System.Windows.Input.ModifierKeys.Control, System.Windows.Input.Key.S);

        screen.Save();
        Assert.Empty(screen.Saved);

        screen.Press(System.Windows.Input.Key.LeftCtrl, System.Windows.Input.Key.S);

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_chosen_as_the_Save_keys_saves_and_hides_instead_of_saving_and_clearing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Services.SaveKeys.Set(
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift, System.Windows.Input.Key.Enter);

        screen.SaveAndClear();

        Assert.Single(screen.Saved);
        Assert.False(screen.IsShowing);
    }

    [WpfTheory]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.AddTag)]
    public void Ctrl_Enter_saves_from_any_field(string field)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(field);

        screen.Save();

        Assert.Single(screen.Saved);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_saves_the_task_and_keeps_the_window_open_with_empty_fields_and_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Focus(EditorFields.AddTag);

        screen.SaveAndClear();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.True(screen.IsShowing);
        var value = screen.Editor.Value;
        Assert.Equal("", value.Title);
        Assert.Equal("", value.Description);
        Assert.Empty(value.Tags);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Ctrl_Enter_with_an_empty_Title_saves_nothing_marks_the_Title_and_puts_the_cursor_there()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.TypeTag("errand");
        screen.Focus(EditorFields.Description);

        screen.Save();

        Assert.Empty(screen.Saved);
        Assert.True(screen.IsShowing);
        Assert.True(screen.TitleIsMarked);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
        Assert.Equal(["errand"], screen.Editor.Value.Tags);
    }

    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.AddTag)]
    public void An_Escape_based_Save_shortcut_saves_instead_of_closing_Add_with_the_draft_kept(string field)
    {
        using var screen = new AddScreen();
        screen.Services.SaveKeys.Set(System.Windows.Input.ModifierKeys.Shift, System.Windows.Input.Key.Escape);
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(field);

        screen.Press(System.Windows.Input.Key.LeftShift, System.Windows.Input.Key.Escape);

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Plain_Esc_still_closes_Add_without_saving_when_the_Save_shortcut_is_Shift_Esc()
    {
        using var screen = new AddScreen();
        screen.Services.SaveKeys.Set(System.Windows.Input.ModifierKeys.Shift, System.Windows.Input.Key.Escape);
        screen.Show();
        screen.Fill("Buy milk");

        screen.PressEscape();

        Assert.False(screen.IsShowing);
        Assert.Empty(screen.Saved);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void A_save_that_works_after_a_failed_one_hides_the_window()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk");
        screen.Save();
        Assert.True(screen.ShowsText("repository folder"));
        screen.Services.UseRepository();

        screen.Save();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.False(screen.IsShowing);
    }
}
