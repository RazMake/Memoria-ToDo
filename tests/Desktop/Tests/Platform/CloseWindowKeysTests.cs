using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Close window keys, with plain test windows that hold a text box. Modifier keys are pressed as key events, as
/// <see cref="SimulatedKey.Chord"/> does, since a test can't change the keyboard's real modifier state. What Set and
/// Attach refuse is unit-tested in CloseWindowKeysSetTests. Not covered: Alt reported as Key.System with the real key
/// in SystemKey, which SimulatedKey can't raise.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class CloseWindowKeysTests
{
    [WpfFact]
    public void Before_Set_no_key_closes_the_window()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);

        SimulatedKey.Chord(shown.Box, Key.Escape);
        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.W);

        Assert.False(shown.Closed);
    }

    [WpfFact]
    public void The_keys_close_the_window_when_pressed_in_it()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Control, Key.W);

        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.W);

        Assert.True(shown.Closed);
    }

    [WpfFact]
    public void A_key_with_no_modifiers_closes_the_window()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.None, Key.F9);

        SimulatedKey.Chord(shown.Box, Key.F9);

        Assert.True(shown.Closed);
    }

    [WpfFact]
    public void Keys_with_several_modifiers_close_the_window()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Control | ModifierKeys.Alt, Key.W);

        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.RightAlt, Key.W);

        Assert.True(shown.Closed);
    }

    [WpfFact]
    public void The_keys_close_the_window_before_a_control_sees_them_and_go_no_further()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.None, Key.Escape);

        var handled = SimulatedKey.Raise(shown.Box, Key.Escape, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

        Assert.True(handled);
        Assert.True(shown.Closed);
        Assert.Empty(shown.KeysTheBoxSaw);
    }

    [WpfFact]
    public void Without_the_modifiers_the_key_does_not_close_the_window_and_reaches_the_control()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Shift, Key.Escape);

        SimulatedKey.Chord(shown.Box, Key.Escape);

        Assert.False(shown.Closed);
        Assert.Contains(Key.Escape, shown.KeysTheBoxSaw);
    }

    [WpfFact]
    public void Extra_modifiers_do_not_close_the_window_and_the_key_reaches_the_control()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Shift, Key.Escape);

        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.LeftShift, Key.Escape);

        Assert.False(shown.Closed);
        Assert.Contains(Key.Escape, shown.KeysTheBoxSaw);
    }

    [WpfFact]
    public void A_modifier_released_before_the_key_no_longer_counts()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.None, Key.Escape);

        SimulatedKey.Chord(shown.Box, Key.LeftCtrl);
        SimulatedKey.Chord(shown.Box, Key.Escape);

        Assert.True(shown.Closed);
    }

    [WpfFact]
    public void Set_after_Attach_replaces_the_keys_so_the_old_keys_stop_closing()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.None, Key.Escape);
        keys.Set(ModifierKeys.None, Key.F9);

        SimulatedKey.Chord(shown.Box, Key.Escape);
        var closedByOldKeys = shown.Closed;
        SimulatedKey.Chord(shown.Box, Key.F9);

        Assert.False(closedByOldKeys);
        Assert.True(shown.Closed);
    }

    [WpfFact]
    public void Set_changes_the_keys_of_every_attached_window()
    {
        using var first = new ShownWindow();
        using var second = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(first.Window);
        keys.Attach(second.Window);
        keys.Set(ModifierKeys.None, Key.Escape);
        keys.Set(ModifierKeys.None, Key.F9);

        SimulatedKey.Chord(first.Box, Key.F9);
        SimulatedKey.Chord(second.Box, Key.F9);

        Assert.True(first.Closed);
        Assert.True(second.Closed);
    }

    [WpfFact]
    public void Only_the_window_the_keys_were_pressed_in_closes()
    {
        using var first = new ShownWindow();
        using var second = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(first.Window);
        keys.Attach(second.Window);
        keys.Set(ModifierKeys.None, Key.Escape);

        SimulatedKey.Chord(second.Box, Key.Escape);

        Assert.False(first.Closed);
        Assert.True(second.Closed);
    }

    [WpfFact]
    public void The_keys_send_the_window_the_users_close_command_like_its_close_button()
    {
        using var shown = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(shown.Window);
        keys.Set(ModifierKeys.Control, Key.W);

        SimulatedKey.Chord(shown.Box, Key.LeftCtrl, Key.W);

        Assert.True(shown.GotUsersCloseCommand);
    }

    [WpfFact]
    public void A_window_not_attached_is_not_closed()
    {
        using var attached = new ShownWindow();
        using var other = new ShownWindow();
        var keys = new CloseWindowKeys();
        keys.Attach(attached.Window);
        keys.Set(ModifierKeys.None, Key.Escape);

        SimulatedKey.Chord(other.Box, Key.Escape);

        Assert.False(other.Closed);
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
