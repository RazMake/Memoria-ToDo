namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Tasks' repository layout and deterministic merge rules. Paths are Git-relative paths using '/'.
/// Rooted paths and paths with '.' or '..' segments are rejected with ArgumentException.
/// </summary>
public interface IRepositoryFileRules
{
    /// <summary>The task directory's name, relative to the repository root.</summary>
    string TasksFolderName { get; }

    /// <summary>The image directory's name, relative to the repository root.</summary>
    string ImagesFolderName { get; }

    /// <summary>The shared settings file's name, relative to the repository root.</summary>
    string SettingsFileName { get; }

    /// <summary>The tags file's name, tags.json, relative to the repository root. It holds each tag's color and place.</summary>
    string TagColorsFileName { get; }

    /// <summary>The kind of file Tasks owns, or Unmanaged for another repository file.</summary>
    RepositoryFileKind Classify(string relativePath);

    /// <summary>The task ID in a task or image path, or null when the path names no valid task ID.</summary>
    string? TaskIdInPath(string relativePath);

    /// <summary>
    /// Merges the three task files without I/O. Null means the file is absent, including a deleted result.
    /// Uses field-by-field merge, edit-over-delete, and the remote value on equal Updated times.
    /// expectedTaskId is the canonical task ID alone, obtained from the repository path, not the whole path.
    /// Validates that argument before inspecting any file, even when all three files are absent.
    /// Throws InvalidDataException for any present file that cannot be read or whose ID differs
    /// from expectedTaskId, including the basis and either side of a deletion; never alters input arrays.
    /// </summary>
    /// <exception cref="ArgumentException">expectedTaskId is null or is not a canonical task ID.</exception>
    byte[]? MergeTask(string expectedTaskId, byte[]? basis, byte[]? local, byte[]? remote);

    /// <summary>
    /// Merges each setting by its own Updated time, remote winning ties. Null means an absent file;
    /// an absent side never removes a setting from a present side. Three absent files return null.
    /// Throws InvalidDataException for any present file that cannot be read; never alters input arrays.
    /// </summary>
    byte[]? MergeSettings(byte[]? basis, byte[]? local, byte[]? remote);

    /// <summary>
    /// Merges each tag's entry, matching tags with case ignored: its color and its place apart, each by its own
    /// Updated time, and its spelling from the side with the latest time; equal times take the remote's. Reads
    /// versions 1 and 2 and writes version 2. Null means an absent file; an absent side or a missing entry never
    /// removes an entry. Three absent files return null. Throws InvalidDataException for any present file that cannot
    /// be read, including a newer version; never alters input arrays.
    /// </summary>
    byte[]? MergeTagColors(byte[]? basis, byte[]? local, byte[]? remote);
}

/// <summary>A repository file's role, as Tasks defines it.</summary>
public enum RepositoryFileKind
{
    Unmanaged,
    Task,
    Image,
    Settings,
    TagColors,
}
