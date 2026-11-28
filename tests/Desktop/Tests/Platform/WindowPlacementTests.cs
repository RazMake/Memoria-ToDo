using Xunit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Memoria.ToDo.Testing;
using Serilog.Core;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Show on the pointer's screen, with a plain window. The expected place comes from Windows' own functions
/// (pointer, screen, work area), read in pixels, so the tests don't repeat the platform's calculation.
/// The first test moves the real pointer to each screen's center, then puts it back.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
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
                    WindowPlacement.ShowOnPointerScreen(window, Logger.None);

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
    public void A_hidden_window_is_visible_and_active_afterwards()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

            Assert.True(window.IsVisible);
            Assert.True(window.IsActive);
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
            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

            AssertCenteredOnWorkAreaOfPointerScreen(window);
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
    public void A_visible_window_stays_where_it_is()
    {
        var window = new Window { Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.Manual, Left = 40, Top = 40 };
        try
        {
            window.Show();
            var before = Win32.WindowRect(Handle(window));

            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

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

            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

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

            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

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
    public void A_window_shown_for_the_first_time_is_cloaked_centered_visible_and_active_when_the_call_returns()
    {
        var window = new Window { Width = 300, Height = 200, Content = new Border() };
        try
        {
            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

            Assert.True(Win32.IsCloaked(Handle(window)));
            Assert.True(window.IsVisible);
            Assert.True(window.IsActive);
            AssertCenteredOnWorkAreaOfPointerScreen(window);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_shown_for_the_first_time_is_uncloaked_once_WPF_has_drawn_it()
    {
        var window = new Window { Width = 300, Height = 200, Content = new Border() };
        try
        {
            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);

            AfterFirstDraw.Wait(window.Dispatcher);

            Assert.False(Win32.IsCloaked(Handle(window)));
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_with_no_content_is_uncloaked_once_WPF_has_drawn_it()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
                    WindowPlacement.ShowOnPointerScreen(window, Logger.None);

            AfterFirstDraw.Wait(window.Dispatcher);

            Assert.False(Win32.IsCloaked(Handle(window)));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_is_uncloaked_even_when_a_Loaded_handler_marks_the_event_handled()
    {
        var window = new Window { Width = 300, Height = 200, Content = new Border() };
        window.Loaded += (_, e) => e.Handled = true;
        try
        {
                    WindowPlacement.ShowOnPointerScreen(window, Logger.None);

            AfterFirstDraw.Wait(window.Dispatcher);

            Assert.False(Win32.IsCloaked(Handle(window)));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_closed_while_it_loads_is_left_alone()
    {
        var window = new Window { Width = 300, Height = 200, Content = new Border() };
        window.Loaded += (_, _) => window.Close();
        try
        {
                    WindowPlacement.ShowOnPointerScreen(window, Logger.None);

            Assert.Null(Record.Exception(() => AfterFirstDraw.Wait(window.Dispatcher)));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_hidden_after_it_was_shown_is_not_cloaked_when_the_call_returns()
    {
        var window = new Window { Width = 300, Height = 200, Content = new Border() };
        try
        {
            WindowPlacement.            ShowOnPointerScreen(window, Logger.None);
            AfterFirstDraw.Wait(window.Dispatcher);
            window.Hide();

                        WindowPlacement.ShowOnPointerScreen(window, Logger.None);

            Assert.False(Win32.IsCloaked(Handle(window)));
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_window_hidden_while_minimized_is_not_cloaked_once_it_opens()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Show();
            window.WindowState = WindowState.Minimized;
            window.Hide();

                        WindowPlacement.ShowOnPointerScreen(window, Logger.None);
            AfterFirstDraw.Wait(window.Dispatcher);

            Assert.False(Win32.IsCloaked(Handle(window)));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void A_null_logger_is_refused_before_the_window_is_shown()
    {
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            Assert.Throws<ArgumentNullException>(() => WindowPlacement.ShowOnPointerScreen(window, null!));

            Assert.False(window.IsVisible);
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
            WindowPlacement.ShowOnPointerScreen(window, Logger.None);
            var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
            window.Left = work.Left; // Moved away from the center, as when the user dragged it.
            window.Top = work.Top;
            window.Hide();

                        WindowPlacement.ShowOnPointerScreen(window, Logger.None);

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
