using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The platform's Close window keys in the Add window, on the common path: pressed from any control, they close Add as
/// its close button does, so the draft is kept. The keys here are Shift+F9, which no editor key uses. How the keys are
/// matched is tested with the platform's CloseWindowKeys.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowCloseKeysTests
{
    [WpfFact]
    public void The_keys_pressed_in_the_Title_close_Add_and_keep_the_draft()
    {
        using var screen = new AddScreen();
        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Focus(EditorFields.Title);

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        AssertKept(screen);
    }

    [WpfFact]
    public void The_keys_pressed_in_the_Description_close_Add()
    {
        using var screen = new AddScreen();
        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Focus(EditorFields.Description);

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        AssertKept(screen);
    }

    [WpfFact]
    public void The_keys_close_Add_while_the_tag_suggestions_are_open()
    {
        using var screen = new AddScreen();
        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("ho");
        Assert.NotEmpty(screen.Suggestions);

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void The_keys_close_Add_while_the_snippet_list_is_open()
    {
        using var screen = new AddScreen();
        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/");
        Assert.NotEmpty(screen.ShownSnippets);

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void Add_shows_again_with_the_draft_after_the_keys_closed_it()
    {
        using var screen = new AddScreen();
        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Press(Key.LeftShift, Key.F9);

        screen.Show();

        Assert.True(screen.IsShowing);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
    }

    private static void AssertKept(AddScreen screen)
    {
        var kept = screen.KeptDraft;
        Assert.NotNull(kept);
        Assert.Equal("Buy milk", kept.Title);
        Assert.Equal("Two litres", kept.Description);
        Assert.Equal(["home"], kept.Tags);
    }
}
