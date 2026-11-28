namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Sync's rules for the files Contacts owns.</summary>
public interface IContactFileRules
{
    /// <summary>
    /// Whether the path, relative to the clone, is a group file, a data type file, or the templates file.
    /// Throws <see cref="ArgumentException"/> for a rooted path or one with "." or ".." parts.
    /// </summary>
    bool Holds(string relativePath);

    /// <summary>
    /// The merged bytes, or null when the merged file is deleted. Null means the side has no file.
    /// Throws <see cref="InvalidDataException"/> when a present side can't be read or needs a newer app;
    /// <see cref="ArgumentException"/> when not <see cref="Holds"/>. Never changes its inputs.
    /// </summary>
    byte[]? Merge(string relativePath, byte[]? basis, byte[]? local, byte[]? remote);
}
