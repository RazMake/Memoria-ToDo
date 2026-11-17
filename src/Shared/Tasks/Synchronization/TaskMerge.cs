namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Merges two versions of one task, field by field, from their common basis. Pure: no I/O.</summary>
internal static class TaskMerge
{
    /// <summary>Null means the task is absent. Throws <see cref="InvalidDataException"/> for any present file that can't be read.</summary>
    public static byte[]? Merge(string expectedTaskId, byte[]? basisBytes, byte[]? localBytes, byte[]? remoteBytes)
    {
        TaskId.ThrowIfInvalid(expectedTaskId, nameof(expectedTaskId));

        var basis = Parse(basisBytes);
        var local = Parse(localBytes);
        var remote = Parse(remoteBytes);

        foreach (var present in new[] { basis, local, remote })
            if (present is not null && present.Id != expectedTaskId)
                throw new InvalidDataException("The file is of a different task than its path.");

        if (local is null || remote is null)
            return Absent(basis, local ?? remote);

        var localWins = local.Updated > remote.Updated;
        T Field<T>(Func<TaskItem, T> field, IEqualityComparer<T>? comparer = null) =>
            Pick(basis is null ? (false, default!) : (true, field(basis)), field(local), field(remote), localWins, comparer ?? EqualityComparer<T>.Default);
        var merged = new TaskItem(
            local.Id,
            Field(task => task.Title),
            Field(task => task.Description),
            Field(task => task.Rank),
            Field(task => task.Tags, TagListComparer.Instance),
            local.Created < remote.Created ? local.Created : remote.Created,
            local.Updated > remote.Updated ? local.Updated : remote.Updated,
            Field(task => task.Resolved));
        return TaskFileFormat.Write(merged);
    }

    /// <summary>One side is missing: a deletion wins over a version nobody changed, an edit wins over a deletion.</summary>
    private static byte[]? Absent(TaskItem? basis, TaskItem? present)
    {
        if (present is null || basis is not null && TaskFileFormat.Write(basis).AsSpan().SequenceEqual(TaskFileFormat.Write(present)))
            return null;
        return TaskFileFormat.Write(present);
    }

    private static TaskItem? Parse(byte[]? bytes) => bytes is null ? null : TaskFileFormat.Read(bytes);

    /// <summary>Without a basis, both sides count as having changed the field, so the later edit wins.</summary>
    private static T Pick<T>((bool Known, T Value) basis, T local, T remote, bool localWins, IEqualityComparer<T> comparer)
    {
        var localChanged = !basis.Known || !comparer.Equals(local, basis.Value);
        var remoteChanged = !basis.Known || !comparer.Equals(remote, basis.Value);
        if (localChanged && remoteChanged)
            return localWins ? local : remote;
        return localChanged ? local : remote;
    }

    /// <summary>Two tag lists are the same when they hold the same tags in the same order, ignoring case.</summary>
    private sealed class TagListComparer : IEqualityComparer<IReadOnlyList<string>>
    {
        public static readonly TagListComparer Instance = new();

        public bool Equals(IReadOnlyList<string>? x, IReadOnlyList<string>? y) =>
            ReferenceEquals(x, y) || x is not null && y is not null && x.SequenceEqual(y, StringComparer.OrdinalIgnoreCase);

        public int GetHashCode(IReadOnlyList<string> tags) =>
            tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(tag)));
    }
}
