using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>A task's new place, as the tasks it lands between: <see cref="After"/> and <see cref="Before"/> are null at an end.</summary>
internal sealed record Move(string Id, string? After, string? Before);

/// <summary>Where a task goes when it is nudged or dropped: always among the tasks of one group, as the tasks it lands between.</summary>
internal static class ListOrder
{
    /// <summary>
    /// Moves the task <paramref name="step"/> places among the tasks of <paramref name="group"/>, the group whose row has the
    /// focus, or returns null when it is not in the group or would pass an end.
    /// </summary>
    public static Move? Nudge(IReadOnlyList<TaskItem> group, string id, int step)
    {
        var at = IndexOf(group, id);
        var to = at + step;
        if (at < 0 || step == 0 || to < 0 || to >= group.Count)
        {
            return null;
        }

        return step > 0
            ? new Move(id, group[to].Id, to + 1 < group.Count ? group[to + 1].Id : null)
            : new Move(id, to > 0 ? group[to - 1].Id : null, group[to].Id);
    }

    /// <summary>
    /// Puts <paramref name="droppedId"/> before or after <paramref name="targetId"/> among <paramref name="targetRows"/>, the rows
    /// of <paramref name="target"/>, the group of the row it is dropped on. <paramref name="shown"/> is every task the list shows, in every group and among the
    /// resolved ones. Returns null when it is dropped on itself, when it is dragged from another group than the target (even if it
    /// is in both), or when it is shown but not in the target group. A task that is
    /// not shown still gets a move, so Tasks can say it is gone.
    /// </summary>
    public static Move? Drop(GroupKey source, GroupKey target, IReadOnlyList<TaskItem> targetRows, IReadOnlyList<TaskItem> shown, string targetId, string droppedId, bool lowerHalf)
    {
        var at = IndexOf(targetRows, targetId);
        if (source != target || droppedId == targetId || at < 0)
        {
            return null;
        }

        var droppedAt = IndexOf(targetRows, droppedId);
        if (droppedAt < 0 && IndexOf(shown, droppedId) >= 0)
        {
            return null;
        }

        // The neighbors are the target's, without the dropped task, which leaves its old place.
        var before = lowerHalf ? at + 1 : at;
        var after = lowerHalf ? at : at - 1;
        return new Move(droppedId, NeighborId(targetRows, after, droppedId, -1), NeighborId(targetRows, before, droppedId, 1));
    }

    /// <summary>
    /// Puts <paramref name="droppedId"/> in the gap before the task at <paramref name="gap"/> among <paramref name="targetRows"/>, or at
    /// the end when the gap is its count, as <see cref="Drop"/> does for the row it is dropped on.
    /// </summary>
    public static Move? DropIn(GroupKey source, GroupKey target, IReadOnlyList<TaskItem> targetRows, IReadOnlyList<TaskItem> shown, string droppedId, int gap) =>
        gap < targetRows.Count
            ? Drop(source, target, targetRows, shown, targetRows[gap].Id, droppedId, lowerHalf: false)
            : Drop(source, target, targetRows, shown, targetRows[^1].Id, droppedId, lowerHalf: true);

    private static string? NeighborId(IReadOnlyList<TaskItem> group, int from, string skip, int direction)
    {
        for (var i = from; i >= 0 && i < group.Count; i += direction)
        {
            if (group[i].Id != skip)
            {
                return group[i].Id;
            }
        }

        return null;
    }

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
}
