namespace Memoria.ToDo.Shared.Sync;

/// <summary>One device's serialized synchronization service. Getters return cached snapshots without I/O.</summary>
public interface ISyncService : IAsyncDisposable
{
    ISyncStatus Status { get; }
    ISyncSetup Setup { get; }

    /// <summary>Raised after a status change, outside Tasks' lock, on any thread.</summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Starts automatic triggers and requests the initial sync without waiting for I/O.
    /// Repeated calls do nothing. Throws ObjectDisposedException after stopping.
    /// </summary>
    void Start();

    /// <summary>
    /// Starts a pass or joins the active pass, also before Start. Operational failures are returned
    /// as a failed status. Cancellation stops only this caller's wait, not the service-owned pass.
    /// Throws InvalidOperationException before starting a pass when no repository or device key is available,
    /// or the selected repository contains the device-only data folder.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// The service has stopped or been disposed, including when a queued request reaches a stopped service.
    /// </exception>
    Task<ISyncStatus> SyncNowAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Permanently stops triggers, cancels owned work, and awaits the worker and transport cleanup.
    /// Repeated calls join the same shutdown. Cancellation stops only this caller's wait.
    /// DisposeAsync joins shutdown without caller cancellation; neither operation blocks a UI thread.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
