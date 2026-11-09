using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Add window's two buttons: a small Clear above the Title and a Save below the tag chips and the message line.
/// Clear does what the old Discard did, so its draft behavior is also in <see cref="AddWindowDraftTests"/>. Save does
/// what Ctrl+Enter does, so its keys are also in <see cref="AddWindowSaveTests"/>.
/// </summary>
[UX]
public class AddWindowButtonsTests
{
    // --- Clear ---

    [WpfFact]
    public void Has_one_button_labelled_Clear()
    {
        using var screen = Shown();

        Assert.Equal("Clear", screen.ClearButton.Content);
    }

    [WpfFact]
    public void Shows_the_word_Discard_nowhere()
    {
        using var screen = Shown("home");

        var names = OpenWindows.Descendants(screen.Window).OfType<FrameworkElement>()
            .SelectMany(e => new[]
            {
                System.Windows.Automation.AutomationProperties.GetName(e),
                System.Windows.Automation.AutomationProperties.GetAutomationId(e),
                e.ToolTip?.ToString(),
            });
        Assert.DoesNotContain(screen.Texts, text => text.Contains("Discard", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name?.Contains("Discard", StringComparison.OrdinalIgnoreCase) == true);
    }

    [WpfFact]
    public void Clear_sits_at_the_top_right_above_the_Title()
    {
        using var screen = Shown();

        var clear = Bounds(screen, screen.ClearButton);
        var title = Bounds(screen, EditorFields.Field(screen.Window, EditorFields.Title));

        Assert.True(clear.Bottom <= title.Top, "Clear isn't above the Title.");
        Assert.True(clear.Left > screen.Window.ActualWidth / 2, "Clear isn't on the right.");
    }

    [WpfFact]
    public void Clear_is_shorter_than_Save_and_has_smaller_text()
    {
        using var screen = Shown();

        Assert.True(screen.ClearButton.ActualHeight < screen.SaveButton.ActualHeight, "Clear isn't shorter than Save.");
        Assert.True(screen.ClearButton.FontSize < screen.SaveButton.FontSize, "Clear's text isn't smaller than Save's.");
    }

    [WpfFact]
    public void Clear_has_the_plain_button_look()
    {
        using var screen = Shown();

        Assert.NotSame(screen.Window.FindResource("AccentButtonStyle"), screen.ClearButton.Style);
    }

    [WpfFact]
    public void Clear_empties_the_Tags_line_text_that_was_not_added()
    {
        using var screen = Shown();
        screen.TypeTag("hom");

        screen.Clear();

        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void Clear_leaves_no_message_and_no_mark_on_the_Title()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Save();
        Assert.True(screen.TitleIsMarked);
        screen.Fill("Buy milk");
        screen.Save();
        Assert.True(screen.ShowsText("repository folder"));

        screen.Clear();

        Assert.False(screen.ShowsText("repository folder"));
        Assert.False(screen.TitleIsMarked);
    }

    [WpfFact]
    public void Clear_deletes_a_draft_that_cannot_be_read()
    {
        using var screen = new AddScreen();
        screen.Services.KeepUnreadableDraft();
        screen.Show();

        screen.Clear();

        Assert.False(screen.Services.Files.FileExists(screen.Services.DraftFile));
    }

    [WpfFact]
    public void Clicking_Clear_does_not_hide_Add()
    {
        using var screen = Shown();
        screen.Fill("Buy milk");

        screen.Clear();

        Assert.True(screen.IsShowing);
    }

    // --- Save ---

    [WpfFact]
    public void Has_a_button_labelled_Save()
    {
        using var screen = Shown();

        Assert.Equal("Save", screen.SaveButton.Content);
    }

    [WpfFact]
    public void Save_sits_at_the_bottom_right_below_the_tag_chips_and_the_message_line()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();
        screen.Fill("Buy milk");
        screen.Save();
        var message = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Single(block => OpenWindows.TextOf(block).Contains("Writing is not allowed here", StringComparison.OrdinalIgnoreCase));

        var save = Bounds(screen, screen.SaveButton);

        Assert.True(save.Top >= Bounds(screen, EditorFields.Field(screen.Window, EditorFields.TagChips)).Bottom, "Save isn't below the chips.");
        Assert.True(save.Top >= Bounds(screen, message).Bottom, "Save isn't below the message line.");
        Assert.True(save.Left > screen.Window.ActualWidth / 2, "Save isn't on the right.");
    }

    [WpfFact]
    public void Save_has_the_accent_button_look()
    {
        using var screen = Shown();

        Assert.Same(screen.Window.FindResource("AccentButtonStyle"), screen.SaveButton.Style);
    }

    [WpfFact]
    public void Save_has_a_tooltip_that_names_Ctrl_Enter()
    {
        using var screen = Shown();

        Assert.Contains("Ctrl+Enter", screen.SaveButton.ToolTip?.ToString());
    }

    [WpfFact]
    public void Clicking_Save_saves_the_task_and_closes_Add()
    {
        using var screen = Shown();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.ClickSave();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Two litres", saved.Description);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Clicking_Save_adds_the_tag_text_still_typed_first()
    {
        using var screen = Shown();
        screen.Fill("Buy milk", "", "home");
        screen.TypeTag("errand");

        screen.ClickSave();

        Assert.Equal(["home", "errand"], Assert.Single(screen.Saved).Tags);
    }

    [WpfFact]
    public void Clicking_Save_with_an_empty_Title_saves_nothing_and_marks_the_Title_with_the_focus()
    {
        using var screen = Shown();
        screen.Fill("   ", "A description");
        screen.Focus(EditorFields.Tags);

        screen.ClickSave();

        Assert.Empty(screen.Saved);
        Assert.True(screen.IsShowing);
        Assert.True(screen.TitleIsMarked);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
        Assert.Equal("A description", screen.Editor.Value.Description);
    }

    [WpfFact]
    public void Clicking_Save_with_no_repository_folder_shows_the_same_message_and_keeps_Add_open()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.ClickSave();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("Configure"));
        Assert.True(screen.ShowsText("repository folder"));
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void Clicking_Save_that_fails_to_write_shows_the_error_and_keeps_Add_open_with_the_text()
    {
        using var screen = new AddScreen();
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.ClickSave();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal("Two litres", screen.Editor.Value.Description);
    }

    [WpfFact]
    public void Save_is_not_the_default_button_so_Enter_in_the_Title_still_moves_to_the_Description()
    {
        using var screen = Shown();
        screen.Fill("Buy milk");
        screen.Focus(EditorFields.Title);

        screen.Press(Key.Enter);

        Assert.False(screen.SaveButton.IsDefault);
        Assert.False(screen.ClearButton.IsDefault);
        Assert.Empty(screen.Saved);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    // --- Keyboard ---

    [WpfFact]
    public void Tab_goes_from_Clear_to_the_Title_through_the_fields_and_chips_to_Save()
    {
        using var screen = Shown("home", "work");
        var sequence = TabSequence(screen);

        var order = new[]
        {
            sequence.FindIndex(e => e == screen.ClearButton),
            sequence.FindIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.Title))),
            sequence.FindIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.Description))),
            sequence.FindIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.Tags))),
            sequence.FindLastIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.TagChips))),
            sequence.FindIndex(e => e == screen.SaveButton),
        };
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
        Assert.Equal(order.Distinct().Count(), order.Length);
    }

    [WpfTheory]
    [InlineData("Clear")]
    [InlineData("Save")]
    public void Ctrl_Enter_saves_and_closes_while_the_button_has_the_focus(string button)
    {
        using var screen = Shown();
        screen.Fill("Buy milk");
        FocusButton(screen, button);

        screen.Save();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.False(screen.IsShowing);
    }

    [WpfTheory]
    [InlineData("Clear")]
    [InlineData("Save")]
    public void Ctrl_Shift_Enter_saves_and_clears_while_the_button_has_the_focus(string button)
    {
        using var screen = Shown();
        screen.Fill("Buy milk");
        FocusButton(screen, button);

        screen.SaveAndClear();

        Assert.Single(screen.Saved);
        Assert.True(screen.IsShowing);
        Assert.Equal("", screen.Editor.Value.Title);
    }

    [WpfTheory]
    [InlineData("Clear")]
    [InlineData("Save")]
    public void Esc_closes_Add_and_keeps_a_draft_while_the_button_has_the_focus(string button)
    {
        using var screen = Shown();
        screen.Fill("Buy milk");
        FocusButton(screen, button);

        screen.PressEscape();

        Assert.False(screen.IsShowing);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void Clicking_Save_that_fails_does_not_hide_Add()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk");

        screen.ClickSave();

        Assert.True(screen.IsShowing);
    }

    private static AddScreen Shown(params string[] tagsInUse)
    {
        var screen = new AddScreen();
        foreach (var tag in tagsInUse)
        {
            screen.Services.UseTag(tag);
        }

        screen.Show();
#pragma warning disable WPF0001 // Window.ThemeMode is experimental; allowed only on this line.
        screen.Window.ThemeMode = ThemeMode.Light;
#pragma warning restore WPF0001
        screen.Window.UpdateLayout();
        return screen;
    }

    private static void FocusButton(AddScreen screen, string label)
    {
        var button = label == "Clear" ? screen.ClearButton : screen.SaveButton;
        button.Focus();
        Keyboard.Focus(button);
        Assert.Same(button, Keyboard.FocusedElement);
    }

    private static Rect Bounds(AddScreen screen, FrameworkElement element)
    {
        screen.Window.UpdateLayout();
        return element.TransformToAncestor(screen.Window).TransformBounds(new Rect(element.RenderSize));
    }

    private static List<DependencyObject> TabSequence(AddScreen screen)
    {
        var sequence = new List<DependencyObject>();
        FocusButton(screen, "Clear");
        for (var i = 0; i < 60; i++)
        {
            var focused = (DependencyObject)Keyboard.FocusedElement;
            sequence.Add(focused);
            if (focused == screen.SaveButton)
            {
                break;
            }

            ((UIElement)focused).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        return sequence;
    }

    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (DependencyObject? e = element; e is not null;
             e = (e is Visual ? VisualTreeHelper.GetParent(e) : null) ?? LogicalTreeHelper.GetParent(e))
        {
            if (e == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}
