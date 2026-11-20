using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Down and Up in the search box pick the top or bottom row only when no handler before the band's, such as an open "@" list
/// attached to <see cref="SearchBand.Box"/> or to a container of the band, used the key. It is a screen test, as
/// <see cref="SearchBandKeysTests"/> is, because a key event needs a showing window to come from.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class SearchBandAttachedListKeysTests : IDisposable
{
    private readonly SearchBand _band = new();
    private readonly Window _window;
    private readonly List<bool> _requests = [];

    public SearchBandAttachedListKeysTests()
    {
        _band.EdgeRowRequested += (_, bottom) => _requests.Add(bottom);
        _window = new Window { Content = _band };
        _window.Show();
    }

    private void Press(Key key) => SimulatedKey.Raise(_band.Box, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, Key[]> ListKeys = [];

    // A list attaches to a box as the "@" list does: through a class-level preview handler, which runs before the box's own handlers.
    static SearchBandAttachedListKeysTests() =>
        EventManager.RegisterClassHandler(typeof(TextBox), Keyboard.PreviewKeyDownEvent, new KeyEventHandler((sender, e) =>
        {
            if (sender is TextBox box && ListKeys.TryGetValue(box, out var keys))
            {
                e.Handled |= keys.Contains(EventKey.Of(e));
            }
        }));

    // An open list attached to the box takes Down and Up, as the "@" list does to move through its rows.
    private void AttachListOnTheBox(params Key[] keysItUses) => ListKeys.AddOrUpdate(_band.Box, keysItUses);

    private void AttachListOnTheWindow(params Key[] keysItUses)
    {
        _window.PreviewKeyDown += (_, e) => e.Handled |= keysItUses.Contains(EventKey.Of(e));
    }

    [WpfFact]
    public void Down_a_list_on_the_box_used_asks_for_no_row()
    {
        AttachListOnTheBox(Key.Down);

        Press(Key.Down);

        Assert.Empty(_requests);
    }

    [WpfFact]
    public void Up_a_list_on_the_box_used_asks_for_no_row()
    {
        AttachListOnTheBox(Key.Up);

        Press(Key.Up);

        Assert.Empty(_requests);
    }

    [WpfFact]
    public void Down_a_list_on_the_window_used_asks_for_no_row()
    {
        AttachListOnTheWindow(Key.Down);

        Press(Key.Down);

        Assert.Empty(_requests);
    }

    [WpfFact]
    public void Down_a_closed_list_left_alone_asks_for_the_top_row()
    {
        AttachListOnTheBox();

        Press(Key.Down);

        Assert.Equal([false], _requests);
    }

    [WpfFact]
    public void Up_a_closed_list_left_alone_asks_for_the_bottom_row()
    {
        AttachListOnTheBox();

        Press(Key.Up);

        Assert.Equal([true], _requests);
    }

    [WpfFact]
    public void Up_asks_for_the_bottom_row_when_the_list_used_only_Down()
    {
        AttachListOnTheBox(Key.Down);

        Press(Key.Up);

        Assert.Equal([true], _requests);
    }

    [WpfFact]
    public void The_key_the_band_takes_goes_no_further()
    {
        var reached = false;
        var takenByBand = false;
        _window.KeyDown += (_, _) => reached = true;
        _band.Box.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => takenByBand = e.Handled), handledEventsToo: true);
        AttachListOnTheBox();

        Press(Key.Down);

        Assert.Equal([false], _requests);
        Assert.True(takenByBand, "The band left the key unhandled.");
        Assert.False(reached);
    }

    public void Dispose() => _window.Close();
}
