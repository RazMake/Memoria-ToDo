using System.IO;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Files on this Windows device. A write goes to a temporary file first, so a failure leaves the old file whole.</summary>
internal sealed class WindowsFileSystem : IFileSystem
{
    public bool IsSameOrDescendantDirectory(string directory, string ancestor) =>
        WindowsDirectoryIdentity.IsSameOrDescendant(directory, ancestor);

    public bool FileExists(string path) => File.Exists(path);

    public byte[] ReadFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (DirectoryNotFoundException e)
        {
            throw new FileNotFoundException("No such file.", path, e);
        }
    }

    public void WriteFile(string path, ReadOnlySpan<byte> content)
    {
        CreateFolderOf(path);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    // Each operation below runs first and treats only a missing file or folder as nothing to do. File.Exists and
    // Directory.Exists answer false for a path they aren't allowed to see, so they only name the wrong kind after a failure.
    public IReadOnlyList<string> ListFiles(string folder)
    {
        try
        {
            return Directory.GetFiles(folder);
        }
        catch (DirectoryNotFoundException) when (!File.Exists(folder))
        {
            return [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException && File.Exists(folder))
        {
            throw new IOException($"A file, not a folder: {folder}", e);
        }
    }

    public void MoveFile(string sourcePath, string targetPath)
    {
        try
        {
            CreateFolderOf(targetPath);
            File.Move(sourcePath, targetPath);
        }
        catch (DirectoryNotFoundException e) when (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(sourcePath))))
        {
            throw new FileNotFoundException("No such file.", sourcePath, e);
        }
    }

    public void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // The file or its folder is missing: nothing to delete.
        }
        catch (UnauthorizedAccessException e) when (Directory.Exists(path))
        {
            throw new IOException($"A folder, not a file: {path}", e);
        }
    }

    public void DeleteFolder(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException) when (!File.Exists(path))
        {
            // The folder is missing: nothing to delete.
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DirectoryNotFoundException && File.Exists(path))
        {
            throw new IOException($"A file, not a folder: {path}", e);
        }
    }

    private static void CreateFolderOf(string path) => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
}
