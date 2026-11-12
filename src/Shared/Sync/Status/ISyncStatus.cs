using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>An immutable snapshot. A completed pass with skipped files is Error, not a clean success.</summary>
public interface ISyncStatus
{
    SyncState State { get; }
    SyncFailure? Failure { get; }
    string? Details { get; }
    DateTimeOffset? LastSuccessfulSync { get; }

    /// <summary>Whether local repository changes or commits still need to reach the remote.</summary>
    bool HasPendingChanges { get; }

    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }
}

public enum SyncState
{
    Idle,
    Working,
    Offline,
    Error,
}

public enum SyncFailure
{
    Connection,
    Authentication,
    UnknownHostKey,
    ChangedHostKey,
    Repository,
    PushRejected,
    InvalidFile,
    LocalStorage,
    Unexpected,
}
