using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The hover card of <see cref="HoverCard"/> on screen, driven through its public surface: the pointer rests on a part of an owner and
/// its card shows in a popup. These cover how the card behaves once it shows: its height, the wheel, a press, and the pointer on it.
/// The real pointer is put on the owner and the mouse move a pointer there raises is raised, as a pointer arriving does.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class HoverCardScreenTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [WpfFact]
    public void A_card_taller_than_the_work_area_of_its_screen_is_cut_to_it_and_scrolls_inside()
    {
        using var screen = new CardScreen(cardHeight: SystemParameters.WorkArea.Height * 3);

        screen.RestOnPart();

        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");
        var popupRoot = (FrameworkElement)PresentationSource.FromVisual(screen.Card)!.RootVisual;
        Assert.True(popupRoot.ActualHeight <= SystemParameters.WorkArea.Height, $"The card is {popupRoot.ActualHeight} tall, over the work area.");
        var scroller = Ancestor<ScrollViewer>(screen.Card);
        Assert.NotNull(scroller);
        Assert.True(scroller.ScrollableHeight > 0, "The taller content doesn't scroll.");
    }

    [WpfFact]
    public void The_wheel_over_the_card_scrolls_it_and_does_not_close_it()
    {
        using var screen = new CardScreen(cardHeight: SystemParameters.WorkArea.Height * 3);
        screen.RestOnPart();
        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");

        screen.Wheel(screen.Card);

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(300)), "The wheel over the card closed it.");
        Assert.True(Ancestor<ScrollViewer>(screen.Card)!.VerticalOffset > 0, "The wheel over the card didn't scroll it.");
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
    public void A_mouse_press_on_the_card_keeps_it_open()
    {
        using var screen = new CardScreen();
        screen.RestOnPart();
        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");

        screen.Press(screen.Card);

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(300)), "The press on the card closed it.");
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

    [WpfFact]
    public void The_pointer_on_the_card_asks_for_no_other_part_and_keeps_the_card_open()
    {
        using var screen = new CardScreen();
        screen.RestOnPart();
        Assert.True(screen.WaitUntil(() => screen.Card.IsLoaded), "The card didn't show.");
        var asked = screen.Asked;

        screen.MoveOver(screen.Card);

        Assert.False(screen.WaitUntil(() => !screen.Card.IsLoaded, TimeSpan.FromMilliseconds(2 * SystemParameters.MouseHoverTime.TotalMilliseconds)), "The pointer on the card closed it.");
        Assert.Equal(asked, screen.Asked);
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
            _ = new HoverCard(Owner, _ =>
            {
                Asked++;
                return Task.FromResult<HoverPart?>(new HoverPart(new Rect(10, 10, 60, 20), Card));
            });
            _window = new Window { Content = Owner, Width = 400, Height = 300, WindowStartupLocation = WindowStartupLocation.Manual, Left = 50, Top = 50 };
            _window.Show();
            _window.UpdateLayout();
        }

        public Border Owner { get; }

        public Border Card { get; }

        /// <summary>How many times the owner was asked for the part at a point.</summary>
        public int Asked { get; private set; }

        public void RestOnPart() => MoveOver(Owner, new Point(30, 20));

        public void MoveOver(FrameworkElement element, Point? at = null)
        {
            var inElement = at ?? new Point(element.ActualWidth / 2, element.ActualHeight / 2);
            var onScreen = element.PointToScreen(inElement);
            Win32.MoveCursor(new Win32.Point { X = (int)onScreen.X, Y = (int)onScreen.Y });
            element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.PreviewMouseMoveEvent, Source = element });
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
