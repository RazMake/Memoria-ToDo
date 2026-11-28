using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Reads and writes the calling thread's key-state table, the one <see cref="System.Windows.Input.Keyboard.Modifiers"/> reads.
/// It never sends input to Windows. Shared test setup, not a fake.
/// </summary>
public static class ThreadKeyboardState
{
    public const int KeyCount = 256;
    public const byte Down = 0x80;
    public const int ShiftKey = 0x10;
    public const int LeftShiftKey = 0xA0;

    /// <exception cref="Win32Exception">Windows refused to read the keyboard state.</exception>
    public static byte[] Read()
    {
        var state = new byte[KeyCount];
        if (!GetKeyboardState(state))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows wouldn't give the thread's keyboard state.");
        }

        return state;
    }

    /// <exception cref="Win32Exception">Windows refused to write the keyboard state.</exception>
    public static void Write(byte[] state)
    {
        if (!SetKeyboardState(state))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows wouldn't take the thread's keyboard state.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetKeyboardState(byte[] state);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetKeyboardState(byte[] state);
}
