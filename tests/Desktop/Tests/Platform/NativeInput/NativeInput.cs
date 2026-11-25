using System.Runtime.InteropServices;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The Windows input sender shared by the keyboard and mouse helpers.</summary>
[RealInput]
internal static class NativeInput
{
    private const uint TypeMouse = 0;
    private const uint TypeKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseNoCoalesce = 0x2000;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint MouseAbsolute = 0x8000;

    public static Input Key(ushort virtualKey, bool up) => new()
    {
        Type = TypeKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInputData { VirtualKey = virtualKey, Flags = up ? KeyUp : 0 } },
    };

    public static Input LeftButton(bool down) => new()
    {
        Type = TypeMouse,
        Data = new InputUnion { Mouse = new MouseInputData { Flags = down ? MouseLeftDown : MouseLeftUp } },
    };

    public static Input Move(Win32.Point point, nint extraInfo = 0)
    {
        if (!Win32.IsOnScreen(point))
        {
            throw new ArgumentOutOfRangeException(nameof(point), $"({point.X}, {point.Y}) is on no screen.");
        }

        var (x, y) = AbsolutePointerCoordinates.Normalize(point, Win32.VirtualScreen());
        return new Input
        {
            Type = TypeMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInputData { Dx = x, Dy = y, Flags = MouseMove | MouseAbsolute | MouseVirtualDesk | MouseNoCoalesce, ExtraInfo = extraInfo },
            },
        };
    }

    public static uint Send(params Input[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Length == 0)
        {
            throw new ArgumentException("There is no input to send.", nameof(inputs));
        }

        var inserted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        InsertedInput.Require(inserted, inputs.Length, Marshal.GetLastWin32Error());
        return inserted;
    }

    // INPUT's union must retain native alignment; its mouse member is larger than its keyboard member.
    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInputData Mouse;
        [FieldOffset(0)] public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
