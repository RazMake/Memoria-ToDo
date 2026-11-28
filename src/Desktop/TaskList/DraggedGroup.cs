using System.Windows;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What a group heading's drag carries: the tag of the dragged group.</summary>
internal sealed record DraggedGroup(string Tag)
{
    /// <summary>The drag data for this payload.</summary>
    public IDataObject ToData() => new DataObject(this);

    /// <summary>Whether the drag data carries a group.</summary>
    public static bool IsIn(IDataObject data) => data.GetDataPresent(typeof(DraggedGroup));

    /// <summary>The payload in the drag data, or null when it carries none.</summary>
    public static DraggedGroup? From(IDataObject data) => data.GetData(typeof(DraggedGroup)) as DraggedGroup;
}
