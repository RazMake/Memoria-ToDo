using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>
/// Close window keys, with plain test windows that hold a text box. Modifier keys are pressed as key events, as
/// <see cref="SimulatedKey.Chord"/> does, since a test can't change the keyboard's real modifier state. What Set and
/// Attach refuse is unit-tested in CloseWindowKeysSetTests. Not covered: Alt reported as Key.System with the real key
/// in SystemKey, which SimulatedKey can't raise. Each test is one path.
/// </summary>
[UX("WindowHosting")]
[Collection(ScreenTests.Collection)]
public class CloseWindowKeysPathTests
{
    [WpfFact]
    public void Only_attached_windows_close_and_only_the_one_the_keys_were_pressed_in_after_Set_with_the_users_close_command()
    {
        using var first = new ShownWindow();
        using var second = new ShownWindow();
        using var notAttached = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(first.Window);
        keys.Attach(second.Window);

        SimulatedKey.Chord(first.Box, Key.Escape);
        SimulatedKey.Chord(first.Box, Key.LeftCtrl, Key.W);
        Assert.False(first.Closed);

        keys.Set(ModifierKeys.Control, Key.W);
        SimulatedKey.Chord(notAttached.Box, Key.LeftCtrl, Key.W);
        Assert.False(notAttached.Closed);

        SimulatedKey.Chord(first.Box, Key.LeftCtrl, Key.W);

        Assert.True(first.Closed);
        Assert.True(first.GotUsersCloseCommand);
        Assert.False(second.Closed);

        SimulatedKey.Chord(second.Box, Key.LeftCtrl, Key.W);

        Assert.True(second.Closed);
    }

    [WpfFact]
    public void Changing_the_keys_changes_what_closes_a_window_and_a_close_key_goes_no_further()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Shift, Key.Escape);

        SimulatedKey.Chord(shown.Box, Key.Escape);
        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.LeftShift, Key.Escape);

        Assert.False(shown.Closed);
        Assert.Contains(Key.Escape, shown.KeysTheBoxSaw);

        keys.Set(ModifierKeys.None, Key.Escape);
        SimulatedKey.Chord(shown.Box, Key.LeftCtrl);
        shown.KeysTheBoxSaw.Clear();
        var handled = SimulatedKey.Raise(shown.Box, Key.Escape, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

        Assert.True(handled);
        Assert.True(shown.Closed);
        Assert.DoesNotContain(Key.Escape, shown.KeysTheBoxSaw);

        using var other = new ShownWindow();
        keys.Attach(other.Window);
        keys.Set(ModifierKeys.None, Key.F9);

        SimulatedKey.Chord(other.Box, Key.Escape);
        Assert.False(other.Closed);

        SimulatedKey.Chord(other.Box, Key.F9);
        Assert.True(other.Closed);

        using var several = new ShownWindow();
        keys.Attach(several.Window);
        keys.Set(ModifierKeys.Control | ModifierKeys.Alt, Key.W);

        SimulatedKey.Chord(several.Box, Key.LeftCtrl, Key.RightAlt, Key.W);

        Assert.True(several.Closed);
    }

    /// <summary>A showing window holding a text box, closed when the test ends. It records what its text box sees.</summary>
    private sealed class ShownWindow : IDisposable
    {
        public ShownWindow()
        {
            Box = new TextBox();
            Window = new Window { Width = 300, Height = 200, Content = Box };
            Window.Closed += (_, _) => Closed = true;
            Box.PreviewKeyDown += (_, e) => KeysTheBoxSaw.Add(e.Key);
            Box.KeyDown += (_, e) => KeysTheBoxSaw.Add(e.Key);
            Window.Show();
            Box.Focus();
            System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(Window).Handle).AddHook(Watch);
        }

        public bool GotUsersCloseCommand { get; private set; }

        // WM_SYSCOMMAND with SC_CLOSE (the low four bits of wParam are flags): what the close button sends.
        private nint Watch(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            if (message == 0x0112 && (wParam & 0xFFF0) == 0xF060)
            {
                GotUsersCloseCommand = true;
            }

            return 0;
        }

        public Window Window { get; }

        public TextBox Box { get; }

        public bool Closed { get; private set; }

        /// <summary>The keys that reached the text box, in either key-down event.</summary>
        public List<Key> KeysTheBoxSaw { get; } = [];

        public void Dispose() => Window.Close();
    }
}
