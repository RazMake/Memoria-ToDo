using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Down and Up in the search box pick the top or bottom row. It is a screen test, not a unit test, because a key event needs a
/// showing window to come from. A held modifier is put in this thread's keyboard-state table by <see cref="HeldModifiers"/>, which
/// never touches the real keyboard.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class SearchBandKeysTests : IDisposable
{
    private readonly SearchBand _band = new();
    private readonly Window _window;
    private readonly List<bool> _requests = [];

    public SearchBandKeysTests()
    {
        _band.EdgeRowRequested += (_, bottom) => _requests.Add(bottom);
        _window = new Window { Content = _band };
        _window.Show();
    }

    private TextBox Box => OpenWindows.Descendants(_band).OfType<TextBox>().Single();

    private bool Press(Key key) => SimulatedKey.Raise(Box, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    // Only the preview event is raised: the box handles the following KeyDown itself, which would make a key look taken
    // even when the band let it go on.
    private bool PressAndSeeIfTheBandTookIt(Key key) => SimulatedKey.RaiseOne(Box, key, Keyboard.PreviewKeyDownEvent);

    [WpfFact]
    public void Down_in_the_box_asks_for_the_top_row_and_the_key_goes_no_further()
    {
        var handled = PressAndSeeIfTheBandTookIt(Key.Down);

        Assert.Equal([false], _requests);
        Assert.True(handled);
    }

    [WpfFact]
    public void Up_in_the_box_asks_for_the_bottom_row_and_the_key_goes_no_further()
    {
        var handled = PressAndSeeIfTheBandTookIt(Key.Up);

        Assert.Equal([true], _requests);
        Assert.True(handled);
    }

    [WpfFact]
    public void Down_with_a_modifier_held_asks_for_nothing_and_goes_on()
    {
        var handled = false;
        HeldModifiers.While(ModifierKeys.Alt, () => handled = Press(Key.Down));

        Assert.Empty(_requests);
        Assert.False(handled);
    }

    [WpfFact]
    public void Up_with_a_modifier_held_asks_for_nothing_and_goes_on()
    {
        var handled = false;
        HeldModifiers.While(ModifierKeys.Alt, () => handled = Press(Key.Up));

        Assert.Empty(_requests);
        Assert.False(handled);
    }

    [WpfFact]
    public void Another_key_asks_for_nothing_and_goes_on()
    {
        var handled = Press(Key.A);

        Assert.Empty(_requests);
        Assert.False(handled);
    }

    public void Dispose() => _window.Close();
}
