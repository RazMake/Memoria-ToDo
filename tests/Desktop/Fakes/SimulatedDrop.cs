using System.Reflection;
using System.Windows;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>Drops files on an element of a showing window as the mouse does. Shared test setup, not a fake of a port.</summary>
public static class SimulatedDrop
{
    /// <summary>
    /// Raises the tunneling drop event on the element, then the bubbling one unless a handler took the drop.
    /// </summary>
    /// <param name="element">The element that gets the drop; it must be in a showing window.</param>
    /// <param name="paths">The files dropped, as Explorer gives them.</param>
    /// <returns>Whether a handler took the drop, in either event.</returns>
    public static bool Files(UIElement element, params string[] paths)
    {
        return Data(element, new DataObject(DataFormats.FileDrop, paths), new Point(1, 1));
    }

    /// <summary>
    /// Raises the tunneling drop event on the element, then the bubbling one unless a handler took the drop, with any payload.
    /// </summary>
    /// <param name="element">The element that gets the drop; it must be in a showing window.</param>
    /// <param name="data">What is dragged.</param>
    /// <param name="at">Where the pointer is, in the element's own coordinates.</param>
    /// <returns>Whether a handler took the drop, in either event.</returns>
    public static bool Data(UIElement element, IDataObject data, Point at)
    {
        var first = Args(element, data, DragDrop.PreviewDropEvent, at);
        element.RaiseEvent(first);
        if (first.Handled)
        {
            return true;
        }

        var second = Args(element, data, DragDrop.DropEvent, at);
        element.RaiseEvent(second);
        return second.Handled;
    }

    /// <summary>
    /// Raises the tunneling drag-over event on the element, then the bubbling one unless a handler handled it, as the pointer
    /// moving over it during a drag does.
    /// </summary>
    /// <param name="element">The element the pointer is over; it must be in a showing window.</param>
    /// <param name="data">What is dragged.</param>
    /// <param name="at">Where the pointer is, in the element's own coordinates.</param>
    /// <returns>The effects the handlers allow: <see cref="DragDropEffects.None"/> where the drop is refused (the no-drop cursor).</returns>
    public static DragDropEffects Over(UIElement element, IDataObject data, Point at)
    {
        var first = Args(element, data, DragDrop.PreviewDragOverEvent, at);
        element.RaiseEvent(first);
        if (first.Handled)
        {
            return first.Effects;
        }

        var second = Args(element, data, DragDrop.DragOverEvent, at);
        element.RaiseEvent(second);
        return second.Effects;
    }

    /// <summary>
    /// Raises the tunneling drag-leave event on the element, then the bubbling one, as the pointer of a drag leaving it does.
    /// </summary>
    /// <param name="element">The element the pointer leaves; it must be in a showing window.</param>
    /// <param name="data">What is dragged.</param>
    public static void Leave(UIElement element, IDataObject data)
    {
        var first = Args(element, data, DragDrop.PreviewDragLeaveEvent, new Point(1, 1));
        element.RaiseEvent(first);
        if (!first.Handled)
        {
            element.RaiseEvent(Args(element, data, DragDrop.DragLeaveEvent, new Point(1, 1)));
        }
    }

    // WPF gives DragEventArgs no public constructor, so the drop is built the way the drag-and-drop system builds it.
    private static DragEventArgs Args(UIElement element, IDataObject data, RoutedEvent routed, Point at)
    {
        var constructor = typeof(DragEventArgs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 5);
        var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.None, DragDropEffects.Copy, element, at]);
        args.RoutedEvent = routed;
        return args;
    }
}
