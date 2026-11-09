using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class AddWindowSaveTests
{
    [WpfFact]
    public void Ctrl_Enter_saves_the_task_and_hides_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.Save();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Two litres", saved.Description);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Ctrl_Enter_saves_from_the_Description_too()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(EditorFields.Description);

        screen.Save();

        Assert.Single(screen.Saved);
    }

    [WpfFact]
    public void Ctrl_Enter_saves_from_the_Tags_too()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(EditorFields.Tags);

        screen.Save();

        Assert.Single(screen.Saved);
    }

    [WpfFact]
    public void A_saved_task_goes_to_the_top_of_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("First");
        screen.Save();
        screen.Show();
        screen.Fill("Second");

        screen.Save();

        Assert.Equal(["Second", "First"], screen.Saved.Select(task => task.Title));
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_saves_the_task_and_keeps_the_window_open_with_empty_fields()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.SaveAndClear();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.True(screen.IsShowing);
        var value = screen.Editor.Value;
        Assert.Equal("", value.Title);
        Assert.Equal("", value.Description);
        Assert.Empty(value.Tags);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_puts_the_cursor_back_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Focus(EditorFields.Tags);

        screen.SaveAndClear();

        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void After_Ctrl_Shift_Enter_the_next_task_is_a_new_one()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("First");
        screen.SaveAndClear();
        screen.Fill("Second");

        screen.SaveAndClear();

        Assert.Equal(2, screen.Saved.Count);
        Assert.NotEqual(screen.Saved[0].Id, screen.Saved[1].Id);
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("   ")]
    public void Saving_with_an_empty_Title_saves_nothing_and_keeps_the_window_open(string title)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill(title, "A description");

        screen.Save();

        Assert.Empty(screen.Saved);
        Assert.True(screen.IsShowing);
        Assert.Equal("A description", screen.Editor.Value.Description);
    }

    [WpfFact]
    public void Saving_with_an_empty_Title_marks_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        Assert.False(screen.TitleIsMarked);

        screen.Save();

        Assert.True(screen.TitleIsMarked);
    }

    [WpfFact]
    public void Saving_with_an_empty_Title_paints_the_Title_border_differently_from_normal()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.UseTheme(dark: false);
        var normal = screen.TitleBorderBrush;

        screen.Save();

        Assert.NotNull(screen.TitleBorderBrush);
        Assert.False(BrushesAreEqual(normal, screen.TitleBorderBrush), "The Title looks the same as before the save was refused.");
    }

    [WpfFact]
    public void Typing_a_Title_paints_the_Title_border_as_normal_again()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.UseTheme(dark: false);
        var normal = screen.TitleBorderBrush;
        screen.Save();

        screen.Edit(EditorFields.Title, "Buy milk", 8);

        Assert.True(BrushesAreEqual(normal, screen.TitleBorderBrush), "The Title still looks missing after it got text.");
    }

    private static bool BrushesAreEqual(System.Windows.Media.Brush? a, System.Windows.Media.Brush? b) => Looks.BrushesAreEqual(a, b);

    [WpfFact]
    public void Saving_with_an_empty_Title_and_clearing_saves_nothing_and_marks_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("", "Keep me");

        screen.SaveAndClear();

        Assert.Empty(screen.Saved);
        Assert.True(screen.TitleIsMarked);
        Assert.Equal("Keep me", screen.Editor.Value.Description);
    }

    [WpfFact]
    public void Saving_with_no_repository_folder_fails_with_a_message_and_keeps_the_text()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.Save();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("Configure"));
        Assert.True(screen.ShowsText("repository folder"));
        var value = screen.Editor.Value;
        Assert.Equal("Buy milk", value.Title);
        Assert.Equal("Two litres", value.Description);
        Assert.Equal(["home"], value.Tags);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void Saving_with_no_repository_folder_and_clearing_keeps_the_text_too()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk");

        screen.SaveAndClear();

        Assert.True(screen.IsShowing);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
    }

    [WpfFact]
    public void A_failed_save_shows_the_error_and_keeps_the_window_open_with_the_text()
    {
        using var screen = new AddScreen();
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.Save();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        var value = screen.Editor.Value;
        Assert.Equal("Buy milk", value.Title);
        Assert.Equal("Two litres", value.Description);
        Assert.Equal(["home"], value.Tags);
    }

    [WpfFact]
    public void A_save_refused_for_an_invalid_top_rank_shows_a_message_and_keeps_the_window_open_with_the_text()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Services.AddResolvedTask("Old", rank: "a0!");
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.Save();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("wasn't saved"));
        var value = screen.Editor.Value;
        Assert.Equal("Buy milk", value.Title);
        Assert.Equal("Two litres", value.Description);
        Assert.Equal(["home"], value.Tags);
    }

    [WpfFact]
    public void A_save_and_clear_refused_for_an_invalid_top_rank_keeps_the_window_open_with_the_text()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Services.AddResolvedTask("Old", rank: "a0!");
        screen.Show();
        screen.Fill("Buy milk");

        screen.SaveAndClear();

        Assert.True(screen.IsShowing);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.True(screen.ShowsText("wasn't saved"));
    }

    [WpfFact]
    public void A_save_whose_undo_also_fails_says_the_add_was_incomplete_and_keeps_the_window_open_with_the_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "", "home");
        screen.Services.Clipboard.Image = Memoria.ToDo.Shared.Fakes.FakeImageEncoder.Image(Memoria.ToDo.Shared.Tasks.ImageFormat.Png, 100, 80);
        screen.Edit(EditorFields.Description, "", 0, 0);
        screen.PressCtrl(false, Key.V);
        var description = screen.TextOf(EditorFields.Description);
        screen.Services.Files.FailWritesUnder(System.IO.Path.Combine(screen.Services.Clone, "images"));
        screen.Services.Files.FailDeletesUnder(screen.Services.TaskFolder);

        screen.Save();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("incomplete"));
        var value = screen.Editor.Value;
        Assert.Equal("Buy milk", value.Title);
        Assert.Equal(description, value.Description);
        Assert.Equal(["home"], value.Tags);
    }

    [WpfFact]
    public void A_failed_save_and_clear_keeps_the_text_too()
    {
        using var screen = new AddScreen();
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();
        screen.Fill("Buy milk");

        screen.SaveAndClear();

        Assert.True(screen.IsShowing);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }
}
