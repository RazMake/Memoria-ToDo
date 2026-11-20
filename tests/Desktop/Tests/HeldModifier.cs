using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop;

/// <summary>
/// Makes the calling thread's keyboard state say a modifier is held while an action runs, so a key event raised in the action meets
/// the modifier that WPF's own controls read through <see cref="Keyboard.Modifiers"/>. It writes only this thread's key-state table
/// (SetKeyboardState), never sends input to Windows, and puts the exact table back afterwards. Shared test setup, not a fake.
/// </summary>
internal static class HeldModifier
{
    private const int KeyCount = 256;
    private const byte Down = 0x80;

    /// <summary>
    /// Runs the action with the modifier held, then restores the thread's keyboard state before returning. The action must raise its
    /// events synchronously and not pump the dispatcher: a pumped message would overwrite the temporary state.
    /// </summary>
    /// <param name="modifier">Alt or Control.</param>
    /// <exception cref="Win32Exception">Windows refused to read or write the keyboard state.</exception>
    /// <exception cref="InvalidOperationException">WPF doesn't report the modifier as held after the state was written.</exception>
    public static void While(ModifierKeys modifier, Action action)
    {
        var (generic, left) = modifier switch
        {
            ModifierKeys.Alt => (0x12, 0xA4),
            ModifierKeys.Control => (0x11, 0xA2),
            _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Only Alt and Control can be held."),
        };

        var original = new byte[KeyCount];
        if (!GetKeyboardState(original))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows wouldn't give the thread's keyboard state.");
        }

        var held = (byte[])original.Clone();
        held[generic] |= Down;
        held[left] |= Down;
        try
        {
            if (!SetKeyboardState(held))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows wouldn't take the thread's keyboard state.");
            }

            if ((Keyboard.Modifiers & modifier) == 0)
            {
                throw new InvalidOperationException($"WPF doesn't see {modifier} as held, so it cannot be put to the control under test.");
            }

            action();
        }
        finally
        {
            if (!SetKeyboardState(original))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows wouldn't take back the thread's original keyboard state.");
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetKeyboardState(byte[] state);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetKeyboardState(byte[] state);
}
