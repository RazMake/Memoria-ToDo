using System.ComponentModel;
using Memoria.ToDo.Desktop.Updates;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The tools for app updates. Updates expects its calls on the UI thread.</summary>
internal sealed class UpdateTools(ToolRunner run, UiContext ui, IUpdateStatus updates)
{
    [McpServerTool(ReadOnly = true), Description("Gets whether an update is available, the current and new versions, what went wrong, and whether Auto update is on.")]
    public Task<string> GetUpdateStatus(CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token => await ui.RunAsync(StatusOf, token).ConfigureAwait(false));

    [McpServerTool(Destructive = false), Description("Turns Auto update on or off and answers the update status. Fails when the app isn't installed or is updating.")]
    public Task<string> SetAutoUpdate([Description("True for on.")] bool on, CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token => await ui.RunAsync(() =>
        {
            updates.SetAutoUpdate(on);
            return StatusOf();
        }, token).ConfigureAwait(false));

    private object StatusOf()
    {
        var state = updates.State;
        return new
        {
            stage = state.Stage,
            currentVersion = state.CurrentVersion,
            newVersion = state.NewVersion,
            failure = state.Failure,
            autoUpdate = updates.AutoUpdate,
        };
    }
}
