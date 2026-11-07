using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Opens windows on the screen with the mouse pointer.</summary>
public static class WindowPlacement
{
    private const uint NoSize = 0x1;
    private const uint NoZOrder = 0x4;
    private const uint NoActivate = 0x10;
    private const int Hide = 0;
    private const int ShowNoActivate = 4;
    private const int Cloak = 13;

    /// <summary>
    /// A hidden window: shown centered on the work area of the screen with the mouse pointer, then activated.
    /// A visible window stays where it is: restored if minimized, then activated.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called off the window's UI thread.</exception>
    /// <exception cref="Win32Exception">Windows can't give the pointer's position, or its screen's work area or scale, or move or restore the window.</exception>
    public static void ShowOnPointerScreen(Window window)
    {
        window.VerifyAccess();

        if (window.IsVisible)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
        }
        else
        {
            var (work, scale) = PointerScreen.Current();
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            // Covers a window whose WPF state is Minimized but whose native window never was (state set while hidden).
            window.WindowState = WindowState.Normal;

            var shownBefore = PresentationSource.FromVisual(window) is not null;
            var handle = new WindowInteropHelper(window).EnsureHandle();

            if (IsIconic(handle))
            {
                RestoreWhileHidden(handle);
            }

            // Move the handle to the pointer's screen first, in pixels: WPF converts Left and Top at the scale of the
            // screen the handle is on, and picks a new window's screen from Left and Top in WPF units, which can
            // lie inside two screens. When the scale differs, Windows sends WM_DPICHANGED while this call runs, and
            // WPF then rescales the window and moves it to the rectangle Windows suggests (HwndTarget.OnDpiChanged).
            // That is why the centered position is set after this move, never before.
            MoveTo(handle, (int)work.Left, (int)work.Top);

            if (shownBefore)
            {
                // Hidden since it was shown: it keeps its size, and showing it again lays nothing out.
                var (left, top) = CenteredPlacement.TopLeft(work, scale, new Size(window.ActualWidth, window.ActualHeight));
                MoveTo(handle, left, top);
                window.Show();
            }
            else
            {
                // Show lays out the content (which sizes a size-to-fit window) and only then shows the window.
                // The first size change comes after that layout and before the window is visible, so the real
                // size is known and the window moves without a visible jump.
                void CenterBySize(object sender, SizeChangedEventArgs e)
                {
                    window.SizeChanged -= CenterBySize;
                    var (left, top) = CenteredPlacement.TopLeft(work, scale, e.NewSize);
                    MoveTo(handle, left, top);
                }

                window.SizeChanged += CenterBySize;
                try
                {
                    window.Show();
                }
                finally
                {
                    window.SizeChanged -= CenterBySize;
                }
            }
        }

        window.Activate();
    }

    // Windows can't restore a minimized window without showing it, and WPF ignores a state change while the window is
    // hidden. So the window is restored while cloaked (invisible to the user), then hidden again.
    private static void RestoreWhileHidden(nint handle)
    {
        SetCloaked(handle, true);
        try
        {
            ShowWindow(handle, ShowNoActivate);
            ShowWindow(handle, Hide);
        }
        finally
        {
            SetCloaked(handle, false);
        }
    }

    private static void SetCloaked(nint handle, bool cloaked)
    {
        var value = cloaked ? 1 : 0;
        var result = DwmSetWindowAttribute(handle, Cloak, ref value, sizeof(int));
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    private static void MoveTo(nint handle, int left, int top)
    {
        if (!SetWindowPos(handle, 0, left, top, 0, 0, NoSize | NoZOrder | NoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int left, int top, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
