using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>What Windows says about screens: their work areas in device pixels and their scale (1.5 is 150%).</summary>
internal static class Monitors
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

    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, nint rect, nint data);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(nint deviceContext, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    /// <summary>The screen nearest the pixel position.</summary>
    public static nint Nearest(int x, int y) => MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToNearest);

    /// <exception cref="Win32Exception">Windows can't give the screen's work area.</exception>
    public static Rect WorkAreaOf(nint monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var work = info.Work;
        return new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
    }

    /// <exception cref="Win32Exception">Windows can't give the screen's scale.</exception>
    public static double ScaleOf(nint monitor)
    {
        var result = GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _);
        if (result != 0)
        {
            // The failure is a Win32 error wrapped in an HRESULT: its code is the low 16 bits.
            throw new Win32Exception(result & 0xFFFF);
        }

        return dpi / WpfUnitDpi;
    }

    /// <summary>The work area of every screen now.</summary>
    /// <exception cref="Win32Exception">Windows can't list the screens or give a work area.</exception>
    public static IReadOnlyList<Rect> WorkAreas()
    {
        var monitors = new List<nint>();
        if (!EnumDisplayMonitors(0, 0, (monitor, _, _, _) => { monitors.Add(monitor); return true; }, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return monitors.ConvertAll(WorkAreaOf);
    }
}
