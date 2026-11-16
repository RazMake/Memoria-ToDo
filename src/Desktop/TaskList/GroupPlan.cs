using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>A row's place in the list: the group it shows in, and its task. A task in two groups has two places.</summary>
internal readonly record struct RowPosition(string? Tag, string TaskId);

/// <summary>One group as the left side shows it: its rows when expanded, none when collapsed, and how many tasks it holds.</summary>
internal sealed record PlannedGroup(string? Tag, int TaskCount, bool Collapsed, IReadOnlyList<TaskItem> Rows)
{
    /// <summary>The count a collapsed heading shows, such as "(3)", or empty while the group is expanded.</summary>
    public string CountText => Collapsed ? $"({TaskCount})" : "";
}

/// <summary>Which rows the groups show, given what the user has collapsed, and how the keyboard moves between them.</summary>
internal static class GroupPlan
{
    /// <summary>The groups in the order given, each with its rows, or with none when collapsed.</summary>
    public static IReadOnlyList<PlannedGroup> Of(IReadOnlyList<TaskGroup> groups, GroupCollapse collapse)
    {
        var plan = new List<PlannedGroup>(groups.Count);
        foreach (var group in groups)
        {
            var collapsed = collapse.IsCollapsed(group.Tag);
            plan.Add(new PlannedGroup(group.Tag, group.Tasks.Count, collapsed, collapsed ? [] : group.Tasks));
        }

        return plan;
    }

    /// <summary>
    /// The row above (<paramref name="step"/> -1) or below (1) the row, across groups and skipping collapsed ones, or null
    /// at an end, or when the row isn't one of the rows showing.
    /// </summary>
    public static RowPosition? Neighbor(IReadOnlyList<PlannedGroup> plan, RowPosition from, int step)
    {
        for (var g = 0; g < plan.Count; g++)
        {
            if (!string.Equals(plan[g].Tag, from.Tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rows = plan[g].Rows;
            var at = IndexOf(rows, from.TaskId);
            if (at < 0)
            {
                return null;
            }

            var to = at + step;
            return to >= 0 && to < rows.Count ? new RowPosition(plan[g].Tag, rows[to].Id) : Beyond(plan, g, step);
        }

        return null;
    }

    // The first row of the next group that shows rows, or the last row of the previous one.
    private static RowPosition? Beyond(IReadOnlyList<PlannedGroup> plan, int group, int step)
    {
        var direction = step > 0 ? 1 : -1;
        for (var g = group + direction; g >= 0 && g < plan.Count; g += direction)
        {
            var rows = plan[g].Rows;
            if (rows.Count > 0)
            {
                return new RowPosition(plan[g].Tag, rows[direction > 0 ? 0 : ^1].Id);
            }
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<TaskItem> rows, string id)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
