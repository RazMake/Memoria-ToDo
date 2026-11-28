using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The mouse as a drag sees it. The composition root passes <see cref="MousePointer"/>; tests pass a fake.</summary>
public interface IDragPointer : IPointerPosition
{
    /// <summary>Whether the left button is down now.</summary>
    bool IsLeftButtonDown { get; }

    /// <summary>Runs a move drag of <paramref name="data"/> from <paramref name="source"/>, and returns when the drag ends.</summary>
    void StartDrag(DependencyObject source, IDataObject data);
}
