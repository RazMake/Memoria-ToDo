namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Decides which group files change when a group moves between two others.</summary>
internal static class GroupOrder
{
    private const int Gap = 1000;

    /// <summary>
    /// The groups, with their new positions, to write; none when the group is already there. The groups are in display order.
    /// Throws <see cref="ArgumentException"/> for an unknown group, or neighbors that aren't next to each other once the group is out.
    /// </summary>
    public static IReadOnlyList<GroupFile> Plan(IReadOnlyList<GroupFile> ordered, string name, string? previous, string? next)
    {
        var moved = Find(ordered, name) ?? throw new ArgumentException($"There is no group '{name}'.", nameof(name));
        var rest = ordered.Where(group => !ReferenceEquals(group, moved)).ToList();
        var low = previous is null ? -1 : IndexOf(rest, previous, nameof(previous));
        var high = next is null ? rest.Count : IndexOf(rest, next, nameof(next));
        if (high - low != 1)
            throw new ArgumentException("The groups aren't next to each other.");
        if (ordered.ToList().IndexOf(moved) == low + 1)
            return [];

        long position;
        if (low < 0)
            position = (long)rest[0].Position - Gap;
        else if (high == rest.Count)
            position = (long)rest[^1].Position + Gap;
        else if ((long)rest[high].Position - rest[low].Position >= 2)
            position = rest[low].Position + ((long)rest[high].Position - rest[low].Position) / 2;
        else
            return Renumber(rest, moved, low + 1);

        return position is >= int.MinValue and <= int.MaxValue
            ? [moved.WithPosition((int)position)]
            : Renumber(rest, moved, low + 1);
    }

    private static IReadOnlyList<GroupFile> Renumber(List<GroupFile> rest, GroupFile moved, int at)
    {
        rest.Insert(at, moved);
        return [.. rest.Select((group, index) => group.WithPosition((index + 1) * Gap)).Where((group, index) => group.Position != rest[index].Position)];
    }

    private static GroupFile? Find(IReadOnlyList<GroupFile> groups, string name) =>
        groups.FirstOrDefault(group => group.Name.Equals(name, StringComparison.Ordinal));

    private static int IndexOf(List<GroupFile> groups, string name, string parameter)
    {
        var index = groups.FindIndex(group => group.Name.Equals(name, StringComparison.Ordinal));
        return index >= 0 ? index : throw new ArgumentException($"There is no other group '{name}'.", parameter);
    }
}
