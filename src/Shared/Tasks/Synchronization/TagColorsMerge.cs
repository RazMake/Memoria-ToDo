namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Merges two versions of the tag colors, each tag by its own time. Pure: no I/O.</summary>
internal static class TagColorsMerge
{
    /// <summary>Null means the file is absent. Throws <see cref="InvalidDataException"/> for any present file that can't be read.</summary>
    public static byte[]? Merge(byte[]? basis, byte[]? localBytes, byte[]? remoteBytes)
    {
        if (basis is not null)
            TagColorsFileFormat.Read(basis);
        var local = localBytes is null ? null : TagColorsFileFormat.Read(localBytes);
        var remote = remoteBytes is null ? null : TagColorsFileFormat.Read(remoteBytes);
        if (local is null || remote is null)
            return (local ?? remote) is { } only ? TagColorsFileFormat.Write(only) : null;

        return TagColorsFileFormat.Write(local.MergedWith(remote));
    }
}
