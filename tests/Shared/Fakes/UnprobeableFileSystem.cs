using System.IO;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// A file system like Windows when a file can't be probed: <see cref="FileExists"/> says false, as File.Exists does
/// after an I/O or access error, while reading the file is denied. Everything else is the wrapped fake.
/// </summary>
public sealed class UnprobeableFileSystem(FakeFileSystem inner, string unreadablePath) : IFileSystem
{
    public int Writes { get; private set; }

    public bool IsSameOrDescendantDirectory(string directory, string ancestor) => inner.IsSameOrDescendantDirectory(directory, ancestor);

    public bool FileExists(string path) => Path.GetFullPath(path) != Path.GetFullPath(unreadablePath) && inner.FileExists(path);

    public byte[] ReadFile(string path) => Path.GetFullPath(path) == Path.GetFullPath(unreadablePath)
        ? throw new UnauthorizedAccessException($"Access to {path} is denied.")
        : inner.ReadFile(path);

    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        Writes++;
        inner.WriteFile(path, content);
    }

    public IReadOnlyList<string> ListFiles(string folder) => inner.ListFiles(folder);

    public void MoveFile(string sourcePath, string targetPath) => inner.MoveFile(sourcePath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public void DeleteFolder(string path) => inner.DeleteFolder(path);
}
