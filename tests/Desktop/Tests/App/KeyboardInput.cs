using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Sends real key presses to Windows, as a keyboard does, so a registered shortcut can be pressed.</summary>
internal static class KeyboardInput
{
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Alt = 0x12;

    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;

    /// <summary>Presses the keys in order, then releases them in reverse order.</summary>
    public static void Chord(params ushort[] virtualKeys)
    {
        var inputs = new List<Input>();
        inputs.AddRange(virtualKeys.Select(key => Key(key, up: false)));
        inputs.AddRange(virtualKeys.Reverse().Select(key => Key(key, up: true)));
        var array = inputs.ToArray();
        if (SendInput((uint)array.Length, array, Marshal.SizeOf<Input>()) != array.Length)
        {
            throw new InvalidOperationException($"Windows took only part of the key presses (error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static Input Key(ushort virtualKey, bool up) => new()
    {
        Type = InputKeyboard,
        Keyboard = new KeyboardInputData { VirtualKey = virtualKey, Flags = up ? KeyUp : 0 },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInputData Keyboard;
    }

    // Padded to the size of the union Windows expects: its mouse member is the largest.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct KeyboardInputData
    {
        [FieldOffset(0)] public ushort VirtualKey;
        [FieldOffset(2)] public ushort ScanCode;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint Time;
        [FieldOffset(16)] public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
