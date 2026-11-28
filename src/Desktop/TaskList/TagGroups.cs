using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>One group of the left side: the tasks whose first tag is <see cref="Tag"/>, or the untagged tasks when it is null.</summary>
internal sealed record TaskGroup(string? Tag, IReadOnlyList<TaskItem> Tasks);

/// <summary>Sorts the open tasks into the groups the left side shows.</summary>
internal static class TagGroups
{
    /// <summary>
    /// A group for each first tag in the tag order's order, then the "No tag" group, with its tasks in the order given. A
    /// task is only in its first tag's group, so a tag that is never first has no group. Groups with no task are left out.
    /// </summary>
    public static IReadOnlyList<TaskGroup> Of(IReadOnlyList<TaskItem> open, ITagOrder tagOrder)
    {
        var tagged = new Dictionary<string, (string Tag, List<TaskItem> Tasks)>(StringComparer.Ordinal);
        var untagged = new List<TaskItem>();
        foreach (var task in open)
        {
            if (task.GroupTag is not { } tag)
            {
                untagged.Add(task);
                continue;
            }

            if (!tagged.TryGetValue(tag, out var group))
            {
                group = (tag, []);
                tagged[tag] = group;
            }

            group.Tasks.Add(task);
        }

        var groups = tagOrder.Sort([.. tagged.Values.Select(group => group.Tag)])
            .Select(tag => new TaskGroup(tag, tagged[tag].Tasks))
            .ToList();
        if (untagged.Count > 0)
        {
            groups.Add(new TaskGroup(null, untagged));
        }

        return groups;
    }
}
