using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Builds the cached setup view from the clone, the device files, and the key, on the owned worker. It remembers the
/// public key once seen, so refreshing never opens the key again.
/// </summary>
internal sealed class SetupModel(ITaskSynchronization tasks, IGitOperations git, IDeviceKeyStore keys, DeviceMetadata metadata, HostTrust trust, OwnedWorker worker)
{
    private byte[]? publicKey;
    // The name of the last read: empty when it failed, null before any read.
    private volatile string? lastNameRead;

    // The first view is built in memory; Start loads the rest on the worker.
    public Published<SetupSnapshot> Snapshot { get; } =
        new(new SetupSnapshot(tasks.RepositoryFolder, null, metadata.DefaultName, null, null, null, false));

    /// <summary>Remembers the public key of an open handle. Call on the worker.</summary>
    public void Observe(IDeviceSigningKey key) => publicKey ??= key.PublicKeyBlob.ToArray();

    /// <summary>Rebuilds the view and publishes it.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await worker.Run(Build).ConfigureAwait(false);
        Snapshot.Update(_ => snapshot);
    }

    /// <summary>
    /// Announces the setup after a failed refresh: the last name read, which is empty when the saved name was unreadable
    /// rather than the default, with the public key line that carries it.
    /// </summary>
    public void PublishFailed() =>
        Snapshot.Update(s => lastNameRead is not { } read ? s : s with
        {
            DeviceName = read,
            PublicKey = publicKey is not null && read.Length > 0 ? DeviceMetadata.PublicKeyLine(publicKey, read) : s.PublicKey,
        });

    private SetupSnapshot Build()
    {
        var folder = tasks.RepositoryFolder;
        lastNameRead = "";
        var name = metadata.LoadName();
        lastNameRead = name;

        var origin = folder is null ? null : git.ReadOrigin(folder);
        if (publicKey is null && keys.HasKey)
        {
            using var key = keys.Open();
            Observe(key);
        }

        var receipt = folder is null ? null : git.ReadReceipt(folder);
        var configured = receipt is not null && publicKey is not null && receipt.KeyFingerprint == DeviceMetadata.Fingerprint(publicKey);
        return new SetupSnapshot(
            folder,
            origin,
            name,
            publicKey is null ? null : DeviceMetadata.PublicKeyLine(publicKey, name),
            origin is null ? null : AddressParser.AddKeyPage(origin),
            trust.Offered,
            configured);
    }
}
