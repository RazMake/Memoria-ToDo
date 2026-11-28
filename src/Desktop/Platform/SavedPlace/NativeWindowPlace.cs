using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Reads and sets a window's place with Windows' own saved-place functions, which keep the size before maximizing.
/// Windows gives the place in workspace coordinates, which start at the main screen's work area, not at its corner.
/// </summary>
internal static class NativeWindowPlace
{
    private const int ShowNormal = 1;
    private const int ShowMinimized = 2;
    private const int ShowMaximized = 3;
    private const uint RestoreToMaximized = 0x2;
    private const uint GetWorkArea = 0x30;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacementInfo
    {
        public int Length;
        public uint Flags;
        public int ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(nint window, ref WindowPlacementInfo placement);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPlacement(nint window, ref WindowPlacementInfo placement);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref NativeRect value, uint flags);

    /// <summary>The window's restored rectangle in screen pixels. A minimized window gives the state it had before.</summary>
    /// <exception cref="Win32Exception">Windows can't give the window's place.</exception>
    public static SavedPlace Get(nint window)
    {
        var info = new WindowPlacementInfo { Length = Marshal.SizeOf<WindowPlacementInfo>() };
        if (!GetWindowPlacement(window, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var (originX, originY) = WorkspaceOrigin();
        var rect = info.NormalPosition;
        var maximized = info.ShowCommand == ShowMaximized
            || (info.ShowCommand == ShowMinimized && (info.Flags & RestoreToMaximized) != 0);
        return new SavedPlace(rect.Left + originX, rect.Top + originY, rect.Right - rect.Left, rect.Bottom - rect.Top, maximized);
    }

    /// <summary>Sets the hidden window's restored rectangle and whether it opens maximized.</summary>
    /// <exception cref="Win32Exception">Windows can't set the window's place.</exception>
    public static void Set(nint window, SavedPlace place)
    {
        var (originX, originY) = WorkspaceOrigin();
        var left = place.Left - originX;
        var top = place.Top - originY;
        var info = new WindowPlacementInfo
        {
            Length = Marshal.SizeOf<WindowPlacementInfo>(),
            ShowCommand = place.Maximized ? ShowMaximized : ShowNormal,
            NormalPosition = new NativeRect { Left = left, Top = top, Right = left + place.Width, Bottom = top + place.Height },
        };
        if (!SetWindowPlacement(window, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    // The main screen's work area starts at its corner, offset by a taskbar on the left or top.
    private static (int X, int Y) WorkspaceOrigin()
    {
        var work = new NativeRect();
        if (!SystemParametersInfo(GetWorkArea, 0, ref work, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return (work.Left, work.Top);
    }
}
