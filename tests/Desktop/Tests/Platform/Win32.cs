using System.Runtime.InteropServices;
using System.Text;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The Windows functions the tests use to look at the screen, the pointer, and other programs' windows.</summary>
internal static class Win32
{
    public const int MonitorDefaultToNearest = 2;
    private const int MonitorDefaultToNull = 0;
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

    public delegate nint MessageHook(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseHook { public Point Point; public nint Window; public uint HitTest; public nint ExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct GuiThreadInfo
    {
        public int Size; public uint Flags; public nint Active; public nint Focus; public nint Capture;
        public nint MenuOwner; public nint MoveSize; public nint Caret; public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Message { public nint Window; public uint Id; public nint WParam; public nint LParam; public uint Time; public Point Point; }

    public const uint EventSystemDialogStart = 0x0010;
    private const uint EventObjectShow = 0x8002;
    private const uint WineventOutOfContext = 0;
    private const uint WmQuit = 0x0012;

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
    [RealInput, DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, MessageHook callback, nint module, uint threadId);
    [RealInput, DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(nint hook);
    [RealInput, DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [RealInput, DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern uint GetQueueStatus(uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, int flags);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, EnumMonitorsProc callback, nint lParam);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    private const int DwmwaCloaked = 14;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);

    /// <summary>Whether Windows hides the window from the screen although WPF may count it as visible.</summary>
    public static bool IsCloaked(nint window)
    {
        var result = DwmGetWindowAttribute(window, DwmwaCloaked, out var cloaked, sizeof(int));
        return result == 0 ? cloaked != 0 : throw new System.ComponentModel.Win32Exception(result);
    }

    public static Point CursorPosition() => GetCursorPos(out var point) ? point : throw new InvalidOperationException("No pointer position.");

    [RealInput]
    public static nint HookMouseMessages(MessageHook callback)
    {
        var hook = SetWindowsHookEx(WhMouse, callback, 0, GetCurrentThreadId());
        return hook != 0 ? hook : throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private const int WhMouse = 7;
    private const int ErrorInvalidWindowHandle = 1400;
    // Include the touch and pointer queue bits on modern Windows.
    private const uint QsAllInput = 0x1CFF;

    [RealInput]
    public static void UnhookMessages(nint hook)
    {
        if (!UnhookWindowsHookEx(hook))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [RealInput]
    public static nint NextMessage(int code, nint message, nint data) => CallNextHookEx(0, code, message, data);

    /// <summary>
    /// Puts the pointer at the point. Throws when Windows refuses, or puts the pointer more than a pixel away, as it does past a
    /// screen's edge. A pixel is allowed because this process isn't per-monitor DPI aware, so Windows rounds the point on a screen
    /// scaled unlike the main one.
    /// </summary>
    [RealInput]
    public static void MoveCursor(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        var moved = CursorPosition();
        if (Math.Abs(moved.X - point.X) > 1 || Math.Abs(moved.Y - point.Y) > 1)
        {
            throw new InvalidOperationException($"Windows put the pointer at ({moved.X}, {moved.Y}), not at ({point.X}, {point.Y}).");
        }
    }

    public static bool IsOnScreen(Point point) => MonitorFromPoint(point, MonitorDefaultToNull) != 0;

    public static Rect VirtualScreen()
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        return new Rect
        {
            Left = left,
            Top = top,
            Right = left + GetSystemMetrics(SmCxVirtualScreen),
            Bottom = top + GetSystemMetrics(SmCyVirtualScreen),
        };
    }

    public static nint ForegroundWindow() => GetForegroundWindow();

    public static nint NativeCapture() => GetCapture();

    public static Point ReadCursorPosition() =>
        GetCursorPos(out var point) ? point : throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

    public static (uint Thread, int Process) ReadOwnerOf(nint window)
    {
        var thread = GetWindowThreadProcessId(window, out var process);
        if (thread != 0)
        {
            return (thread, process);
        }

        var error = Marshal.GetLastWin32Error();
        throw new System.ComponentModel.Win32Exception(error != 0 ? error : ErrorInvalidWindowHandle);
    }

    public static GuiThreadInfo GuiThread(uint thread)
    {
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(thread, ref info) ? info : throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// The current queue status (the high word). Reading it changes the thread's new-message bookkeeping.
    /// </summary>
    public static uint PendingInput() => GetQueueStatus(QsAllInput) >> 16;

    public static bool IsChildOf(nint window, nint parent) => IsChild(parent, window);

    /// <summary>The window on top at the point, which gets the pointer's moves there.</summary>
    public static nint WindowAt(Point point) => WindowFromPoint(point);

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

    public static void Unhook(nint hook)
    {
        if (!UnhookWinEvent(hook))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static uint CurrentThreadId() => GetCurrentThreadId();

    private const uint WmUser = 0x0400;
    private const uint PmNoRemove = 0;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x102;

    public static void CreateMessageQueue() => PeekMessage(out _, 0, WmUser, WmUser, PmNoRemove);

    /// <summary>Handles the messages of the calling thread, which hooks need, until <see cref="StopMessageLoop"/>.</summary>
    public static void RunMessageLoop()
    {
        while (true)
        {
            var result = GetMessage(out _, 0, 0, 0);
            if (result == 0)
            {
                return;
            }

            if (result == -1)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    public static void StopMessageLoop(uint threadId)
    {
        if (!PostThreadMessage(threadId, WmQuit, 0, 0))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Waits without pumping the caller's STA messages.</summary>
    public static bool WaitWithoutPumping(WaitHandle handle, TimeSpan bound)
    {
        var safe = handle.SafeWaitHandle;
        var added = false;
        try
        {
            safe.DangerousAddRef(ref added);
            var milliseconds = (uint)Math.Min(Math.Ceiling(bound.TotalMilliseconds), uint.MaxValue - 1);
            return WaitForSingleObject(safe.DangerousGetHandle(), milliseconds) switch
            {
                WaitObject0 => true,
                WaitTimeout => false,
                _ => throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()),
            };
        }
        finally
        {
            if (added)
            {
                safe.DangerousRelease();
            }
        }
    }

    private static string TextOf(Func<nint, StringBuilder, int, int> read, nint window)
    {
        var text = new StringBuilder(4096);
        read(window, text, text.Capacity);
        return text.ToString();
    }
}
