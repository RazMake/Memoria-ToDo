using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class AddWindowTests
{
    [WpfFact]
    public void Create_opens_no_window()
    {
        using var screen = new AddScreen();

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Show_makes_the_window_visible_and_active()
    {
        using var screen = new AddScreen();

        screen.Show();

        Assert.True(screen.Window.IsActive);
    }

    [WpfFact]
    public void Esc_hides_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        var window = screen.Window;

        OpenWindows.PressEscape(window);

        Assert.False(window.IsVisible);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void The_close_button_hides_the_window_instead_of_closing_it()
    {
        using var screen = new AddScreen();
        screen.Show();
        var window = screen.Window;

        window.Close();

        Assert.False(window.IsVisible);
        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Show_after_Esc_reopens_the_same_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        var first = screen.Window;
        OpenWindows.PressEscape(first);

        screen.Show();

        Assert.Same(first, screen.Window);
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Show_after_the_close_button_reopens_the_same_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        var first = screen.Window;
        first.Close();

        screen.Show();

        Assert.Same(first, screen.Window);
    }

    [WpfFact]
    public void Show_while_open_keeps_one_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        var first = screen.Window;

        screen.Show();

        Assert.Same(first, screen.Window);
    }

    [WpfFact]
    public void Opens_480_wide_with_a_minimum_height_of_240_sizing_its_height_to_its_content_and_cannot_be_resized()
    {
        using var screen = new AddScreen();
        screen.Show();
        var window = screen.Window;

        Assert.Equal(480, window.ActualWidth);
        Assert.Equal(240, window.MinHeight);
        Assert.True(window.ActualHeight >= 240);
        Assert.Equal(SizeToContent.Height, window.SizeToContent);
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
    }

    [WpfFact]
    public void Holds_a_Title_a_Description_and_a_Tags_field()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.NotNull(EditorFields.Field(screen.Window, EditorFields.Title));
        Assert.NotNull(EditorFields.Field(screen.Window, EditorFields.Description));
        Assert.NotNull(EditorFields.Field(screen.Window, EditorFields.Tags));
    }

    [WpfFact]
    public void Holds_one_task_editor()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.NotNull(screen.Editor);
    }

    [WpfFact]
    public void Opens_with_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();

        screen.Show();

        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Opens_with_the_cursor_in_the_Title_again_after_Esc()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Tags);
        OpenWindows.PressEscape(screen.Window);

        screen.Show();

        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Hides_when_another_window_becomes_active()
    {
        using var screen = new AddScreen();
        screen.Show();
        var window = screen.Window;
        var other = new Window { Width = 100, Height = 100 };

        other.Show();
        other.Activate();

        Assert.False(window.IsVisible);
    }

    [WpfFact]
    public void Stays_open_while_it_is_the_active_window()
    {
        using var screen = new AddScreen();

        screen.Show();

        Assert.True(screen.Window.IsVisible);
    }
}
