namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// Files on this device, by full path. Each app implements it beside its composition root.
/// Each method fails with the system's exception, such as <see cref="IOException"/>, and never returns a fallback.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Whether directory is ancestor itself or physically below it, resolving filesystem aliases.
    /// Both arguments are full paths. Only genuinely absent suffixes are resolved from their nearest existing
    /// directory ancestor; a dangling link or junction is not an absent suffix. Performs no writes and never
    /// falls back to a textual comparison when physical resolution fails.
    /// </summary>
    /// <exception cref="ArgumentException">An argument is not a valid full path.</exception>
    /// <exception cref="IOException">Physical resolution fails, including a file where a directory is required
    /// or a link or junction whose target is missing. Existing files are left unchanged.</exception>
    /// <exception cref="UnauthorizedAccessException">Physical resolution is denied.</exception>
    bool IsSameOrDescendantDirectory(string directory, string ancestor);

    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>The whole file. Throws <see cref="FileNotFoundException"/> when there's none.</summary>
    byte[] ReadFile(string path);

    /// <summary>Replaces the whole file, or creates it and its folders. A failure leaves the old file whole.</summary>
    void WriteFile(string path, ReadOnlySpan<byte> content);

    /// <summary>The full paths of the files directly in <paramref name="folder"/>, in no set order; none when the folder doesn't exist. Throws <see cref="IOException"/> when <paramref name="folder"/> is a file.</summary>
    IReadOnlyList<string> ListFiles(string folder);

    /// <summary>Moves a file, creating the target's folders. Throws <see cref="IOException"/> when the target exists.</summary>
    void MoveFile(string sourcePath, string targetPath);

    /// <summary>Deletes a file. Does nothing when there's none; throws <see cref="IOException"/> when <paramref name="path"/> is a folder.</summary>
    void DeleteFile(string path);

    /// <summary>Deletes a folder and everything in it. Does nothing when there's none; throws <see cref="IOException"/> when <paramref name="path"/> is a file.</summary>
    void DeleteFolder(string path);
}
