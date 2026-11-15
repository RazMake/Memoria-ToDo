using System.Windows;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What a row drag carries: the dragged task and the group of the row the drag started from.</summary>
internal sealed record DraggedTask(TaskItem Task, IReadOnlyList<TaskItem> Source)
{
    /// <summary>The drag data for this payload.</summary>
    public IDataObject ToData() => new DataObject(this);

    /// <summary>Whether the drag data carries a task.</summary>
    public static bool IsIn(IDataObject data) => data.GetDataPresent(typeof(DraggedTask));

    /// <summary>The payload in the drag data, or null when it carries none.</summary>
    public static DraggedTask? From(IDataObject data) => data.GetData(typeof(DraggedTask)) as DraggedTask;
}
