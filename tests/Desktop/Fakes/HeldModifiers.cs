using System.ComponentModel;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Makes the calling thread's keyboard state say exactly the given modifiers are held while an action runs, so a key event
/// raised in the action meets those modifiers, as WPF's controls read them through <see cref="Keyboard.Modifiers"/>, and not
/// whatever the real keyboard holds. It writes only this thread's key-state table (SetKeyboardState), never sends input to
/// Windows, and puts the exact table back afterwards. Shared test setup, not a fake.
/// </summary>
public static class HeldModifiers
{
    // WPF reads the left and right codes (and no generic one for Win); a real key press sets the generic code too.
    // Held puts down the generic and the left code; the others are cleared whenever the modifier isn't held.
    private static readonly (ModifierKeys Modifier, int[] Pressed, int[] Released)[] Codes =
    [
        (ModifierKeys.Shift, [0x10, 0xA0], [0xA1]),
        (ModifierKeys.Control, [0x11, 0xA2], [0xA3]),
        (ModifierKeys.Alt, [0x12, 0xA4], [0xA5]),
        (ModifierKeys.Windows, [0x5B], [0x5C]),
    ];

    [ThreadStatic]
    private static ModifierKeys _current;

    /// <summary>The modifiers the enclosing <see cref="While"/> holds on this thread, or none outside one.</summary>
    internal static ModifierKeys Current => _current;

    /// <summary>
    /// Runs the action with exactly the given modifiers held, then restores the thread's keyboard state before returning, even
    /// when the action throws. The action must raise its events synchronously and not pump the dispatcher: a pumped message
    /// would overwrite the temporary state.
    /// </summary>
    /// <exception cref="Win32Exception">Windows refused to read or write the keyboard state.</exception>
    /// <exception cref="InvalidOperationException">WPF doesn't report exactly these modifiers after the state was written.</exception>
    public static void While(ModifierKeys modifiers, Action action)
    {
        var original = ThreadKeyboardState.Read();
        var previous = _current;
        var held = (byte[])original.Clone();
        foreach (var (modifier, pressed, released) in Codes)
        {
            var isHeld = (modifiers & modifier) != 0;
            foreach (var key in pressed)
            {
                held[key] = isHeld ? ThreadKeyboardState.Down : (byte)0;
            }

            foreach (var key in released)
            {
                held[key] = 0;
            }
        }

        try
        {
            ThreadKeyboardState.Write(held);
            _current = modifiers;

            if (Keyboard.Modifiers != modifiers)
            {
                throw new InvalidOperationException($"WPF sees {Keyboard.Modifiers} as held, not {modifiers}, so the control under test can't be given exactly those. A key held on the real keyboard can do this: leave the keyboard alone while screen tests run.");
            }

            action();
        }
        finally
        {
            _current = previous;
            ThreadKeyboardState.Write(original);
        }
    }
}
