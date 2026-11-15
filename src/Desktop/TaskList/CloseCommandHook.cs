using System.Windows;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Tells the user's close command from the application ending. Windows sends SC_CLOSE for the close button, Alt+F4 and the
/// window menu's Close, and only then does the window ask. The application ending sends a plain WM_CLOSE, which closes without
/// asking: Exit and signing out discard the edit.
/// </summary>
internal static class CloseCommandHook
{
    private const int WmClose = 0x0010;
    private const int WmSysCommand = 0x0112;
    private const int ScClose = 0xF060;

    /// <summary>
    /// Calls <paramref name="userClose"/> instead of closing when the user closes the window. WPF can't close a window while its
    /// modal prompt shows, so a plain close closes the owned windows first, answering Cancel. Call it once the window has a handle.
    /// </summary>
    public static void Attach(Window window, Action userClose) =>
        HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook((nint _, int message, nint wParam, nint _, ref bool handled) =>
        {
            if (message == WmSysCommand && (wParam & 0xFFF0) == ScClose)
            {
                handled = true;
                userClose();
            }
            else if (message == WmClose)
            {
                foreach (var owned in window.OwnedWindows.Cast<Window>().ToList())
                {
                    owned.Close();
                }
            }

            return 0;
        });
}
