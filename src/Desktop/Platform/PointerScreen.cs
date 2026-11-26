using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The work area of the screen with the mouse pointer, in device pixels, and that screen's scale (1.5 is 150%).</summary>
internal static class PointerScreen
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    /// <exception cref="Win32Exception">Windows can't give the pointer's position or the screen's work area or scale.</exception>
    public static (Rect WorkPixels, double Scale) Current()
    {
        if (!GetCursorPos(out var pointer))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitor = Monitors.Nearest(pointer.X, pointer.Y);
        return (Monitors.WorkAreaOf(monitor), Monitors.ScaleOf(monitor));
    }
}
