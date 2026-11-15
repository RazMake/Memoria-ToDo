using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Which tag groups the user has collapsed. Every group starts expanded, and the state lives as long as this object, which is
/// as long as one Task View window. A tag is matched ignoring case; the "No tag" group is the null tag.
/// </summary>
internal sealed class GroupCollapse
{
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private bool _noTag;

    /// <summary>Whether the group's rows are hidden.</summary>
    public bool IsCollapsed(string? tag) => tag is null ? _noTag : _tags.Contains(tag);

    /// <summary>Collapses the group when it is expanded, and expands it when it is collapsed.</summary>
    public void Toggle(string? tag)
    {
        if (IsCollapsed(tag))
        {
            Expand(tag);
        }
        else if (tag is null)
        {
            _noTag = true;
        }
        else
        {
            _tags.Add(tag);
        }
    }

    /// <summary>
    /// Makes sure the task has a row showing: when every group the task is in is collapsed, expands only the group of its first
    /// row, the first in the list's group order; when one of its groups is expanded already, changes nothing.
    /// </summary>
    public void Reveal(TaskItem task)
    {
        if (task.Tags.Count == 0)
        {
            Expand(null);
        }
        else if (task.Tags.All(IsCollapsed))
        {
            Expand(task.Tags.Order(StringComparer.OrdinalIgnoreCase).First());
        }
    }

    /// <summary>Shows the group's rows, when they are hidden.</summary>
    public void Expand(string? tag)
    {
        if (tag is null)
        {
            _noTag = false;
        }
        else
        {
            _tags.Remove(tag);
        }
    }
}
