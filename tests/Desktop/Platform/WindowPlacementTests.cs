using Xunit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Show on the pointer's screen, with a plain window. The expected place comes from Windows' own functions
/// (pointer, screen, work area), read in pixels, so the tests don't repeat the platform's calculation.
/// The first test moves the real pointer to each screen's center, then puts it back.
/// </summary>
[UX]
public class WindowPlacementTests
{
    private const int RoundingPixels = 2;

    [WpfFact]
    public void A_hidden_window_opens_centered_on_the_work_area_of_each_screen_with_the_pointer()
    {
        var original = Win32.CursorPosition();
        try
        {
            foreach (var center in Win32.ScreenCenters())
            {
                Win32.MoveCursor(center);
                var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
                var window = new Window { Width = 300, Height = 200 };
                try
                {
                    WindowPlacement.ShowOnPointerScreen(window);

                    var rect = Win32.WindowRect(Handle(window));
                    Assert.InRange(rect.Left + rect.Width / 2 - (work.Left + work.Width / 2), -RoundingPixels, RoundingPixels);
                    Assert.InRange(rect.Top + rect.Height / 2 - (work.Top + work.Height / 2), -RoundingPixels, RoundingPixels);
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            Win32.MoveCursor(original);
        }
    }

    [WpfFact]
    public void A_hidden_window_is_visible_afterwards()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_hidden_window_is_activated()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            Assert.True(window.IsActive);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_bigger_than_the_work_area_opens_at_its_top_left_corner()
    {
        var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
        // Pixels used as device-independent units are never fewer than the work area's pixels, at 100% scale or more.
        var window = new Window { Width = work.Width + 400, Height = work.Height + 400 };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            var rect = Win32.WindowRect(Handle(window));
            Assert.InRange(rect.Left - work.Left, -RoundingPixels, RoundingPixels);
            Assert.InRange(rect.Top - work.Top, -RoundingPixels, RoundingPixels);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_size_to_fit_window_with_no_width_or_height_opens_centered_by_its_real_size()
    {
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight, Content = new Border { Width = 400, Height = 250 } };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            AssertCenteredOnWorkAreaOfPointerScreen(window);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_size_to_fit_window_counts_its_MinWidth_when_it_opens_centered()
    {
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight, MinWidth = 600, Content = new Border { Width = 100, Height = 100 } };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            AssertCenteredOnWorkAreaOfPointerScreen(window);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_size_to_fit_window_bigger_than_the_work_area_opens_at_its_top_left_corner()
    {
        var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight, Content = new Border { Width = work.Width + 400, Height = work.Height + 400 } };
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);

            var rect = Win32.WindowRect(Handle(window));
            Assert.InRange(rect.Left - work.Left, -RoundingPixels, RoundingPixels);
            Assert.InRange(rect.Top - work.Top, -RoundingPixels, RoundingPixels);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_size_to_fit_window_hidden_after_it_was_shown_opens_centered_again()
    {
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight, Content = new Border { Width = 400, Height = 250 } };
        AssertOpensCenteredAgainAfterHide(window);
    }

    [WpfFact]
    public void A_fixed_size_window_hidden_after_it_was_shown_opens_centered_again()
    {
        var window = new Window { Width = 300, Height = 200 };
        AssertOpensCenteredAgainAfterHide(window);
    }

    [WpfFact]
    public void A_visible_window_stays_where_it_is()
    {
        var window = new Window { Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.Manual, Left = 40, Top = 40 };
        try
        {
            window.Show();
            var before = Win32.WindowRect(Handle(window));

            WindowPlacement.ShowOnPointerScreen(window);

            var after = Win32.WindowRect(Handle(window));
            Assert.Equal((before.Left, before.Top, before.Width, before.Height), (after.Left, after.Top, after.Width, after.Height));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_minimized_window_is_restored()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Show();
            window.WindowState = WindowState.Minimized;

            WindowPlacement.ShowOnPointerScreen(window);

            Assert.Equal(WindowState.Normal, window.WindowState);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_hidden_while_minimized_opens_visible_in_the_normal_state_centered_on_the_pointer_screen()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Show();
            window.WindowState = WindowState.Minimized;
            window.Hide(); // As the Add window hides when closed from its taskbar button.

            WindowPlacement.ShowOnPointerScreen(window);

            Assert.True(window.IsVisible);
            Assert.Equal(WindowState.Normal, window.WindowState);
            AssertCenteredOnWorkAreaOfPointerScreen(window);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public async Task Calling_off_the_windows_UI_thread_throws_InvalidOperationException()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => WindowPlacement.ShowOnPointerScreen(window)));
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertOpensCenteredAgainAfterHide(Window window)
    {
        try
        {
            WindowPlacement.ShowOnPointerScreen(window);
            var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
            window.Left = work.Left; // Moved away from the center, as when the user dragged it.
            window.Top = work.Top;
            window.Hide();

            WindowPlacement.ShowOnPointerScreen(window);

            AssertCenteredOnWorkAreaOfPointerScreen(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertCenteredOnWorkAreaOfPointerScreen(Window window)
    {
        var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
        var rect = Win32.WindowRect(Handle(window));
        Assert.InRange(rect.Left + rect.Width / 2 - (work.Left + work.Width / 2), -RoundingPixels, RoundingPixels);
        Assert.InRange(rect.Top + rect.Height / 2 - (work.Top + work.Height / 2), -RoundingPixels, RoundingPixels);
    }

    private static nint Handle(Window window) => new WindowInteropHelper(window).Handle;
}
