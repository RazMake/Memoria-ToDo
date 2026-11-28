using System.IO;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The Windows watcher: a <see cref="FileSystemWatcher"/> over every file in the folder and its subfolders, with the largest buffer it allows (64 KB).</summary>
internal sealed class WindowsFolderWatcher : IFolderWatcher
{
    private const int BufferSize = 64 * 1024;

    public IDisposable Watch(string folder, IFolderChanges changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(changes);

        var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = BufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        try
        {
            FileSystemEventHandler changed = (_, e) => changes.Changed(e.Name!);
            watcher.Created += changed;
            watcher.Changed += changed;
            watcher.Deleted += changed;
            watcher.Renamed += (_, e) => changes.Changed(e.Name!);
            watcher.Error += (_, e) =>
            {
                var error = e.GetException();
                if (error is InternalBufferOverflowException)
                    changes.Overflowed();
                else
                    changes.Failed(error);
            };
            watcher.EnableRaisingEvents = true;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }

        return watcher;
    }
}
