using System.Runtime.InteropServices;
using System.Text;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The Windows functions the tests use to look at the screen, the pointer, and other programs' windows.</summary>
internal static class Win32
{
    public const int MonitorDefaultToNearest = 2;
    public const uint WmClose = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size; public Rect Monitor; public Rect Work; public uint Flags;
    }

    private delegate bool EnumWindowsProc(nint window, nint lParam);
    private delegate bool EnumMonitorsProc(nint monitor, nint dc, nint rect, nint lParam);

    public delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct Message { public nint Window; public uint Id; public nint WParam; public nint LParam; public uint Time; public Point Point; }

    public const uint EventSystemDialogStart = 0x0010;
    private const uint EventObjectShow = 0x8002;
    private const uint WineventOutOfContext = 0;
    private const uint WmQuit = 0x0012;

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, int flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, EnumMonitorsProc callback, nint lParam);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    public static Point CursorPosition() => GetCursorPos(out var point) ? point : throw new InvalidOperationException("No pointer position.");

    public static void MoveCursor(Point point) => SetCursorPos(point.X, point.Y);

    public static Rect WindowRect(nint window) => GetWindowRect(window, out var rect) ? rect : throw new InvalidOperationException("No window rectangle.");

    /// <summary>The work area (the screen minus the taskbar), in pixels, of the screen that holds the point.</summary>
    public static Rect WorkAreaOfScreenWith(Point point)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromPoint(point, MonitorDefaultToNearest), ref info)
            ? info.Work
            : throw new InvalidOperationException("No monitor information.");
    }

    /// <summary>The center of each screen, in pixels.</summary>
    public static IReadOnlyList<Point> ScreenCenters()
    {
        var centers = new List<Point>();
        EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                centers.Add(new Point
                {
                    X = info.Monitor.Left + info.Monitor.Width / 2,
                    Y = info.Monitor.Top + info.Monitor.Height / 2,
                });
            }
            return true;
        }, 0);
        return centers;
    }

    /// <summary>The text of a message box: the text of its static controls.</summary>
    public static string MessageBoxText(nint dialog)
    {
        var texts = new List<string>();
        EnumChildWindows(dialog, (child, _) =>
        {
            if (TextOf(GetClassName, child) == "Static")
            {
                texts.Add(TextOf(GetWindowText, child));
            }
            return true;
        }, 0);
        return string.Join('\n', texts);
    }

    public static void CloseWindow(nint window) => PostMessage(window, WmClose, 0, 0);

    public static bool IsVisible(nint window) => IsWindowVisible(window);

    public static int ProcessIdOf(nint window)
    {
        GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    /// <summary>
    /// Calls <paramref name="callback"/> on the calling thread, which must then run <see cref="RunMessageLoop"/>, for the
    /// system's "a dialog started" event and every window's "shown" event, in any process. Returns the hook to remove.
    /// </summary>
    /// <summary>Throws <see cref="System.ComponentModel.Win32Exception"/> when Windows refuses the hook.</summary>
    public static nint HookDialogEvents(WinEventProc callback)
    {
        var hook = SetWinEventHook(EventSystemDialogStart, EventObjectShow, 0, callback, 0, 0, WineventOutOfContext);
        return hook != 0 ? hook : throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void Unhook(nint hook) => UnhookWinEvent(hook);

    public static uint CurrentThreadId() => GetCurrentThreadId();

    /// <summary>Handles the messages of the calling thread, which out-of-context hooks need, until <see cref="StopMessageLoop"/>.</summary>
    public static void RunMessageLoop()
    {
        while (GetMessage(out _, 0, 0, 0) > 0)
        {
        }
    }

    public static void StopMessageLoop(uint threadId) => PostThreadMessage(threadId, WmQuit, 0, 0);

    private static string TextOf(Func<nint, StringBuilder, int, int> read, nint window)
    {
        var text = new StringBuilder(4096);
        read(window, text, text.Capacity);
        return text.ToString();
    }
}
