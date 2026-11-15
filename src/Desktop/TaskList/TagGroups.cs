using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>One group of the left side: the tasks that carry <see cref="Tag"/>, or the untagged tasks when it is null.</summary>
internal sealed record TaskGroup(string? Tag, IReadOnlyList<TaskItem> Tasks);

/// <summary>Sorts the open tasks into the groups the left side shows.</summary>
internal static class TagGroups
{
    /// <summary>
    /// A group for each tag in alphabetical order ignoring case, then the "No tag" group, with its tasks in the order given. A
    /// task with two tags is in both groups. Groups with no task are left out. With <paramref name="onlyTag"/>, only that tag's
    /// group is made, plus the groups of the task <paramref name="keepId"/> names: the task being edited stays in the list.
    /// </summary>
    public static IReadOnlyList<TaskGroup> Of(IReadOnlyList<TaskItem> open, string? onlyTag = null, string? keepId = null)
    {
        var tagged = new Dictionary<string, (string Tag, List<TaskItem> Tasks)>(StringComparer.OrdinalIgnoreCase);
        var untagged = new List<TaskItem>();
        foreach (var task in open)
        {
            var kept = task.Id == keepId;
            if (task.Tags.Count == 0 && (onlyTag is null || kept))
            {
                untagged.Add(task);
            }

            foreach (var tag in task.Tags)
            {
                if (onlyTag is not null && !kept && !string.Equals(tag, onlyTag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!tagged.TryGetValue(tag, out var group))
                {
                    group = (tag, []);
                    tagged[tag] = group;
                }

                // A task that repeats a tag in other letter case is still one row in the group.
                if (group.Tasks.Count == 0 || group.Tasks[^1] != task)
                {
                    group.Tasks.Add(task);
                }
            }
        }

        var groups = tagged.Values
            .OrderBy(group => group.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TaskGroup(group.Tag, group.Tasks))
            .ToList();
        if (untagged.Count > 0)
        {
            groups.Add(new TaskGroup(null, untagged));
        }

        return groups;
    }
}
