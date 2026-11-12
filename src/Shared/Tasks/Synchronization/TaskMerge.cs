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
        T Field<T>(Func<TaskItem, T> field) => Pick(basis is null ? (false, default!) : (true, field(basis)), field(local), field(remote), localWins);
        var merged = new TaskItem(
            local.Id,
            Field(task => task.Title),
            Field(task => task.Description),
            Field(task => task.Rank),
            MergeTags(basis?.Tags ?? [], local.Tags, remote.Tags),
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
    private static T Pick<T>((bool Known, T Value) basis, T local, T remote, bool localWins)
    {
        var localChanged = !basis.Known || !EqualityComparer<T>.Default.Equals(local, basis.Value);
        var remoteChanged = !basis.Known || !EqualityComparer<T>.Default.Equals(remote, basis.Value);
        if (localChanged && remoteChanged)
            return localWins ? local : remote;
        return localChanged ? local : remote;
    }

    /// <summary>The basis tags minus any either side removed, then each side's additions, ignoring case.</summary>
    private static IReadOnlyList<string> MergeTags(IReadOnlyList<string> basis, IReadOnlyList<string> local, IReadOnlyList<string> remote)
    {
        var tags = new List<string>();
        foreach (var tag in basis.Where(tag => TagCatalog.Has(local, tag) && TagCatalog.Has(remote, tag))
                     .Concat(local.Where(tag => !TagCatalog.Has(basis, tag)))
                     .Concat(remote.Where(tag => !TagCatalog.Has(basis, tag))))
            if (!TagCatalog.Has(tags, tag))
                tags.Add(tag);
        return tags;
    }
}
