using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>An immutable status; every change publishes a new one.</summary>
internal sealed record StatusSnapshot(
    SyncState State,
    SyncFailure? Failure,
    string? Details,
    DateTimeOffset? LastSuccessfulSync,
    bool HasPendingChanges,
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles) : ISyncStatus
{
    public static StatusSnapshot Initial { get; } = new(SyncState.Idle, null, null, null, false, []);

    public StatusSnapshot Working() => this with { State = SyncState.Working };

    public StatusSnapshot Succeeded(DateTimeOffset at, bool hasPendingChanges) =>
        new(SyncState.Idle, null, null, at, hasPendingChanges, []);

    public StatusSnapshot Failed(SyncFailure failure, string details, IReadOnlyList<ISkippedRepositoryFile> skipped) =>
        this with
        {
            State = failure == SyncFailure.Connection ? SyncState.Offline : SyncState.Error,
            Failure = failure,
            Details = details,
            SkippedFiles = skipped,
        };
}
