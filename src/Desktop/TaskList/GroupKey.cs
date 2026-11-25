namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Which group of the list a row is in: a tag's group (<see cref="Tag"/> null for "No tag") or the resolved tasks. The key stays
/// the same when the list shows the group again after a refresh. Tags are compared as they are, ordinal, as in <see cref="DropTargets"/>.
/// </summary>
internal sealed record GroupKey(string? Tag, bool Resolved);
