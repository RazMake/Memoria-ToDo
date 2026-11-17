using System.Windows;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The mouse as a <see cref="GroupHeading"/> or a <see cref="TaskRow"/> uses it: the left button, the pointer's place, and starting a drag.</summary>
internal interface IDragPointer
{
    /// <summary>Whether the left button is down now.</summary>
    bool IsLeftButtonDown { get; }

    /// <summary>The pointer's place in <paramref name="element"/>'s own coordinates.</summary>
    Point PositionIn(UIElement element);

    /// <summary>Runs a move drag of <paramref name="data"/> from <paramref name="source"/>, and returns when the drag ends.</summary>
    void StartDrag(DependencyObject source, IDataObject data);
}
