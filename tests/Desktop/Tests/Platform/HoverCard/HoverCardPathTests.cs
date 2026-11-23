using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.HoverCard;

using HoverCard = Memoria.ToDo.Desktop.Platform.HoverCard;

/// <summary>
/// The hover card of <see cref="HoverCard"/> on screen, driven through its public surface: the pointer rests on a part of an owner and
/// its card shows in a popup. Each test is one path: how a tall card behaves while it shows, and a wheel or press elsewhere closing it.
/// The pointer is a shared fake, driven by routed events without moving the Windows cursor.
/// </summary>
[UX("HoverCard")]
[Collection(ScreenTests.Collection)]
public class HoverCardPathTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [WpfFact]
    public async Task A_card_taller_than_the_work_area_shows_where_the_part_was_asked_cut_to_the_work_area_and_stays_open_for_the_wheel_a_press_and_the_pointer_on_it()
    {
        using var screen = new CardScreen(cardHeight: SystemParameters.WorkArea.Height * 3);
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        screen.Card.Loaded += (_, _) => shown.TrySetResult();

        screen.RestOnPart();

        await shown.Task.WaitAsync(Bound);
        Assert.Equal(new Point(30, 20), Assert.Single(screen.AskedAt));
        var popupRoot = (FrameworkElement)PresentationSource.FromVisual(screen.Card)!.RootVisual;
        Assert.True(popupRoot.ActualHeight <= SystemParameters.WorkArea.Height, $"The card is {popupRoot.ActualHeight} tall, over the work area.");
        var scroller = Ancestor<ScrollViewer>(screen.Card);
        Assert.NotNull(scroller);
        Assert.True(scroller.ScrollableHeight > 0, "The taller content doesn't scroll.");

        screen.Wheel(screen.Card);

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(300)), "The wheel over the card closed it.");
        Assert.True(scroller.VerticalOffset > 0, "The wheel over the card didn't scroll it.");

        screen.Press(screen.Card);

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(300)), "The press on the card closed it.");

        var asked = screen.Asked;
        screen.MoveOntoCard();

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(2 * SystemParameters.MouseHoverTime.TotalMilliseconds)),
            "The pointer on the card closed it. " + string.Join(" | ", screen.Log.Events.Select(entry => entry.RenderMessage())));
        Assert.Equal(asked, screen.Asked);
    }

    [WpfFact]
    public void The_wheel_elsewhere_in_the_window_closes_the_card()
    {
        using var screen = new CardScreen();
        screen.RestOnPart();
        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");

        screen.Wheel(screen.Owner);

        Assert.True(screen.WaitUntil(() => !screen.Card.IsLoaded), "The wheel elsewhere left the card open.");
    }

    [WpfFact]
    public void A_mouse_press_elsewhere_in_the_window_closes_the_card()
    {
        using var screen = new CardScreen();
        screen.RestOnPart();
        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");

        screen.Press(screen.Owner);

        Assert.True(screen.WaitUntil(() => !screen.Card.IsLoaded), "The press elsewhere left the card open.");
    }

    private static T? Ancestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var parent = System.Windows.Media.VisualTreeHelper.GetParent(element); parent is not null; parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
        {
            if (parent is T found)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class CardScreen : IDisposable
    {
        private readonly Window _window;

        public CardScreen(double cardHeight = 40)
        {
            Owner = new Border { Width = 300, Height = 100, Background = System.Windows.Media.Brushes.White };
            Card = new Border { Width = 120, Height = cardHeight, Background = System.Windows.Media.Brushes.LightYellow };
            _ = new HoverCard(Owner, point =>
            {
                AskedAt.Add(point);
                var box = new Rect(10, 10, 60, 20);
                return Task.FromResult<HoverPart?>(box.Contains(point) ? new HoverPart(box, Card) : null);
            }, Pointer, Log.CreateLogger());
            _window = new Window { Content = Owner, Width = 400, Height = 300, WindowStartupLocation = WindowStartupLocation.Manual, Left = 50, Top = 50 };
            _window.Show();
            _window.UpdateLayout();
        }

        public Border Owner { get; }

        public Border Card { get; }

        /// <summary>How many times the owner was asked for the part at a point.</summary>
        public int Asked => AskedAt.Count;

        public List<Point> AskedAt { get; } = [];

        public FakeDragPointer Pointer { get; } = new();

        public InMemoryLogSink Log { get; } = new();

        public void RestOnPart()
        {
            SimulatedPointer.RequireActiveWindow(Owner);
            SimulatedPointer.Move(Owner, Pointer, new Point(30, 20));
        }

        public void MoveOntoCard()
        {
            SimulatedPointer.Leave(Owner);
            SimulatedPointer.Enter(Ancestor<ScrollViewer>(Card)!);
        }

        public void Wheel(FrameworkElement element)
        {
            element.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = element });
            element.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent, Source = element });
        }

        public void Press(FrameworkElement element) =>
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = element });

        /// <summary>Lets the dispatcher work until the condition holds or the time passes; says whether it held.</summary>
        public bool WaitUntil(Func<bool> condition, TimeSpan? limit = null)
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(limit ?? Bound, DispatcherPriority.Normal, (_, _) => frame.Continue = false, _window.Dispatcher);
            var poll = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal, (_, _) => frame.Continue = !condition(), _window.Dispatcher);
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timeout.Stop();
                poll.Stop();
            }

            return condition();
        }

        public void Dispose() => _window.Close();
    }
}
