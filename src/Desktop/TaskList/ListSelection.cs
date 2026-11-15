using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The one task selected on the left side, which the right side shows. A task in two groups is selected in both.</summary>
internal sealed class ListSelection
{
    /// <summary>The selected task's id, or null when nothing is selected.</summary>
    public string? SelectedId { get; private set; }

    /// <summary>Selects the task, replacing any earlier selection.</summary>
    public void Select(string id) => SelectedId = id;

    /// <summary>True for the selected task, in every group that shows it.</summary>
    public bool IsSelected(string id) => SelectedId == id;

    /// <summary>
    /// Keeps the selection while the snapshot shows the task, and returns the task as the snapshot shows it. When the snapshot
    /// no longer shows it, clears the selection and returns null.
    /// </summary>
    public TaskItem? Update(ListSnapshot snapshot)
    {
        if (SelectedId is { } id)
        {
            foreach (var task in snapshot.Shown)
            {
                if (task.Id == id)
                {
                    return task;
                }
            }

            SelectedId = null;
        }

        return null;
    }
}
