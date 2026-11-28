using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Creates the task store an app uses.</summary>
public static class TaskStore
{
    /// <summary>
    /// Creates Tasks and adds it to <paramref name="repository"/>, which then loads it when it selects a folder.
    /// <paramref name="deviceFolder"/> is this device's folder for the draft, outside the clone.
    /// Tasks logs its operations and failures to <paramref name="logger"/>.
    /// Throws <see cref="InvalidOperationException"/> when the repository already selected a folder.
    /// </summary>
    public static ITaskStore Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder, IRepositoryAccess repository, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFolder);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        if (!Path.IsPathFullyQualified(deviceFolder))
        {
            throw new ArgumentException("The device folder must be a fully qualified path.", nameof(deviceFolder));
        }

        var store = new TaskService(files, images, time, deviceFolder, repository, logger);
        repository.Add(store);
        return store;
    }
}
