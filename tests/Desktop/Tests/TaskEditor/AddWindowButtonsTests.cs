using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Add window's two buttons: a small Clear above the Title and a Save below the tag chips and the message line.
/// What Clear and Save do to the draft and the repository is in the unit tests of the add draft and the editor save;
/// these check the buttons are wired to it, on the common path.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowButtonsTests
{
    [WpfFact]
    public void Clicking_Clear_empties_the_fields_and_the_Tags_line_and_leaves_Add_open()
    {
        using var screen = Shown();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("hom");

        screen.Clear();

        Assert.True(screen.IsShowing);
        Assert.Equal("", screen.Editor.Value.Title);
        Assert.Equal("", screen.TypedTag);
    }

    [WpfFact]
    public void Clicking_Save_saves_the_task_and_closes_Add()
    {
        using var screen = Shown();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("errand");

        screen.ClickSave();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Two litres", saved.Description);
        Assert.Equal(["home", "errand"], saved.Tags);
        Assert.False(screen.IsShowing);
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
    }

    [WpfFact]
    public void Clicking_Save_with_no_repository_folder_shows_the_message_and_keeps_Add_open_with_the_text()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.ClickSave();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("repository folder"));
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Empty(screen.Saved);
    }

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

    private static AddScreen Shown(params string[] tagsInUse)
    {
        var screen = new AddScreen();
        foreach (var tag in tagsInUse)
        {
            screen.Services.UseTag(tag);
        }

        screen.Show();
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
