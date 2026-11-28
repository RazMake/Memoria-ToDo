using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The pointer's position. The composition root passes <see cref="MousePointer"/>; tests pass a fake.</summary>
public interface IPointerPosition
{
    /// <summary>The pointer's place in <paramref name="element"/>'s own coordinates.</summary>
    Point PositionIn(UIElement element);
}
