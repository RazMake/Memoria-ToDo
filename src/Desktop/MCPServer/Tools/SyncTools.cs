using System.ComponentModel;
using Memoria.ToDo.Shared.Sync;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The tools for synchronization.</summary>
internal sealed class SyncTools(ToolRunner run, ISyncService sync)
{
    [McpServerTool(ReadOnly = true), Description("Gets the synchronization status: its state, what went wrong, when it last succeeded, whether changes wait to be sent, the files it skipped, and this device's name.")]
    public Task<string> GetSyncStatus() => run.Run(StatusOf);

    [McpServerTool(Destructive = false), Description("Synchronizes now, waits for it to end, and answers the status.")]
    public Task<string> SyncNow(CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token =>
        {
            await sync.SyncNowAsync(token).ConfigureAwait(false);
            return StatusOf();
        });

    [McpServerTool(Destructive = false), Description("Sets this device's name, as shown in Configure, and answers it.")]
    public Task<string> SetDeviceName([Description("A name on one line, not empty.")] string name, CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token =>
        {
            await sync.Setup.SetDeviceNameAsync(name, token).ConfigureAwait(false);
            return new { deviceName = sync.Setup.State.DeviceName };
        });

    private object StatusOf()
    {
        var status = sync.Status;
        return new
        {
            state = status.State,
            failure = status.Failure,
            details = status.Details,
            lastSuccessfulSync = status.LastSuccessfulSync,
            hasPendingChanges = status.HasPendingChanges,
            skippedFiles = SkippedFiles.Of(status.SkippedFiles),
            deviceName = sync.Setup.State.DeviceName,
        };
    }
}
