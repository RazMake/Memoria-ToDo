using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The PC's mouse, as an <see cref="IDragPointer"/>.</summary>
internal sealed class MousePointer : IDragPointer
{
    public static readonly MousePointer Instance = new();

    private MousePointer()
    {
    }

    public bool IsLeftButtonDown => Mouse.LeftButton == MouseButtonState.Pressed;

    public Point PositionIn(UIElement element) => Mouse.GetPosition(element);

    public void StartDrag(DependencyObject source, IDataObject data) => DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
}
