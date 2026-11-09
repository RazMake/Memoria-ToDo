using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>The shared fake file system: keeps files in memory and behaves like the real one, as <see cref="FileSystemContract"/> checks.</summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly List<string> failingFolders = [];
    private readonly List<string> failingReadFolders = [];
    private readonly List<string> failingDeleteFolders = [];

    /// <summary>From now on, writes and moves into <paramref name="folder"/> or below it fail with an <see cref="IOException"/>, and leave what's there whole.</summary>
    public void FailWritesUnder(string folder) => failingFolders.Add(System.IO.Path.GetFullPath(folder));

    /// <summary>From now on, reading a file in <paramref name="folder"/> or below it fails with an <see cref="IOException"/>, though the file still exists.</summary>
    public void FailReadsUnder(string folder) => failingReadFolders.Add(System.IO.Path.GetFullPath(folder));

    /// <summary>From now on, deleting a file or folder in <paramref name="folder"/> or below it (or the folder itself) fails with an <see cref="IOException"/>, and leaves what's there whole.</summary>
    public void FailDeletesUnder(string folder) => failingDeleteFolders.Add(System.IO.Path.GetFullPath(folder));

    /// <summary>Ends every failure set up with <see cref="FailWritesUnder"/>, <see cref="FailReadsUnder"/> and <see cref="FailDeletesUnder"/>, as when the cause of a failure goes away.</summary>
    public void StopFailing()
    {
        failingFolders.Clear();
        failingReadFolders.Clear();
        failingDeleteFolders.Clear();
    }

    /// <inheritdoc />
    public bool FileExists(string path) => files.ContainsKey(System.IO.Path.GetFullPath(path));

    /// <inheritdoc />
    public byte[] ReadFile(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (files.TryGetValue(full, out var content) && failingReadFolders.Any(folder => IsUnder(full, folder)))
            throw new IOException($"Reading is not allowed here: {full}");
        return content?.ToArray() ?? throw new FileNotFoundException("No such file.", path);
    }

    /// <inheritdoc />
    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        var full = System.IO.Path.GetFullPath(path);
        CheckCanWrite(full);
        files[full] = content.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string folder)
    {
        var full = System.IO.Path.GetFullPath(folder);
        if (files.ContainsKey(full))
            throw new IOException($"A file, not a folder: {full}");
        return files.Keys.Where(file => System.IO.Path.GetDirectoryName(file) == full).ToList();
    }

    /// <inheritdoc />
    public void MoveFile(string sourcePath, string targetPath)
    {
        var source = System.IO.Path.GetFullPath(sourcePath);
        var target = System.IO.Path.GetFullPath(targetPath);
        if (!files.TryGetValue(source, out var content))
            throw new FileNotFoundException("No such file.", sourcePath);
        CheckCanWrite(target);
        if (files.ContainsKey(target))
            throw new IOException($"The file already exists: {targetPath}");
        files.Remove(source);
        files[target] = content;
    }

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        CheckCanDelete(full);
        if (files.Keys.Any(file => IsUnder(file, full)))
            throw new IOException($"A folder, not a file: {full}");
        files.Remove(full);
    }

    /// <inheritdoc />
    public void DeleteFolder(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        CheckCanDelete(full);
        if (files.ContainsKey(full))
            throw new IOException($"A file, not a folder: {full}");
        foreach (var file in files.Keys.Where(file => IsUnder(file, full)).ToList())
            files.Remove(file);
    }

    private void CheckCanDelete(string fullPath)
    {
        if (failingDeleteFolders.Any(folder => fullPath == folder || IsUnder(fullPath, folder)))
            throw new IOException($"Deleting is not allowed here: {fullPath}");
    }

    private static bool IsUnder(string path, string folder) =>
        path.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>Fails, before changing anything, when the file can't be written: a failing folder, or a file where a folder is needed.</summary>
    private void CheckCanWrite(string fullPath)
    {
        if (failingFolders.Any(folder => IsUnder(fullPath, folder)))
            throw new IOException($"Writing is not allowed here: {fullPath}");
        for (var parent = System.IO.Path.GetDirectoryName(fullPath); parent is not null; parent = System.IO.Path.GetDirectoryName(parent))
            if (files.ContainsKey(parent))
                throw new IOException($"A file is in the way: {parent}");
    }
}
