using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>A task's new place, as the tasks it lands between: <see cref="After"/> and <see cref="Before"/> are null at an end.</summary>
internal sealed record Move(string Id, string? After, string? Before);

/// <summary>Where a task goes when it is nudged or dropped: always among the tasks of its own group, open or resolved.</summary>
internal static class ListOrder
{
    /// <summary>Moves the task <paramref name="step"/> places, or null when it is not listed or would pass an end.</summary>
    public static Move? Nudge(IReadOnlyList<TaskItem> open, IReadOnlyList<TaskItem> resolved, string id, int step)
    {
        var group = GroupOf(open, id) ? open : resolved;
        var at = IndexOf(group, id);
        var without = group.Where(task => task.Id != id).ToList();
        var to = at + step;
        return at >= 0 && to >= 0 && to <= without.Count ? Place(id, without, to) : null;
    }

    /// <summary>
    /// Puts <paramref name="droppedId"/> before or after <paramref name="targetId"/>, or returns null when it is dropped on
    /// itself or comes from the other group. A task that is not listed still gets a move, so Tasks can say it is gone.
    /// </summary>
    public static Move? Drop(IReadOnlyList<TaskItem> open, IReadOnlyList<TaskItem> resolved, string targetId, string droppedId, bool lowerHalf)
    {
        var targetOpen = GroupOf(open, targetId);
        var group = targetOpen ? open : resolved;
        var listed = IndexOf(open, droppedId) >= 0 || IndexOf(resolved, droppedId) >= 0;
        if (targetId == droppedId || listed && GroupOf(open, droppedId) != targetOpen)
        {
            return null;
        }

        var without = group.Where(task => task.Id != droppedId).ToList();
        return Place(droppedId, without, without.FindIndex(task => task.Id == targetId) + (lowerHalf ? 1 : 0));
    }

    private static bool GroupOf(IReadOnlyList<TaskItem> open, string id) => IndexOf(open, id) >= 0;

    private static int IndexOf(IReadOnlyList<TaskItem> list, string id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static Move Place(string id, List<TaskItem> without, int index) =>
        new(id, index > 0 ? without[index - 1].Id : null, index < without.Count ? without[index].Id : null);
}
