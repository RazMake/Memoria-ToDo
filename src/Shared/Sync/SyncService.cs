using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Creates the sync service over the app's platform adapters.</summary>
public static class SyncService
{
    /// <summary>
    /// Creates the service with the native Git and SSH implementation. It checks its arguments only: it starts no
    /// thread or timer and touches no file until the first operation.
    /// </summary>
    /// <param name="deviceFolder">A full path outside every repository clone, for the device name and trusted hosts.</param>
    /// <param name="defaultDeviceName">The device name used until the user saves another.</param>
    /// <param name="periodicInterval">How often to sync when idle, or null for no periodic sync.</param>
    public static ISyncService Create(
        ITaskSynchronization tasks,
        IFileSystem files,
        IDeviceKeyStore keys,
        IRepositorySelection repositories,
        TimeProvider time,
        string deviceFolder,
        string defaultDeviceName,
        TimeSpan? periodicInterval)
    {
        var worker = new OwnedWorker();
        return Create(tasks, files, keys, repositories, time, deviceFolder, defaultDeviceName, periodicInterval, new NativeGitOperations(tasks, time, worker), worker);
    }

    internal static ISyncService Create(
        ITaskSynchronization tasks,
        IFileSystem files,
        IDeviceKeyStore keys,
        IRepositorySelection repositories,
        TimeProvider time,
        string deviceFolder,
        string defaultDeviceName,
        TimeSpan? periodicInterval,
        IGitOperations git) =>
        Create(tasks, files, keys, repositories, time, deviceFolder, defaultDeviceName, periodicInterval, git, new OwnedWorker());

    private static ISyncService Create(
        ITaskSynchronization tasks,
        IFileSystem files,
        IDeviceKeyStore keys,
        IRepositorySelection repositories,
        TimeProvider time,
        string deviceFolder,
        string defaultDeviceName,
        TimeSpan? periodicInterval,
        IGitOperations git,
        OwnedWorker worker)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(git);
        if (string.IsNullOrWhiteSpace(defaultDeviceName))
            throw new ArgumentException("The default device name is blank.", nameof(defaultDeviceName));
        if (string.IsNullOrWhiteSpace(deviceFolder) || !Path.IsPathFullyQualified(deviceFolder))
            throw new ArgumentException("The device folder must be a full path.", nameof(deviceFolder));
        if (periodicInterval is { } interval && interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(periodicInterval), "The interval must be positive.");

        var operations = new Operations(worker);
        var metadata = new DeviceMetadata(files, deviceFolder, defaultDeviceName.Trim());
        var trust = new HostTrust(HostPins.BuiltIn, new KnownHostsFile(files, metadata.KnownHostsPath), () => { });
        var model = new SetupModel(tasks, git, keys, metadata, trust, worker);
        var session = new GitSession(worker, keys, metadata, trust, model);
        var status = new Published<StatusSnapshot>(StatusSnapshot.Initial);
        var boundary = new DeviceFolderBoundary(files, deviceFolder);
        var setup = new SyncSetup(git, keys, repositories, boundary, worker, operations, session, model, metadata, trust, status);
        return new SyncEngine(tasks, git, keys, time, periodicInterval, worker, operations, session, model, status, setup, boundary, repositories);
    }
}
