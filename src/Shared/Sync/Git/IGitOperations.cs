namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// The Git and SSH work Sync coordinates. The public factory supplies the LibGit2Sharp implementation, which runs
/// its synchronous native calls on the service's owned worker; the test assembly supplies a fake through an internal
/// factory overload.
/// A failure that Sync must classify is a <see cref="GitFailureException"/>; any other exception is Unexpected.
/// <see cref="Clone"/> and <see cref="RunPass"/> return a task that completes only after the operation and all of its
/// transport cleanup have ended, so Sync awaits it before it disposes the signing handle; the token asks for a prompt end.
/// </summary>
internal interface IGitOperations
{
    /// <summary>The configured receipt in the clone's local Git configuration, or null when there is none.</summary>
    GitReceipt? ReadReceipt(string folder);

    void WriteReceipt(string folder, GitReceipt receipt);

    /// <summary>The address text of the clone's origin, or null when the folder has none.</summary>
    string? ReadOrigin(string folder);

    /// <summary>Validates an existing clone, then sets its origin to the address, clearing the receipt when the origin changes.</summary>
    void UseExisting(string folder, SshAddress address);

    /// <summary>Clones into a new or empty folder, asking the connection to verify each host key.</summary>
    Task Clone(GitConnection connection, SshAddress address, string folder, CancellationToken cancellationToken);

    /// <summary>One pass: delete, commit, fetch, merge, push. Refuses unsafe branch states before changing any file.</summary>
    Task<GitPassResult> RunPass(GitConnection connection, string folder, CancellationToken cancellationToken);
}

internal sealed record SshAddress(string Text, string Host, int Port, string Path);

internal sealed record GitReceipt(DateTimeOffset LastSuccessfulSync, string KeyFingerprint);

/// <summary>
/// What one operation may use: the open signing handle, the author name, Sync's host-key decision, and the collector
/// where transport callbacks add the tasks of work that outlives the operation. Sync awaits the collector before it
/// disposes the signing handle or starts the next operation.
/// </summary>
internal sealed record GitConnection(IDeviceSigningKey Key, string DeviceName, HostKeyVerifier VerifyHostKey, ITransportCleanup Cleanup);

/// <summary>Collects the tasks of transport work that outlives an operation, such as a closing SSH channel.</summary>
internal interface ITransportCleanup
{
    /// <summary>Adds a task to await. Never waits.</summary>
    void Add(Task task);

    /// <summary>Completes when every added task has completed, including tasks added while waiting.</summary>
    Task WhenAllAsync();
}

/// <summary>
/// What a pass did. <see cref="Skipped"/> lists files the merge kept the local copy of because it couldn't merge them,
/// which Sync reports with the files Tasks couldn't read.
/// </summary>
internal sealed record GitPassResult(bool HasPendingChanges)
{
    public IReadOnlyList<Tasks.ISkippedRepositoryFile> Skipped { get; init; } = [];
}

internal delegate HostKeyVerdict HostKeyVerifier(string host, int port, string algorithm, string fingerprint);

internal enum HostKeyVerdict
{
    Trusted,
    Unknown,
    Changed,
}

internal sealed class GitFailureException(SyncFailure kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public SyncFailure Kind { get; } = kind;
}
