using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Keeps the device folder, which holds device metadata, outside every repository clone. Call it on the owned worker only.</summary>
internal sealed class DeviceFolderBoundary(IFileSystem files, string deviceFolder)
{
    /// <summary>
    /// Whether the clone folder is the device folder or an ancestor of it, by real location, so links can't hide it.
    /// An <see cref="IOException"/> means the real location couldn't be checked.
    /// </summary>
    public bool Contains(string cloneFolder) => files.IsSameOrDescendantDirectory(deviceFolder, cloneFolder);
}