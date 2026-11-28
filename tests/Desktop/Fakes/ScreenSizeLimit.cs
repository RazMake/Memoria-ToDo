using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Lets a test make a window wider or taller than the screen. Windows keeps a window no larger than the screen, so a
/// layout measured at a set width would differ between a large monitor and the workflow runner's 1024 by 768 screen.
/// Shared test setup.
/// </summary>
public static class ScreenSizeLimit
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmNcDestroy = 0x0082;
    private const int Largest = 32767;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Point Reserved; public Point MaxSize; public Point MaxPosition; public Point MinTrackSize; public Point MaxTrackSize; }

    private delegate nint SubclassProc(nint window, int message, nint wParam, nint lParam, nuint id, nuint data);

    // Kept in a field so the collector never frees the callback Windows holds.
    private static readonly SubclassProc Subclass = RaiseLimit;

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, ref MinMaxInfo lParam);

    /// <summary>
    /// Lifts the limit on the showing window, so each width and height the test sets from now on is the one it gets.
    /// The size the window opened with stays as the screen cut it: WPF replaces the asked-for size with the cut one.
    /// </summary>
    public static void Lift(Window window)
    {
        var source = PresentationSource.FromVisual(window) as HwndSource
            ?? throw new InvalidOperationException("The window isn't showing.");
        if (!SetWindowSubclass(source.Handle, Subclass, 1, 0))
        {
            throw new InvalidOperationException("Windows refused to subclass the window.");
        }

        // WPF keeps the limit from the last time Windows asked, at the window's creation, so ask again.
        var limits = new MinMaxInfo();
        SendMessage(source.Handle, WmGetMinMaxInfo, 0, ref limits);
    }

    // Raises the limit before WPF reads it: WPF keeps the limit it sees and sizes the window within it.
    private static nint RaiseLimit(nint window, int message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == WmGetMinMaxInfo)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            info.MaxTrackSize = new Point { X = Largest, Y = Largest };
            Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
        }
        else if (message == WmNcDestroy)
        {
            RemoveWindowSubclass(window, Subclass, id);
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }
}
