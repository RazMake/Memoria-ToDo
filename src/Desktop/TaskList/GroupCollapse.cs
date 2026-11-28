using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Which tag groups the user has collapsed. Every group starts expanded, and the state lives as long as this object, which is
/// as long as one Task View window. A tag is matched exactly; the "No tag" group is the null tag.
/// </summary>
internal sealed class GroupCollapse
{
    private readonly HashSet<string> _tags = new(StringComparer.Ordinal);
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
    /// Makes sure the task has a row showing: expands the one group it is in, its first tag's or "No tag", and no other.
    /// </summary>
    public void Reveal(TaskItem task) => Expand(task.GroupTag);

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
