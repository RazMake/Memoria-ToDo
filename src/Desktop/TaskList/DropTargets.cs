namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The one rule that picks the gap a dragged task or group lands in, from where the list's items sit and where the pointer is.
/// The drop line and the drop both use it, so a drop lands where the line showed. A gap that would move nothing is null.
/// </summary>
internal static class DropTargets
{
    /// <summary>
    /// The gap a task lands in at <paramref name="y"/>: between the rows of the group it was dragged from (or of the resolved
    /// tasks, when <paramref name="fromResolved"/>), by the halves of the row under the pointer. Null elsewhere, and next to the task itself.
    /// </summary>
    /// <param name="fromTag">The tag of the group the drag started in, or null for "No tag"; ignored for a resolved task.</param>
    public static TaskGap? ForTask(ListLayout layout, string? fromTag, string taskId, bool fromResolved, double y)
    {
        if (fromResolved)
        {
            return y >= layout.ResolvedTop ? GapAmong(layout.ResolvedRows, taskId, y, null, resolved: true) : null;
        }

        foreach (var group in layout.Groups)
        {
            if (Holds(group.Top, group.Bottom, y))
            {
                return SameTag(group.Tag, fromTag) ? GapAmong(group.Rows, taskId, y, group.Tag, resolved: false) : null;
            }
        }

        return null;
    }

    /// <summary>
    /// The gap a group lands in at <paramref name="y"/>, as a position among the tag groups shown: 0 is before the first, and
    /// their count is after the last. A group and its shown rows are one item split in halves; anywhere over "No tag" is the
    /// gap just before it. Null for "No tag", which can't be dragged, over the resolved tasks, outside the items, and next to the group itself.
    /// </summary>
    public static int? ForGroup(ListLayout layout, string? draggedTag, double y)
    {
        if (draggedTag is null)
        {
            return null;
        }

        var tagged = 0;
        var own = -1;
        int? gap = null;
        foreach (var group in layout.Groups)
        {
            if (group.Tag is null)
            {
                gap ??= Holds(group.Top, group.Bottom, y) ? tagged : null;
                continue;
            }

            if (SameTag(group.Tag, draggedTag))
            {
                own = tagged;
            }

            if (Holds(group.Top, group.Bottom, y))
            {
                gap = y < (group.Top + group.Bottom) / 2 ? tagged : tagged + 1;
            }

            tagged++;
        }

        return gap is { } found && found != own && found != own + 1 ? found : null;
    }

    /// <summary>The y of the drop line for a task's gap: the top of the row it lands before, or the bottom of the last row.</summary>
    public static double LineY(ListLayout layout, TaskGap gap)
    {
        var rows = gap.Resolved ? layout.ResolvedRows : layout.Groups.First(group => SameTag(group.Tag, gap.Tag)).Rows;
        return gap.Index < rows.Count ? rows[gap.Index].Top : rows[^1].Bottom;
    }

    /// <summary>
    /// The y of the drop line for a group's gap, as <see cref="ForGroup"/> gives it: just above the heading of the group it lands
    /// before, which sits <paramref name="headingGap"/> below its item's top, and under the last group when "No tag" doesn't follow it.
    /// </summary>
    public static double LineY(ListLayout layout, int groupGap, double headingGap)
    {
        var tagged = layout.Groups.Where(group => group.Tag is not null).ToList();
        if (groupGap < tagged.Count)
        {
            return tagged[groupGap].Top + headingGap;
        }

        return layout.Groups.FirstOrDefault(group => group.Tag is null) is { } noTag ? noTag.Top + headingGap : tagged[^1].Bottom;
    }

    private static TaskGap? GapAmong(IReadOnlyList<RowSpan> rows, string taskId, double y, string? tag, bool resolved)
    {
        var own = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].TaskId == taskId)
            {
                own = i;
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (Holds(rows[i].Top, rows[i].Bottom, y))
            {
                var index = y < (rows[i].Top + rows[i].Bottom) / 2 ? i : i + 1;
                return index != own && index != own + 1 ? new TaskGap(tag, resolved, index) : null;
            }
        }

        return null;
    }

    private static bool Holds(double top, double bottom, double y) => y >= top && y < bottom;

    private static bool SameTag(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
