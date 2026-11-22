using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>Raises pointer events using fake positions, without moving the PC's cursor.</summary>
public static class SimulatedPointer
{
    /// <summary>Waits for a showing control to load, sets the fake position, then raises preview and unhandled bubbling moves.</summary>
    public static void Move(UIElement element, FakeDragPointer pointer, Point position)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(pointer);
        if (element is FrameworkElement { IsLoaded: false } control && PresentationSource.FromVisual(control) is not null)
        {
            LoadSync.Loaded(control);
        }

        pointer.Position = position;
        var preview = Args(element, Mouse.PreviewMouseMoveEvent);
        element.RaiseEvent(preview);
        if (!preview.Handled)
        {
            element.RaiseEvent(Args(element, Mouse.MouseMoveEvent));
        }
    }

    public static void Enter(UIElement element) => Raise(element, Mouse.MouseEnterEvent);

    public static void Leave(UIElement element) => Raise(element, Mouse.MouseLeaveEvent);

    /// <summary>Raises one mouse event without changing the fake position or waiting for loading.</summary>
    public static void Raise(UIElement element, RoutedEvent routed)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(routed);
        element.RaiseEvent(Args(element, routed));
    }

    /// <summary>Rejects hover setup on an inactive window instead of waiting for a card that cannot appear.</summary>
    public static void RequireActiveWindow(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Window.GetWindow(element) is not { IsActive: true })
        {
            throw new InvalidOperationException("The element's window isn't active, so a hover card would ignore the pointer.");
        }
    }

    private static MouseEventArgs Args(UIElement element, RoutedEvent routed) =>
        new(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = routed, Source = element };
}
