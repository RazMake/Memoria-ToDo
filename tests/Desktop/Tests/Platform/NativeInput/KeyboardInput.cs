using System.Runtime.InteropServices;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Sends real key presses to Windows, as a keyboard does, so a registered shortcut can be pressed.</summary>
[RealInput]
internal static class KeyboardInput
{
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Alt = 0x12;
    public const ushort Escape = 0x1B;
    public const ushort Tab = 0x09;
    public const ushort Enter = 0x0D;
    public const ushort A = 0x41;
    public const ushort F5 = 0x74;

    /// <summary>Presses the keys in order, then releases them in reverse order.</summary>
    public static void Chord(params ushort[] virtualKeys)
    {
        ArgumentNullException.ThrowIfNull(virtualKeys);
        var inputs = new NativeInput.Input[virtualKeys.Length * 2];
        for (var i = 0; i < virtualKeys.Length; i++)
        {
            inputs[i] = NativeInput.Key(virtualKeys[i], up: false);
            inputs[inputs.Length - 1 - i] = NativeInput.Key(virtualKeys[i], up: true);
        }

        NativeInput.Send(inputs);
    }

    /// <summary>
    /// Makes the window the one in front, which gets the key presses and the pointer's moves. Windows lets a process take the
    /// foreground only when it got the last input, so a key no app uses is pressed first, unless the window is already in front;
    /// a window that still isn't in front fails at once, before any key or move goes elsewhere.
    /// </summary>
    public static void BringToFront(nint window)
    {
        if (Win32.ForegroundWindow() == window)
        {
            return;
        }

        Chord(Unassigned);
        if (!SetForegroundWindow(window) || Win32.ForegroundWindow() != window)
        {
            throw new InvalidOperationException("Windows kept another window in front, so key presses and pointer moves wouldn't reach the window.");
        }
    }

    // F24: no keyboard has it, and no app reacts to it.
    private const ushort Unassigned = 0x87;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
}
