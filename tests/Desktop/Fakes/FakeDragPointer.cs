using System.Windows;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The mouse as a row or a heading sees it, set by the test, and the drags it was asked to start, as the data each carries. The
/// element asks it, not Windows, for the button, the pointer's place, and to start a drag. Shared test setup, because the
/// mouse of the PC can't be told where to be.
/// </summary>
public sealed class FakeDragPointer : IDragPointer
{
    public bool IsLeftButtonDown { get; set; }

    public Point Position { get; set; }

    public List<IDataObject> Drags { get; } = [];

    public Point PositionIn(UIElement element) => Position;

    public void StartDrag(DependencyObject source, IDataObject data) => Drags.Add(data);
}
