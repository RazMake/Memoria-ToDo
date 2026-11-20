using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Tests;

/// <summary>
/// A key sent by <see cref="SimulatedKey"/> is the only input a screen test describes, so the real keyboard never changes
/// what the control under test sees. It is a screen test because a key event needs a showing window to come from.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class SimulatedKeyTests
{
    [WpfFact]
    public void A_simulated_key_meets_no_modifier_even_when_the_real_keyboard_holds_Shift()
    {
        var element = new Border { Focusable = true };
        var window = new Window { Content = element, Width = 200, Height = 100 };
        var seen = new List<ModifierKeys>();
        element.KeyDown += (_, _) => seen.Add(Keyboard.Modifiers);
        window.Show();
        var original = ThreadKeyboardState.Read();
        try
        {
            var shiftHeld = (byte[])original.Clone();
            shiftHeld[ThreadKeyboardState.ShiftKey] |= ThreadKeyboardState.Down;
            shiftHeld[ThreadKeyboardState.LeftShiftKey] |= ThreadKeyboardState.Down;
            ThreadKeyboardState.Write(shiftHeld);

            SimulatedKey.Raise(element, Key.Down, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
        }
        finally
        {
            ThreadKeyboardState.Write(original);
            window.Close();
        }

        Assert.Equal([ModifierKeys.None], seen);
    }
}
