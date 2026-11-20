using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>The Add window opening and closing, on the common path.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowTests
{
    [WpfFact]
    public void Show_opens_the_window_active_with_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        Assert.False(screen.IsShowing);

        screen.Show();

        Assert.True(screen.Window.IsActive);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void Esc_hides_the_window_and_Show_reopens_the_same_one_with_the_cursor_in_the_Title()
    {
        using var screen = new AddScreen();
        screen.Show();
        var first = screen.Window;
        screen.Focus(EditorFields.TagPill);

        OpenWindows.PressEscape(first);

        Assert.False(first.IsVisible);
        Assert.False(screen.IsShowing);

        screen.Show();

        Assert.Same(first, screen.Window);
        Assert.Equal(EditorFields.Title, screen.FocusedField);
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

        screen.Show();

        Assert.Same(window, screen.Window);
        Assert.True(screen.IsShowing);
    }

    [WpfFact]
    public void Stays_open_behind_another_window_that_becomes_active_and_Show_brings_it_back_to_the_front()
    {
        using var screen = new AddScreen();
        screen.Show();
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
    }
}
