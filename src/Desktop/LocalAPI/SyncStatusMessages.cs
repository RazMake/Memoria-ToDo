using Google.Protobuf.WellKnownTypes;
using Memoria.ToDo.Shared.Sync;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>Turns Sync's status snapshot into the message that carries it. An unknown state or failure is a bug, so it throws.</summary>
internal static class SyncStatusMessages
{
    public static V1.SyncStatus Of(ISyncStatus status)
    {
        var message = new V1.SyncStatus
        {
            State = StateOf(status.State),
            Failure = status.Failure is { } failure ? FailureOf(failure) : V1.SyncFailure.Unspecified,
            Details = status.Details ?? "",
            HasPendingChanges = status.HasPendingChanges,
        };
        if (status.LastSuccessfulSync is { } last)
            message.LastSuccessfulSync = Timestamp.FromDateTimeOffset(last);
        foreach (var file in status.SkippedFiles)
        {
            message.SkippedFiles.Add(new V1.SkippedFile
            {
                RelativePath = file.RelativePath,
                Reason = file.Reason,
                RequiresUpdate = file.RequiresUpdate,
            });
        }

        return message;
    }

    private static V1.SyncState StateOf(SyncState state) => state switch
    {
        SyncState.Idle => V1.SyncState.Idle,
        SyncState.Working => V1.SyncState.Working,
        SyncState.Offline => V1.SyncState.Offline,
        SyncState.Error => V1.SyncState.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The sync state has no message value."),
    };

    private static V1.SyncFailure FailureOf(SyncFailure failure) => failure switch
    {
        SyncFailure.Connection => V1.SyncFailure.Connection,
        SyncFailure.Authentication => V1.SyncFailure.Authentication,
        SyncFailure.UnknownHostKey => V1.SyncFailure.UnknownHostKey,
        SyncFailure.ChangedHostKey => V1.SyncFailure.ChangedHostKey,
        SyncFailure.Repository => V1.SyncFailure.Repository,
        SyncFailure.PushRejected => V1.SyncFailure.PushRejected,
        SyncFailure.InvalidFile => V1.SyncFailure.InvalidFile,
        SyncFailure.LocalStorage => V1.SyncFailure.LocalStorage,
        SyncFailure.Unexpected => V1.SyncFailure.Unexpected,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "The sync failure has no message value."),
    };
}
