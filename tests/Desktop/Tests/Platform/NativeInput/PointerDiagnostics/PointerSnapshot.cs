using System.ComponentModel;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Reads pointer, window, and capture state without recording window text or titles.</summary>
internal static class PointerSnapshot
{
    public static string Take(Win32.Point point, nint target)
    {
        var testThread = Win32.CurrentThreadId();
        var foreground = Win32.ForegroundWindow();
        return string.Join("; ",
            Cursor(),
            PointerReading.Of("window at the point", () => Window(Win32.WindowAt(point), target)),
            PointerReading.Of("foreground window", () => Window(foreground, target)),
            PointerReading.Of("foreground window thread", () => foreground == 0 ? "none" : Win32.ReadOwnerOf(foreground).Thread.ToString()),
            PointerReading.Of("target window thread", () => Win32.ReadOwnerOf(target).Thread.ToString()),
            PointerReading.Of("test thread", () => testThread.ToString()),
            PointerReading.Of("WPF capture", () => TypeName(Mouse.Captured)),
            PointerReading.Of("test thread native capture", () => Window(Win32.NativeCapture(), target)),
            PointerReading.Of("test thread GUI info", () => Gui(testThread, target)),
            PointerReading.Of("foreground thread GUI info", () => foreground == 0 ? "none" : Gui(Win32.ReadOwnerOf(foreground).Thread, target)));
    }

    public static string Cursor() => PointerReading.Of("cursor", () =>
    {
        var cursor = Win32.ReadCursorPosition();
        return $"({cursor.X}, {cursor.Y})";
    });

    public static string TypeName(object? element) => element?.GetType().Name ?? "none";

    public static string Window(nint window, nint target)
    {
        if (window == 0)
        {
            return "none";
        }

        if (window == target)
        {
            return $"{window:X} (target)";
        }

        try
        {
            return $"{window:X} ({(Win32.ReadOwnerOf(window).Process == Environment.ProcessId ? "same process" : "other")})";
        }
        catch (Win32Exception unknown)
        {
            return $"{window:X} (owner unavailable, error {unknown.NativeErrorCode})";
        }
    }

    private static string Gui(uint thread, nint target)
    {
        var info = Win32.GuiThread(thread);
        return $"thread {thread} active {Window(info.Active, target)}, focus {Window(info.Focus, target)}, capture {Window(info.Capture, target)}, "
            + $"menu owner {Window(info.MenuOwner, target)}, move/size {Window(info.MoveSize, target)}, flags 0x{info.Flags:X}";
    }
}
