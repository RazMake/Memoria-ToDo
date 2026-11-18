using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The in-memory file system, noting every call and every path that was changed, so a test can say what a store wrote and what it never touched.</summary>
internal sealed class RecordingFileSystem(FakeFileSystem inner) : IFileSystem
{
    private readonly List<string> changed = [];
    private int calls;
    private int writes;
    private int allowedWrites = int.MaxValue;
    private Exception? writeFailure;

    /// <summary>From now on, the writes after the next <paramref name="allowed"/> ones fail with <paramref name="failure"/>, and leave what's there whole.</summary>
    public void FailWritesAfter(int allowed, Exception failure)
    {
        writes = 0;
        allowedWrites = allowed;
        writeFailure = failure;
    }

    /// <summary>How many calls any method has had since the last <see cref="Forget"/>.</summary>
    public int Calls => Volatile.Read(ref calls);

    /// <summary>The full path of every file written, moved to, or deleted since the last <see cref="Forget"/>, in order.</summary>
    public IReadOnlyList<string> Changed
    {
        get { lock (changed) { return [.. changed]; } }
    }

    public void Forget()
    {
        Volatile.Write(ref calls, 0);
        lock (changed)
            changed.Clear();
    }

    private void Count() => Interlocked.Increment(ref calls);

    private void Change(string path)
    {
        lock (changed)
            changed.Add(System.IO.Path.GetFullPath(path));
    }

    public bool IsSameOrDescendantDirectory(string directory, string ancestor)
    {
        Count();
        return inner.IsSameOrDescendantDirectory(directory, ancestor);
    }

    public bool FileExists(string path)
    {
        Count();
        return inner.FileExists(path);
    }

    public byte[] ReadFile(string path)
    {
        Count();
        return inner.ReadFile(path);
    }

    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        Count();
        if (++writes > allowedWrites)
            throw writeFailure!;
        inner.WriteFile(path, content);
        Change(path);
    }

    public IReadOnlyList<string> ListFiles(string folder)
    {
        Count();
        return inner.ListFiles(folder);
    }

    public void MoveFile(string sourcePath, string targetPath)
    {
        Count();
        inner.MoveFile(sourcePath, targetPath);
        Change(sourcePath);
        Change(targetPath);
    }

    public void DeleteFile(string path)
    {
        Count();
        inner.DeleteFile(path);
        Change(path);
    }

    public void DeleteFolder(string path)
    {
        Count();
        inner.DeleteFolder(path);
        Change(path);
    }
}
