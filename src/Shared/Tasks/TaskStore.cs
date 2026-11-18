using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Creates the task store an app uses.</summary>
public static class TaskStore
{
    /// <summary>
    /// Creates Tasks and adds it to <paramref name="repository"/>, which then loads it when it selects a folder.
    /// <paramref name="deviceFolder"/> is this device's folder for the draft, outside the clone.
    /// Throws <see cref="InvalidOperationException"/> when the repository already selected a folder.
    /// </summary>
    public static ITaskStore Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder, IRepositoryAccess repository)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFolder);
        ArgumentNullException.ThrowIfNull(repository);
        if (!Path.IsPathFullyQualified(deviceFolder))
        {
            throw new ArgumentException("The device folder must be a fully qualified path.", nameof(deviceFolder));
        }

        var store = new TaskService(files, images, time, deviceFolder, repository);
        repository.Add(store);
        return store;
    }
}
