using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.SearchBand;

using SearchBand = Memoria.ToDo.Desktop.Platform.SearchBand;

/// <summary>
/// The search band at the top of a window, in a minimal host: one window with the band docked at its top. Each test is one
/// path: how the band looks in a themed window, what Down and Up do in its box, and how an "@" list or another handler that
/// uses those keys first leaves the band out. Key events need a showing window to come from, so these are screen tests. A held
/// modifier is put in this thread's keyboard-state table by <see cref="HeldModifiers"/>, which never touches the real keyboard.
/// </summary>
[UX("SearchBand")]
[Collection(ScreenTests.Collection)]
public class SearchBandPathTests : IDisposable
{
    private const string CardKey = "CardBackgroundFillColorDefaultBrush";
    private const double MostExtraBandHeight = 12;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, Key[]> ListKeys = [];

    // A list attaches to a box as the "@" list does: through a class-level preview handler, which runs before the box's own handlers.
    static SearchBandPathTests() =>
        EventManager.RegisterClassHandler(typeof(TextBox), Keyboard.PreviewKeyDownEvent, new KeyEventHandler((sender, e) =>
        {
            if (sender is TextBox box && ListKeys.TryGetValue(box, out var keys))
            {
                e.Handled |= keys.Contains(EventKey.Of(e));
            }
        }));

    private readonly SearchBand _band = new();
    private readonly DockPanel _content = new() { LastChildFill = false };
    private readonly Window _window;
    private readonly List<bool> _requests = [];

    public SearchBandPathTests()
    {
        _band.EdgeRowRequested += (_, bottom) => _requests.Add(bottom);
        DockPanel.SetDock(_band, Dock.Top);
        _content.Children.Add(_band);
        _window = new Window { Width = 700, Height = 300, Content = _content };
        OpenWindows.UseTheme(_window, dark: false);
        _window.Show();
        _window.UpdateLayout();
    }

    public void Dispose() => _window.Close();

    private Color BandColor => ((SolidColorBrush)_band.Background).Color;

    private Color ThemeCardColor => ((SolidColorBrush)_window.FindResource(CardKey)).Color;

    private bool Press(Key key) => SimulatedKey.Raise(_band.Box, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    // Only the preview event is raised: the box handles the following KeyDown itself, which would make a key look taken
    // even when the band let it go on.
    private bool PressAndSeeIfTheBandTookIt(Key key) => SimulatedKey.RaiseOne(_band.Box, key, Keyboard.PreviewKeyDownEvent);

    // An open list attached to the box takes the keys it uses, as the "@" list does to move through its rows.
    private void AttachListOnTheBox(params Key[] keysItUses) => ListKeys.AddOrUpdate(_band.Box, keysItUses);

    private void AttachListOnTheWindow(params Key[] keysItUses) =>
        _window.PreviewKeyDown += (_, e) => e.Handled |= keysItUses.Contains(EventKey.Of(e));

    [WpfFact]
    public void The_band_runs_edge_to_edge_at_the_top_in_the_themes_color_follows_the_theme_and_keeps_its_box_180_wide()
    {
        var at = _band.TranslatePoint(new Point(0, 0), _content);
        Assert.InRange(at.X, -0.5, 0.5);
        Assert.InRange(at.Y, -0.5, 0.5);
        Assert.InRange(_band.ActualWidth, _content.ActualWidth - 0.5, _content.ActualWidth + 0.5);
        Assert.InRange(_band.ActualHeight - _band.Box.ActualHeight, 0, MostExtraBandHeight);
        Assert.InRange(_band.Box.ActualWidth, 179.5, 180.5);
        Assert.Equal(ThemeCardColor, BandColor);
        var light = BandColor;
        Assert.NotEqual(((SolidColorBrush)_window.Background).Color, light);

        _window.Width = 1000;
        _window.UpdateLayout();

        Assert.InRange(_band.ActualWidth, _content.ActualWidth - 0.5, _content.ActualWidth + 0.5);
        Assert.InRange(_band.Box.ActualWidth, 179.5, 180.5);

        OpenWindows.UseTheme(_window, dark: true);

        Assert.NotEqual(light, BandColor);
        Assert.Equal(ThemeCardColor, BandColor);
    }

    [WpfFact]
    public void Down_and_Up_in_the_box_ask_for_the_top_or_bottom_row_and_go_no_further_unless_a_modifier_is_held_or_the_key_is_another()
    {
        var downTaken = PressAndSeeIfTheBandTookIt(Key.Down);
        var upTaken = PressAndSeeIfTheBandTookIt(Key.Up);

        Assert.True(downTaken);
        Assert.True(upTaken);
        Assert.Equal([false, true], _requests);

        _requests.Clear();
        var downWithAlt = true;
        var upWithAlt = true;
        HeldModifiers.While(ModifierKeys.Alt, () => downWithAlt = Press(Key.Down));
        HeldModifiers.While(ModifierKeys.Alt, () => upWithAlt = Press(Key.Up));
        var another = Press(Key.A);

        Assert.Empty(_requests);
        Assert.False(downWithAlt);
        Assert.False(upWithAlt);
        Assert.False(another);
    }

    [WpfFact]
    public void A_list_or_handler_that_uses_Down_or_Up_first_leaves_the_band_out_and_a_closed_list_leaves_the_keys_to_it()
    {
        AttachListOnTheBox(Key.Down, Key.Up);
        Press(Key.Down);
        Press(Key.Up);
        Assert.Empty(_requests);

        AttachListOnTheBox(Key.Down);
        Press(Key.Down);
        Assert.Empty(_requests);
        Press(Key.Up);
        Assert.Equal([true], _requests);

        _requests.Clear();
        AttachListOnTheBox();
        var reached = false;
        var takenByBand = false;
        _window.KeyDown += (_, _) => reached = true;
        _band.Box.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => takenByBand = e.Handled), handledEventsToo: true);
        Press(Key.Down);
        Assert.Equal([false], _requests);
        Assert.True(takenByBand, "The band left the key unhandled.");
        Assert.False(reached);
        Press(Key.Up);
        Assert.Equal([false, true], _requests);

        _requests.Clear();
        AttachListOnTheWindow(Key.Down);
        Press(Key.Down);
        Assert.Empty(_requests);
        Press(Key.Up);
        Assert.Equal([true], _requests);
    }
}
