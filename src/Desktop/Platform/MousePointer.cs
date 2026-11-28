using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The PC's mouse, as an <see cref="IDragPointer"/>. Not confirmed by a test: no test can move the real mouse.</summary>
public sealed class MousePointer : IDragPointer
{
    public bool IsLeftButtonDown => Mouse.LeftButton == MouseButtonState.Pressed;

    public Point PositionIn(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Mouse.GetPosition(element);
    }

    public void StartDrag(DependencyObject source, IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(source);
        DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
    }
}
