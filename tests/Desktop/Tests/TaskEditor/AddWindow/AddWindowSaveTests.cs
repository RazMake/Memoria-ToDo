using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>
/// The Add window's save and clear paths, each one whole user path through one open window. The rules behind them (why a
/// save is refused, what each refusal says, what the draft does) are in the unit tests of the add draft, the editor save
/// and the save failures.
/// </summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowSaveTests
{
    [WpfFact]
    public void Path_1_opens_in_the_Title_tabs_through_the_parts_and_saves_with_Ctrl_Enter_then_opens_empty()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("home");
        screen.Services.UseTag("work");
        Assert.False(screen.IsShowing);

        screen.Show();

        Assert.True(screen.Window.IsActive);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
        screen.Fill("Buy milk", "Two litres", "home");
        screen.Window.UpdateLayout();
        AssertTabOrder(screen);
        screen.TypeTag("errand");
        var savedId = screen.Editor.Value.TaskId;

        screen.Save();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Two litres", saved.Description);
        Assert.Equal(["home", "errand"], saved.Tags);
        Assert.False(screen.IsShowing);
        Assert.Null(screen.KeptDraft);

        screen.Show();

        Assert.Equal("", screen.Editor.Value.Title);
        Assert.Equal("", screen.Editor.Value.Description);
        Assert.Empty(screen.Editor.Value.Tags);
        Assert.NotEqual(savedId, screen.Editor.Value.TaskId);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Path_2_the_buttons_Ctrl_Shift_Enter_and_an_empty_Title_clear_and_save()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("hom");

        screen.Clear();

        Assert.True(screen.IsShowing);
        Assert.Equal("", screen.Editor.Value.Title);
        Assert.Equal("", screen.TypedTag);

        screen.Fill("   ", "A description");
        screen.Focus(EditorFields.AddTagPill);
        screen.ClickSave();

        Assert.Empty(screen.Saved);
        Assert.True(screen.IsShowing);
        Assert.True(screen.TitleIsMarked);
        Assert.Equal(EditorFields.Title, screen.FocusedField);

        screen.Fill("", "", "errand");
        screen.Focus(EditorFields.Description);
        screen.Save();

        Assert.Empty(screen.Saved);
        Assert.True(screen.TitleIsMarked);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
        Assert.Equal(["errand"], screen.Editor.Value.Tags);

        screen.Fill("Buy milk", "Two litres", "home");
        screen.Focus(EditorFields.AddTagPill);
        screen.SaveAndClear();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.True(screen.IsShowing);
        Assert.Equal("", screen.Editor.Value.Title);
        Assert.Equal("", screen.Editor.Value.Description);
        Assert.Empty(screen.Editor.Value.Tags);
        Assert.Equal(EditorFields.Title, screen.FocusedField);

        screen.Fill("Draft kept");
        screen.PressEscape();
        screen.Show();
        screen.Clear();

        Assert.Equal("", screen.Editor.Value.Title);
        Assert.Null(screen.KeptDraft);

        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("errand");
        screen.ClickSave();

        var saved = Assert.Single(screen.Saved, task => task.Tags.Contains("errand"));
        Assert.Equal("Buy milk", saved.Title);
        Assert.False(screen.IsShowing);

        var count = screen.Saved.Count;
        foreach (var button in new[] { "Clear", "Save" })
        {
            screen.Show();
            screen.Fill("Second " + button);
            FocusButton(screen, button);

            screen.Save();

            Assert.Equal("Second " + button, screen.Saved[0].Title);
            Assert.False(screen.IsShowing);
        }

        Assert.Equal(count + 2, screen.Saved.Count);
    }

    [WpfFact]
    public void Path_3_changed_Save_keys_replace_the_old_ones_and_an_Escape_based_Save_saves_instead_of_closing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);

        screen.Save();
        Assert.Empty(screen.Saved);
        screen.Press(Key.LeftCtrl, Key.S);

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);

        screen.Services.SaveKeys.Set(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter);
        screen.Show();
        screen.Fill("Second");
        screen.SaveAndClear();

        Assert.Equal(2, screen.Saved.Count);
        Assert.False(screen.IsShowing);

        screen.Services.SaveKeys.Set(ModifierKeys.Shift, Key.Escape);
        var count = 2;
        foreach (var field in new[] { EditorFields.Title, EditorFields.Description, EditorFields.AddTagPill })
        {
            screen.Show();
            screen.Fill("Via " + field);
            screen.Focus(field);

            screen.Press(Key.LeftShift, Key.Escape);

            Assert.Equal(++count, screen.Saved.Count);
            Assert.False(screen.IsShowing);
        }

        screen.Show();
        screen.Fill("Esc closes");
        screen.PressEscape();

        Assert.False(screen.IsShowing);
        Assert.Equal(count, screen.Saved.Count);
        Assert.Equal("Esc closes", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void Path_4_a_failed_save_shows_the_message_keeps_the_text_and_a_later_save_works()
    {
        using var screen = new AddScreen(withRepository: false);
        screen.Show();
        screen.Fill("Buy milk", "Two litres", "home");

        screen.ClickSave();

        Assert.True(screen.IsShowing);
        Assert.True(screen.ShowsText("repository folder"));
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Empty(screen.Saved);

        screen.PressEscape();
        screen.Show();

        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Equal("Two litres", screen.Editor.Value.Description);
        Assert.Equal(["home"], screen.Editor.Value.Tags);

        screen.Save();
        Assert.True(screen.ShowsText("repository folder"));
        screen.Services.UseRepository();
        screen.Save();

        Assert.Equal("Buy milk", Assert.Single(screen.Saved).Title);
        Assert.False(screen.IsShowing);
    }

    private static void AssertTabOrder(AddScreen screen)
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

        var order = new[]
        {
            sequence.FindIndex(e => e == screen.ClearButton),
            sequence.FindIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.Title))),
            sequence.FindIndex(e => IsInside(e, EditorFields.Field(screen.Window, EditorFields.Description))),
            sequence.FindIndex(e => e == TagControls.PillButton(screen.Window)),
            sequence.FindIndex(e => e == TagControls.ClearButton(screen.Window)),
            sequence.FindIndex(e => e == TagControls.AddPill(screen.Window)),
            sequence.FindIndex(e => e == screen.SaveButton),
        };
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
        Assert.Equal(order.Distinct().Count(), order.Length);
    }

    private static void FocusButton(AddScreen screen, string label)
    {
        var button = label == "Clear" ? screen.ClearButton : screen.SaveButton;
        button.Focus();
        Keyboard.Focus(button);
        Assert.Same(button, Keyboard.FocusedElement);
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

/// <summary>The Add window opening, hiding and keeping its draft, each test one whole path.</summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowCloseTests
{
    [WpfFact]
    public void Path_5_Esc_the_close_button_Close_and_the_platform_Close_keys_hide_Add_and_keep_the_draft()
    {
        using var screen = new AddScreen();
        screen.Show();
        var first = screen.Window;
        screen.Fill("Buy milk", "Two litres", "home");
        screen.TypeTag("hom");
        screen.Focus(EditorFields.Description);

        screen.PressEscape();
        Assert.Null(screen.KeptDraft);
        screen.PressEscape();

        Assert.False(first.IsVisible);
        AssertKept(screen);
        screen.Show();

        Assert.Same(first, screen.Window);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Equal("home", screen.ShownTag);
        Assert.True(string.IsNullOrEmpty(screen.TypedTag));
        Assert.Equal(EditorFields.Title, screen.FocusedField);

        screen.CloseWindow();

        Assert.False(first.IsVisible);
        AssertKept(screen);
        screen.Show();
        screen.TypeTag("hom");

        screen.Add.Close();

        Assert.False(screen.Add.IsOpen);
        AssertKept(screen);
        screen.Show();

        Assert.True(screen.Add.IsOpen);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        Assert.Equal("hom", screen.TypedTag);
        Assert.Equal(EditorFields.Title, screen.FocusedField);

        screen.Services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        screen.Services.UseTag("hobby");
        screen.Fill("Buy milk", "Two litres", "home");

        foreach (var field in new[] { EditorFields.Title, EditorFields.Description })
        {
            screen.Focus(field);
            screen.Press(Key.LeftShift, Key.F9);

            Assert.False(screen.IsShowing);
            AssertKept(screen);
            screen.Show();
            Assert.Equal("Buy milk", screen.Editor.Value.Title);
        }

        OpenWindows.Click(TagControls.PillButton(screen.Window));
        Assert.True(TagControls.MenuIsOpen(screen.Window));
        TagControls.SearchBox(screen.Window).Focus();
        Keyboard.Focus(TagControls.SearchBox(screen.Window));
        Assert.Equal(TagControls.MenuSearch, TagControls.FocusedId());
        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
        screen.Show();

        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/");
        Assert.NotEmpty(screen.ShownSnippets);
        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.IsShowing);
        Assert.Equal("Buy milk", screen.KeptDraft?.Title);
    }

    [WpfFact]
    public void Path_6_first_Show_opens_a_draft_kept_before_the_window_existed_and_Add_stays_open_behind_another_window()
    {
        using var screen = new AddScreen();
        screen.Add.Close();
        Assert.False(screen.Add.IsOpen);
        Assert.Null(screen.KeptDraft);
        var id = screen.Services.Tasks.StartDraft().TaskId;
        screen.Services.Tasks.KeepDraft(new TaskDraft(id, "From last run", "Text", ["old"]));

        screen.Show();

        Assert.Equal("From last run", screen.Editor.Value.Title);
        Assert.Equal("Text", screen.Editor.Value.Description);
        Assert.Equal(["old"], screen.Editor.Value.Tags);
        Assert.Equal(id, screen.Editor.Value.TaskId);
        var window = screen.Window;
        var other = new Window { Width = 100, Height = 100 };

        other.Show();
        other.Activate();

        Assert.True(window.IsVisible);
        Assert.False(window.IsActive);
        screen.Show();

        Assert.Same(window, screen.Window);
        Assert.True(window.IsActive);
        other.Close();

        screen.PressEscape();
        screen.Services.Tasks.KeepDraft(new TaskDraft(id, "Kept elsewhere", "", []));
        screen.Add.Close();

        Assert.False(screen.Add.IsOpen);
        Assert.Equal("Kept elsewhere", screen.KeptDraft?.Title);
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
