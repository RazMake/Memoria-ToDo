using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>A part of an element, its box in the element's coordinates, and the card to show for it.</summary>
public sealed record HoverPart(Rect Box, FrameworkElement Card);

/// <summary>
/// A card that shows by a part of an element while the pointer rests on that part, as a tooltip does. A thin adapter: when to ask,
/// show, and close is <see cref="HoverRest"/>'s; this watches the owner and its window and shows the card in a popup under the part,
/// where WPF's popup placement keeps it on the screen.
/// </summary>
public sealed class HoverCard
{
    // One card shows in the whole app.
    private static readonly HoverCardSlot Slot = new();

    private readonly FrameworkElement _owner;
    private readonly Func<Point, Task<HoverPart?>> _partAt;
    private readonly HoverRest _rest;
    private readonly ScrollViewer _scroller = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Focusable = false,
    };
    // Transparent around the card, so a card with rounded corners shows no square edges.
    private readonly Popup _popup = new() { StaysOpen = true, Placement = PlacementMode.Bottom, AllowsTransparency = true };
    private (int Token, HoverPart? Part) _answer;
    private Window? _window;

    /// <summary>
    /// Watches owner's pointer while owner is on screen. Once the pointer rests on one place for Windows' hover time,
    /// asks partAt for the part there, in owner's coordinates, and shows its card, if any, under the part.
    /// Use it on the UI thread.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public HoverCard(FrameworkElement owner, Func<Point, Task<HoverPart?>> partAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(partAt);
        _owner = owner;
        _partAt = partAt;
        _rest = new HoverRest(TimeProvider.System, SystemParameters.MouseHoverTime, Slot, new Output(this), windowActive: false);
        _popup.PlacementTarget = owner;
        _popup.Child = _scroller;
        _scroller.MouseEnter += (_, _) => _rest.CardEntered();
        _scroller.MouseLeave += (_, _) => _rest.CardLeft();
        _scroller.PreviewMouseDown += (_, _) => _rest.ButtonPressed(onCard: true);
        _scroller.PreviewMouseWheel += (_, _) => _rest.WheelTurned(onCard: true);
        owner.PreviewMouseMove += (_, e) => _rest.PointerMoved(e.GetPosition(owner));
        // The popup is its own window, so the pointer reaching the card leaves the owner; that leaves the card open.
        owner.MouseLeave += (_, _) =>
        {
            if (!(_popup.IsOpen && new Rect(_scroller.RenderSize).Contains(Mouse.GetPosition(_scroller))))
            {
                _rest.PointerLeft();
            }
        };
        owner.IsVisibleChanged += HiddenWhenInvisible;
        owner.Loaded += (_, _) => Watch(Window.GetWindow(owner));
        owner.Unloaded += (_, _) => Unwatch();
        if (owner.IsLoaded)
        {
            Watch(Window.GetWindow(owner));
        }
    }

    private void Watch(Window? window)
    {
        if (window is null || window == _window)
        {
            return;
        }

        Unwatch();
        _window = window;
        window.Activated += WindowActivated;
        window.Deactivated += WindowDeactivated;
        window.LocationChanged += WindowMoved;
        window.IsVisibleChanged += HiddenWhenInvisible;
        window.PreviewMouseDown += ButtonPressed;
        window.PreviewMouseWheel += WheelTurned;
        window.PreviewKeyDown += KeyPressed;
        if (window.IsActive)
        {
            _rest.WindowActivated();
        }
    }

    private void Unwatch()
    {
        if (_window is { } window)
        {
            window.Activated -= WindowActivated;
            window.Deactivated -= WindowDeactivated;
            window.LocationChanged -= WindowMoved;
            window.IsVisibleChanged -= HiddenWhenInvisible;
            window.PreviewMouseDown -= ButtonPressed;
            window.PreviewMouseWheel -= WheelTurned;
            window.PreviewKeyDown -= KeyPressed;
            _window = null;
        }

        _rest.Unloaded();
    }

    private void WindowActivated(object? sender, EventArgs e) => _rest.WindowActivated();

    private void WindowDeactivated(object? sender, EventArgs e) => _rest.WindowDeactivated();

    private void WindowMoved(object? sender, EventArgs e) => _rest.WindowMoved();

    private void HiddenWhenInvisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            _rest.WindowHidden();
        }
    }

    private void ButtonPressed(object sender, MouseButtonEventArgs e) => _rest.ButtonPressed(onCard: false);

    private void WheelTurned(object sender, MouseWheelEventArgs e) => _rest.WheelTurned(onCard: false);

    private void KeyPressed(object sender, KeyEventArgs e) =>
        _rest.KeyPressed(ModifierKey.Of(EventKey.Of(e)) != ModifierKeys.None);

    private sealed class Output(HoverCard card) : IHoverRestOutput
    {
        public void FindPart(Point at, int token) =>
            card._owner.Dispatcher.BeginInvoke(() => Answer(at, token));

        public void Post(Action action) => card._owner.Dispatcher.BeginInvoke(action);

        public void Show(int token)
        {
            if (card._answer is not (var answered, { } part) || answered != token)
            {
                return;
            }

            var (work, scale) = PointerScreen.Current();
            card._scroller.MaxHeight = work.Height / scale;
            card._popup.PlacementRectangle = part.Box;
            card._scroller.Content = part.Card;
            card._popup.IsOpen = true;
        }

        public void Close()
        {
            card._popup.IsOpen = false;
            card._scroller.Content = null;
        }

        // Async void so a failure of the caller's part finder reaches the app's UI-thread error handling, not a dropped task.
        private async void Answer(Point at, int token)
        {
            var part = await card._partAt(at);
            card._answer = (token, part);
            card._rest.PartAnswered(token, part?.Box);
        }
    }
}
