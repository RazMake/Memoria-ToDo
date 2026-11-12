using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The physical folder a path names, with junctions, symbolic links, 8.3 names, and <c>subst</c> drives resolved by
/// Windows itself. A path that doesn't exist yet resolves through its nearest existing folder. Opens nothing for write
/// and creates nothing.
/// </summary>
internal static class WindowsDirectoryIdentity
{
    private const uint ReadAttributes = 0x80;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint InvalidFileAttributes = 0xFFFFFFFF;
    private const int FileNotFound = 2;
    private const int AccessDenied = 5;
    private const int PathNotFound = 3;

    /// <summary>Whether <paramref name="directory"/> is <paramref name="ancestor"/> or lies below it, physically.</summary>
    /// <exception cref="IOException">A path can't be resolved, or an existing part of it is a file.</exception>
    public static bool IsSameOrDescendant(string directory, string ancestor)
    {
        RequireFullyQualified(directory, nameof(directory));
        RequireFullyQualified(ancestor, nameof(ancestor));
        var below = Resolve(directory);
        var above = Resolve(ancestor);
        return below.Equals(above, StringComparison.OrdinalIgnoreCase)
            || below.StartsWith(Path.EndsInDirectorySeparator(above) ? above : above + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void RequireFullyQualified(string path, string name)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"Not a full path: {path}", name);
        }
    }

    // Without a trailing separator, except a root, which keeps its own: C:\ stays C:\.
    private static string Resolve(string path)
    {
        var missing = new Stack<string>();
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        while (true)
        {
            var resolved = ResolveExisting(current);
            if (resolved is not null)
            {
                return Path.TrimEndingDirectorySeparator(
                    missing.Aggregate(resolved, (parent, name) => Path.Join(parent, name)));
            }

            var parent = Path.GetDirectoryName(current)
                ?? throw new IOException($"No part of this path exists: {path}");
            missing.Push(Path.GetFileName(current));
            current = parent;
        }
    }

    // Null only when nothing is there. A link whose target is gone is not "nothing": it's a failure, not a free spot.
    private static string? ResolveExisting(string path)
    {
        var extended = Extended(path);
        using var handle = CreateFile(extended, ReadAttributes, ShareAll, 0, OpenExisting, BackupSemantics, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if ((error is FileNotFound or PathNotFound) && GetFileAttributes(extended) == InvalidFileAttributes)
            {
                return null;
            }

            throw Failure(path, error);
        }

        if (!File.GetAttributes(handle).HasFlag(FileAttributes.Directory))
        {
            throw new IOException($"A file, not a folder: {path}");
        }

        var buffer = new StringBuilder(512);
        while (true)
        {
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                throw Failure(path, Marshal.GetLastWin32Error());
            }

            if (length < buffer.Capacity)
            {
                return Normalize(buffer.ToString());
            }

            buffer.EnsureCapacity((int)length + 1);
        }
    }

    private static Exception Failure(string path, int error)
    {
        var cause = new Win32Exception(error);
        return error == AccessDenied
            ? new UnauthorizedAccessException($"Not allowed to resolve the folder {path}.", cause)
            : new IOException($"Can't resolve the folder {path}.", cause);
    }

    private const string ExtendedPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";

    // A full path in the extended-length spelling: one already in it stays, a UNC path gains the UNC form.
    private static string Extended(string fullPath) =>
        fullPath.StartsWith(ExtendedPrefix, StringComparison.Ordinal) ? fullPath
        : fullPath.StartsWith(@"\\", StringComparison.Ordinal) ? ExtendedUncPrefix + fullPath[2..]
        : ExtendedPrefix + fullPath;

    private static string Normalize(string finalPath) =>
        finalPath.StartsWith(ExtendedUncPrefix, StringComparison.Ordinal) ? @"\\" + finalPath[ExtendedUncPrefix.Length..]
        : finalPath.StartsWith(ExtendedPrefix, StringComparison.Ordinal) ? finalPath[ExtendedPrefix.Length..]
        : finalPath;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint length, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetFileAttributes(string fileName);
}
