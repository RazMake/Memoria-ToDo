using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>EscCloses on a plain test window with one control inside, as Task View and Configure use it.</summary>
[UX("WindowHosting")]
[Collection(ScreenTests.Collection)]
public class EscClosesPathTests : IDisposable
{
    private readonly OpenWindows _windows = new();
    private readonly Window _window = new() { Width = 300, Height = 200 };
    private readonly TextBox _box = new();
    private bool _controlHandlesEsc;
    private ModifierKeys _physicalModifiers = ModifierKeys.None;

    public EscClosesPathTests()
    {
        _window.Content = _box;
        _box.KeyDown += (_, e) => e.Handled = _controlHandlesEsc && e.Key == Key.Escape;
        EscCloses.Attach(_window, () => _physicalModifiers);
        _window.Show();
        Keyboard.Focus(_box);
    }

    public void Dispose() => _windows.Dispose();

    private bool Closed => !_window.IsVisible;

    [WpfFact]
    public void Esc_closes_the_window_only_when_no_other_key_modifier_or_control_took_it()
    {
        SimulatedKey.Chord(_box, Key.Enter);
        Assert.False(Closed);

        foreach (var modifier in new[] { Key.LeftCtrl, Key.LeftShift, Key.LeftAlt })
        {
            SimulatedKey.Chord(_box, modifier, Key.Escape);
            Assert.False(Closed);
        }

        _physicalModifiers = ModifierKeys.Control | ModifierKeys.Alt;
        SimulatedKey.Chord(_box, Key.Escape);
        Assert.False(Closed);

        _physicalModifiers = ModifierKeys.None;
        _controlHandlesEsc = true;
        SimulatedKey.Chord(_box, Key.Escape);
        Assert.False(Closed);

        _controlHandlesEsc = false;
        SimulatedKey.Chord(_box, Key.Escape);
        Assert.True(Closed);
    }
}
