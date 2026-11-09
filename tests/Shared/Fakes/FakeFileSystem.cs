using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// The shared fake file system: files live in memory and follow <see cref="IFileSystem"/>'s contract, which
/// <see cref="FileSystemContract"/> checks. Only files exist; a folder exists while a file is in it.
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly object gate = new();
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        lock (gate) { return files.ContainsKey(Normalize(path)); }
    }

    /// <inheritdoc />
    public byte[] ReadFile(string path)
    {
        lock (gate)
        {
            return files.TryGetValue(Normalize(path), out var content)
                ? (byte[])content.Clone()
                : throw new FileNotFoundException("The file doesn't exist.", path);
        }
    }

    /// <inheritdoc />
    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        var key = Normalize(path);
        lock (gate)
        {
            // As the real write does: the bytes go to <name>.tmp, which then replaces the target.
            files.Remove(key + ".tmp");
            files[key] = content.ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string folder)
    {
        var key = Normalize(folder);
        lock (gate)
        {
            return files.Keys.Where(file => Path.GetDirectoryName(file) == key).ToArray();
        }
    }

    /// <inheritdoc />
    public void MoveFile(string sourcePath, string targetPath)
    {
        var source = Normalize(sourcePath);
        var target = Normalize(targetPath);
        lock (gate)
        {
            if (!files.TryGetValue(source, out var content))
            {
                throw new FileNotFoundException("The file doesn't exist.", sourcePath);
            }

            if (files.ContainsKey(target))
            {
                throw new IOException($"The file '{targetPath}' already exists.");
            }

            files.Remove(source);
            files[target] = content;
        }
    }

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        lock (gate) { files.Remove(Normalize(path)); }
    }

    /// <inheritdoc />
    public void DeleteFolder(string path)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;
        lock (gate)
        {
            foreach (var file in files.Keys.Where(file => file.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                files.Remove(file);
            }
        }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
}
