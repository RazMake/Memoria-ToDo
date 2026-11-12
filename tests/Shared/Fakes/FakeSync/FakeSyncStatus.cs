using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

public sealed record FakeSyncStatus : ISyncStatus
{
    public SyncState State { get; init; }
    public SyncFailure? Failure { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? LastSuccessfulSync { get; init; }
    public bool HasPendingChanges { get; init; }
    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; init; } = [];
}
