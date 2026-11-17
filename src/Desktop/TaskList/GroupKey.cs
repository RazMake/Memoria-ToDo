namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Which group of the list a row is in: a tag's group (<see cref="Tag"/> null for "No tag") or the resolved tasks. The key stays
/// the same when the list shows the group again after a refresh. Tags are the same ignoring case, as in <see cref="DropTargets"/>.
/// </summary>
internal sealed record GroupKey(string? Tag, bool Resolved)
{
    public bool Equals(GroupKey? other) =>
        other is not null && Resolved == other.Resolved && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(Tag is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Tag), Resolved);
}
