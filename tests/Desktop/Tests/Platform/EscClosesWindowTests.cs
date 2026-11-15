using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>EscCloses on a plain test window with one control inside, as View Tasks and Configure use it.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class EscClosesWindowTests
{
    [WpfFact]
    public void Esc_that_no_control_handled_closes_the_window()
    {
        using var screen = new EscScreen();

        screen.Press(Key.Escape);

        Assert.True(screen.Closed);
    }

    [WpfFact]
    public void Esc_that_a_control_handled_leaves_the_window_open()
    {
        using var screen = new EscScreen(controlHandlesEsc: true);

        screen.Press(Key.Escape);

        Assert.False(screen.Closed);
    }

    [WpfTheory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.LeftAlt)]
    public void Esc_with_a_modifier_key_held_leaves_the_window_open(Key modifier)
    {
        using var screen = new EscScreen();

        screen.Press(modifier, Key.Escape);

        Assert.False(screen.Closed);
    }

    [WpfFact]
    public void Esc_with_a_modifier_physically_held_before_the_window_got_focus_leaves_it_open()
    {
        using var screen = new EscScreen(physicalModifiers: () => ModifierKeys.Control | ModifierKeys.Alt);

        screen.Press(Key.Escape);

        Assert.False(screen.Closed);
    }

    [WpfFact]
    public void Esc_with_no_modifier_physically_held_closes_the_window()
    {
        using var screen = new EscScreen(physicalModifiers: () => ModifierKeys.None);

        screen.Press(Key.Escape);

        Assert.True(screen.Closed);
    }

    [WpfFact]
    public void Another_key_leaves_the_window_open()
    {
        using var screen = new EscScreen();

        screen.Press(Key.Enter);

        Assert.False(screen.Closed);
    }

    /// <summary>A shown window holding a text box, which can be told to handle Esc as an open list does.</summary>
    private sealed class EscScreen : IDisposable
    {
        private readonly OpenWindows windows = new();
        private readonly Window window = new() { Width = 300, Height = 200 };
        private readonly TextBox box = new();

        public EscScreen(bool controlHandlesEsc = false, Func<ModifierKeys>? physicalModifiers = null)
        {
            window.Content = box;
            if (controlHandlesEsc)
            {
                box.KeyDown += (_, e) => e.Handled = e.Key == Key.Escape;
            }

            if (physicalModifiers is null)
            {
                EscCloses.Attach(window);
            }
            else
            {
                EscCloses.Attach(window, physicalModifiers);
            }
            window.Show();
            Keyboard.Focus(box);
        }

        public bool Closed => !window.IsVisible;

        /// <summary>Presses the keys on the text box in order and releases them in reverse order.</summary>
        public void Press(params Key[] keys) => SimulatedKey.Chord(box, keys);

        public void Dispose() => windows.Dispose();
    }
}
