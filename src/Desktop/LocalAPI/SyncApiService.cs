using Grpc.Core;
using Memoria.ToDo.Shared.Sync;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The Sync service: maps each call to <see cref="ISyncService"/>. Named apart from <c>Memoria.ToDo.Shared.Sync.SyncService</c>, which is
/// Sync's own factory.
/// </summary>
internal sealed class SyncApiService : V1.Sync.SyncBase
{
    private readonly ISyncService sync;

    public SyncApiService(ISyncService sync) => this.sync = sync;

    /// <inheritdoc />
    public override Task<V1.SyncStatus> GetStatus(V1.GetStatusRequest request, ServerCallContext context) =>
        Task.FromResult(SyncStatusMessages.Of(sync.Status));

    /// <inheritdoc />
    public override async Task<V1.SyncStatus> SyncNow(V1.SyncNowRequest request, ServerCallContext context) =>
        SyncStatusMessages.Of(await sync.SyncNowAsync(context.CancellationToken));

    /// <inheritdoc />
    public override Task WatchStatus(V1.WatchStatusRequest request, IServerStreamWriter<V1.SyncStatus> responseStream, ServerCallContext context) =>
        ChangeStream.SendAsync(
            handler => sync.StatusChanged += handler, handler => sync.StatusChanged -= handler, responseStream,
            () => SyncStatusMessages.Of(sync.Status), context.CancellationToken);
}
