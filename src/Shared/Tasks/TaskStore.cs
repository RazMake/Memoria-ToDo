using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Creates the one <see cref="ITasks"/> an app uses.</summary>
public static class TaskStore
{
    /// <summary>Creates Tasks. <paramref name="deviceFolder"/> is this device's folder for the draft, outside the clone.</summary>
    public static ITasks Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFolder);
        return new TaskService(files, images, time, deviceFolder);
    }
}
