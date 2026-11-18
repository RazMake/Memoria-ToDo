namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One kind of contact file, such as group files: which paths it owns and how two versions of one merge.</summary>
internal interface IContactFileKind
{
    /// <summary>Whether the path, split into its parts, is a file of this kind.</summary>
    bool Holds(IReadOnlyList<string> parts);

    /// <summary>The merged bytes; null when the merged file is deleted. Null sides are absent files.</summary>
    byte[]? Merge(IReadOnlyList<string> parts, byte[]? basis, byte[]? local, byte[]? remote);
}

/// <summary>The merge steps every kind shares: reading each side, and keeping an edited file against a deletion.</summary>
internal abstract class ContactFileKind<T> : IContactFileKind where T : class
{
    public abstract bool Holds(IReadOnlyList<string> parts);

    /// <summary>The file's content. Throws <see cref="InvalidDataException"/> when it can't be read or needs a newer app.</summary>
    protected abstract T Read(IReadOnlyList<string> parts, byte[] bytes);

    protected abstract byte[] Write(T file);

    /// <summary>The merge of both present sides; the basis is null when no common version exists.</summary>
    protected abstract T MergeSides(T? basis, T local, T remote);

    public byte[]? Merge(IReadOnlyList<string> parts, byte[]? basis, byte[]? local, byte[]? remote)
    {
        var (b, l, r) = (ReadSide(parts, basis), ReadSide(parts, local), ReadSide(parts, remote));
        if (l is null || r is null)
        {
            var present = l ?? r;
            if (present is null)
                return null;
            var written = Write(present);
            return b is not null && Write(b).AsSpan().SequenceEqual(written) ? null :             written;
                    }

                    return Write(MergeSides(b, l, r));
    }

                private T? ReadSide(IReadOnlyList<string> parts, byte[]? bytes) => bytes is null ? null : Read(parts, bytes);

    /// <summary>Whether the path is a file directly in the folder, which is a "/" path.</summary>
    protected static bool IsIn(IReadOnlyList<string> parts, string folder)
    {
        var folderParts = folder.Split('/');
        return parts.Count == folderParts.Length + 1 && folderParts.Zip(parts).All(pair => pair.First == pair.Second);
    }
}
