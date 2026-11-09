namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// The folder for files that stay on this device and never sync, outside the clone.
/// Modules read and write its files through <see cref="IFileSystem"/>.
/// </summary>
public sealed class DeviceStore
{
    /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="folder"/> is a full path. Touches no file.</summary>
    public DeviceStore(string folder) => throw new NotImplementedException();

    /// <summary>
    /// The full path of the file <paramref name="name"/> names, relative to the store's folder, such as "Tasks/draft.json".
    /// Throws <see cref="ArgumentException"/> when <paramref name="name"/> is empty, a full path, or leads out of the folder.
    /// </summary>
    public string PathOf(string name) => throw new NotImplementedException();
}
