using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>A file system whose writes work <paramref name="allowedWrites"/> times and then fail with <see cref="RefusalMessage"/>. Everything else is the wrapped fake.</summary>
internal sealed class WriteLimitedFileSystem(FakeFileSystem inner, int allowedWrites) : IFileSystem
{
    public const string RefusalMessage = "This write is refused.";

    private int _writes;

    public bool IsSameOrDescendantDirectory(string directory, string ancestor) => inner.IsSameOrDescendantDirectory(directory, ancestor);

    public bool FileExists(string path) => inner.FileExists(path);

    public byte[] ReadFile(string path) => inner.ReadFile(path);

    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        if (++_writes > allowedWrites)
        {
            throw new IOException(RefusalMessage);
        }

        inner.WriteFile(path, content);
    }

    public IReadOnlyList<string> ListFiles(string folder) => inner.ListFiles(folder);

    public void MoveFile(string sourcePath, string targetPath) => inner.MoveFile(sourcePath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public void DeleteFolder(string path) => inner.DeleteFolder(path);
}
