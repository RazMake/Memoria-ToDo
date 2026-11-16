using System.Runtime.InteropServices;
using System.Windows;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Sends a real left mouse button press and release to Windows, so the button reads as held to the whole program.</summary>
internal static class MouseInput
{
    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;

    /// <summary>Moves the pointer to the screen point and holds the left button down there.</summary>
    public static void Press(Point screen)
    {
        if (!SetCursorPos((int)screen.X, (int)screen.Y))
        {
            throw new InvalidOperationException($"Windows did not move the pointer (error {Marshal.GetLastWin32Error()}).");
        }

        mouse_event(LeftDown, 0, 0, 0, 0);
    }

    /// <summary>Releases the left button.</summary>
    public static void Release() => mouse_event(LeftUp, 0, 0, 0, 0);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extraInfo);
}
