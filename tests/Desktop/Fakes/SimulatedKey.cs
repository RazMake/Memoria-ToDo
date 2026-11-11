using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>Sends a key to an element of a showing window as the keyboard does. Shared test setup, not a fake of a port.</summary>
public static class SimulatedKey
{
    /// <summary>
    /// Raises the tunneling event on the element, then the bubbling one unless a handler took the key.
    /// </summary>
    /// <param name="element">The element that gets the key; it must be in a showing window.</param>
    /// <param name="tunneling">The preview event, such as <see cref="Keyboard.PreviewKeyDownEvent"/>.</param>
    /// <param name="bubbling">The matching event, such as <see cref="Keyboard.KeyDownEvent"/>.</param>
    /// <returns>Whether a handler took the key, in either event.</returns>
    /// <exception cref="InvalidOperationException">The element isn't in a showing window.</exception>
    public static bool Raise(UIElement element, Key key, RoutedEvent tunneling, RoutedEvent bubbling)
    {
        var source = PresentationSource.FromVisual(element)
            ?? throw new InvalidOperationException("The window isn't showing, so it can't get a key.");
        var first = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = tunneling };
        element.RaiseEvent(first);
        if (first.Handled)
        {
            return true;
        }

        var second = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = bubbling };
        element.RaiseEvent(second);
        return second.Handled;
    }

    /// <summary>
    /// Presses the keys in order and releases them in reverse order, as a user holding a chord does. The releases go to the
    /// element that has the keyboard focus then: the pressed element when the key presses left it showing, otherwise
    /// wherever the focus went, as the keyboard does when a key press closes what had the focus. They go nowhere when
    /// nothing showing has the focus.
    /// </summary>
    public static void Chord(UIElement element, params Key[] keys)
    {
        foreach (var key in keys)
        {
            Raise(element, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
        }

        foreach (var key in keys.Reverse())
        {
            var target = PresentationSource.FromVisual(element) is not null ? element : Keyboard.FocusedElement as UIElement;
            if (target is not null && PresentationSource.FromVisual(target) is not null)
            {
                Raise(target, key, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent);
            }
        }
    }
}
