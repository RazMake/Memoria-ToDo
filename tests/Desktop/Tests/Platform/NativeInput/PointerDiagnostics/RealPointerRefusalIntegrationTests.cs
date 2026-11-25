using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Integration]
[Collection(ScreenTests.Collection)]
public class RealPointerRefusalIntegrationTests
{
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [WpfFact]
    public void Refuses_a_step_away_that_an_owned_window_covers_without_moving_the_pointer_and_reports_which_step_it_was()
    {
        var element = new Border { Width = 300, Height = 100, Background = System.Windows.Media.Brushes.White };
        var window = new Window { Content = element, Width = 400, Height = 300, WindowStartupLocation = WindowStartupLocation.Manual, Left = 50, Top = 50 };
        Window? cover = null;
        try
        {
            window.Show();
            window.UpdateLayout();
            cover = new Window
            {
                Owner = window, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
                ShowActivated = false, Topmost = true, Background = System.Windows.Media.Brushes.Red, Width = 20, Height = 20,
            };
            var inElement = new Point(30, 20);
            var onScreen = element.PointToScreen(inElement);
            var target = new Win32.Point { X = (int)Math.Round(onScreen.X), Y = (int)Math.Round(onScreen.Y) };

            // The step away is 3 pixels to the right of a pointer already at the target. The cover starts 2 pixels right of the
            // target, so it covers that step but never the target itself.
            cover.Show();
            var coverWindow = new WindowInteropHelper(cover).Handle;
            Assert.True(SetWindowPos(coverWindow, 0, target.X + 2, target.Y - 10, 40, 20, NoZOrder | NoActivate), "The cover could not be placed.");
            var away = new Win32.Point { X = target.X + 3, Y = target.Y };
            Win32.MoveCursor(target);
            var before = Win32.CursorPosition();
            Assert.Equal(coverWindow, Win32.WindowAt(away));
            Assert.NotEqual(coverWindow, Win32.WindowAt(target));

            var failure = Assert.Throws<InvalidOperationException>(() => RealPointer.MoveOver(window, element, inElement));

            var after = Win32.CursorPosition();
            Assert.Equal((before.X, before.Y), (after.X, after.Y));
            Assert.Contains(PointerTrace.Label(1, "away"), failure.Message);
            Assert.Contains(PointerTrace.Refused, failure.Message);
            Assert.Contains(PointerTrace.AtFailure, failure.Message);
            Assert.Contains($"{coverWindow:X}", failure.Message);
        }
        finally
        {
            cover?.Close();
            window.Close();
        }
    }
}