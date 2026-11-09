namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// Files on this device, by full path. Each app implements it beside its composition root.
/// Each method fails with the system's exception, such as <see cref="IOException"/>, and never returns a fallback.
/// </summary>
public interface IFileSystem
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>The whole file. Throws <see cref="FileNotFoundException"/> when there's none.</summary>
    byte[] ReadFile(string path);

    /// <summary>Replaces the whole file, or creates it and its folders. A failure leaves the old file whole.</summary>
    void WriteFile(string path, ReadOnlySpan<byte> content);

    /// <summary>The full paths of the files directly in <paramref name="folder"/>, in no set order; none when the folder doesn't exist.</summary>
    IReadOnlyList<string> ListFiles(string folder);

    /// <summary>Moves a file, creating the target's folders. Throws <see cref="IOException"/> when the target exists.</summary>
    void MoveFile(string sourcePath, string targetPath);

    /// <summary>Deletes a file. Does nothing when there's none.</summary>
    void DeleteFile(string path);

    /// <summary>Deletes a folder and everything in it. Does nothing when there's none.</summary>
    void DeleteFolder(string path);
}
