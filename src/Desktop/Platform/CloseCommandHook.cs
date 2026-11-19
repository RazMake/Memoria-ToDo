using System.Windows;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Tells the user's close command from the application ending. Windows sends SC_CLOSE for the close button, Alt+F4 and the
/// window menu's Close, and only then does the window ask. The application ending sends a plain WM_CLOSE, which closes without
/// asking: Exit and signing out discard the edit.
/// </summary>
public static class CloseCommandHook
{
    /// <summary>
    /// Calls <paramref name="userClose"/> instead of closing when the user closes the window. WPF can't close a window while its
    /// modal prompt shows, so a plain close closes the owned windows first, answering Cancel. Call it once the window has a handle.
    /// </summary>
    public static void Attach(Window window, Action userClose) =>
        HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook((nint _, int message, nint wParam, nint _, ref bool handled) =>
        {
            if (message == CloseMessages.WmSysCommand && (wParam & CloseMessages.SysCommandMask) == CloseMessages.ScClose)
            {
                handled = true;
                userClose();
            }
            else if (message == CloseMessages.WmClose)
            {
                foreach (var owned in window.OwnedWindows.Cast<Window>().ToList())
                {
                    owned.Close();
                }
            }

            return 0;
        });
}
