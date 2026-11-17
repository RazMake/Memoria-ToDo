namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Device setup on the service's worker. Invalid arguments fail before side effects.
/// Operational failures update status and throw IOException with the cause retained.
/// Cancellation requests cancellation of this setup operation; the service still observes its completion.
/// Calls after stopping throw ObjectDisposedException.
/// </summary>
public interface ISyncSetup
{
    ISyncSetupState State { get; }

    /// <summary>
    /// Raised after the cached setup snapshot is published, on any thread and outside Tasks' lock.
    /// Also signals completion of initial local loading on success or failure, even when the snapshot is unchanged.
    /// On initialization failure, status reports the error before this publication.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Saves a nonblank, single-line device name without regenerating the device key.</summary>
    Task SetDeviceNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>Creates the device key once. An existing key is never replaced.</summary>
    Task GenerateKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates an existing clone, sets its origin to the supplied SSH address, and selects it.
    /// Never replaces the clone. Selection alone does not count as a successful first sync.
    /// Rejects a clone containing the supplied device-only folder before changing anything.
    /// </summary>
    Task UseExistingAsync(string address, string folder, CancellationToken cancellationToken);

    /// <summary>
    /// Clones through the device's SSH transport into a new or empty folder, then selects it.
    /// A clone failure keeps the previous selection. A first successful sync is still required.
    /// Rejects a destination containing the supplied device-only folder before creating anything.
    /// </summary>
    Task CloneAsync(string address, string folder, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts only the currently offered, unknown or changed custom-host key with this exact fingerprint.
    /// It replaces every key saved for that host and port. Built-in hosts cannot be accepted by this operation.
    /// </summary>
    Task AcceptHostKeyAsync(string fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the device key, if any. Host trust and the selected clone don't change.
    /// The device isn't configured again until a new key completes a first writable sync.
    /// </summary>
    Task ForgetKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether Clone and Use existing accept this address, checked on the caller's thread with no I/O.
    /// When they do, addKeyPage is the host's page for adding the device key, or null when Sync knows none.
    /// </summary>
    bool CheckAddress(string address, out Uri? addKeyPage);
}
