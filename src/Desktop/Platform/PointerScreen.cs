using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The work area of the screen with the mouse pointer, in device pixels, and that screen's scale (1.5 is 150%).</summary>
internal static class PointerScreen
{
    private const int MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;
    private const double WpfUnitDpi = 96;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    /// <exception cref="Win32Exception">Windows can't give the pointer's position or the screen's work area or scale.</exception>
    public static (Rect WorkPixels, double Scale) Current()
    {
        if (!GetCursorPos(out var pointer))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitor = MonitorFromPoint(pointer, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var result = GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _);
        if (result != 0)
        {
            // The failure is a Win32 error wrapped in an HRESULT: its code is the low 16 bits.
            throw new Win32Exception(result & 0xFFFF);
        }

        var work = info.Work;
        return (new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top), dpi / WpfUnitDpi);
    }
}
