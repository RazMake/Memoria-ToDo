namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The rule that moves one pill to another place in the list of tags, which a drag and Alt+Left and Alt+Right share.</summary>
internal static class TagOrder
{
    /// <summary>
    /// A new list with the tag at <paramref name="from"/> moved to place <paramref name="to"/> and the others in their order.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a place outside the list; <paramref name="tags"/> is never changed.
    /// </summary>
    public static IReadOnlyList<string> Move(IReadOnlyList<string> tags, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, tags.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(to);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(to, tags.Count);

        var moved = new List<string>(tags);
        moved.RemoveAt(from);
        moved.Insert(to, tags[from]);
        return moved;
    }
}
