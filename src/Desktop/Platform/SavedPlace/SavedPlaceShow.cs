using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Opens a new window at its saved place when that place is on a screen now, and saves its place when it closes.</summary>
internal static class SavedPlaceShow
{
    private const int DestroyMessage = 0x0002;

    /// <summary>
    /// Reads the saved place, then shows the new hidden window there, placed while it's cloaked for its first show, or with
    /// <see cref="WindowPlacement.ShowOnPointerScreen"/> when there's no place or it's on no screen. Saves the window's place
    /// when it closes, however that place was chosen; a failed save is logged and the window still closes.
    /// </summary>
    /// <exception cref="Win32Exception">Windows can't give the screens or the window's place, or place, move, or cloak the window.</exception>
    public static void Show(Window window, SavedWindowPlacement placement, Serilog.ILogger logger)
    {
        window.VerifyAccess();
        SaveOnClose(window, placement, logger);

        var chosen = Choose(window, placement.Read(), logger);
        if (chosen is null)
        {
            WindowPlacement.ShowOnPointerScreen(window, logger);
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = chosen.Value.Maximized ? WindowState.Maximized : WindowState.Normal;
        var handle = new WindowInteropHelper(window).EnsureHandle();
        FirstShowCloak.Start(window, logger);
        NativeWindowPlace.Set(handle, chosen.Value);
        window.Show();
        window.Activate();
    }

    private static SavedPlace? Choose(Window window, SavedPlace? saved, Serilog.ILogger logger)
    {
        if (saved is not { } place)
        {
            logger.Debug("Showing the window {Window} on the pointer's screen: it has no saved place", window.GetType().Name);
            return null;
        }

        // The window has no handle yet, so its minimum is in pixels at the scale of the screen the place is on.
        var scale = Monitors.ScaleOf(Monitors.Nearest(place.Left + place.Width / 2, place.Top + place.Height / 2));
        var chosen = SavedPlaceChoice.Choose(place, Monitors.WorkAreas(), new Size(window.MinWidth * scale, window.MinHeight * scale));
        if (chosen is null)
        {
            logger.Debug("Showing the window {Window} on the pointer's screen: its saved place {Place} is on no screen now", window.GetType().Name, place);
        }
        else
        {
            logger.Debug("Showing the window {Window} at its saved place {Place}, as {Chosen}", window.GetType().Name, place, chosen);
        }

        return chosen;
    }

    private static void SaveOnClose(Window window, SavedWindowPlacement placement, Serilog.ILogger logger)
    {
        // Subscribed before the handle exists; the handle is still valid while Windows destroys the window.
        window.SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
            {
                if (message == DestroyMessage)
                {
                    try
                    {
                        placement.Write(NativeWindowPlace.Get(hwnd));
                    }
                    catch (Win32Exception e)
                    {
                        logger.Warning(e, "The place of the window {Window} can't be read, so it isn't saved", window.GetType().Name);
                    }
                }

                return 0;
            });
        };
    }
}
