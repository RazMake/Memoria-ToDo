using System.ComponentModel;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.McpServer.Tools;

/// <summary>The tools for settings that live in Configure: the deletion delay and Start with Windows.</summary>
internal sealed class SettingsTools(ToolRunner run, UiContext ui, ISharedTaskSettings settings, IStartWithWindows startWithWindows)
{
    [McpServerTool(ReadOnly = true), Description("Gets how many days a resolved task stays before it is deleted.")]
    public Task<string> GetDeletionDelay() => run.Run(() => new { days = settings.DeletionDelayDays });

    [McpServerTool(Destructive = true), Description("Sets how many days a resolved task stays before it is deleted. A shorter delay deletes tasks sooner, and that can't be undone.")]
    public Task<string> SetDeletionDelay([Description("From 1 to 365.")] int days) => run.Run(() =>
    {
        settings.SetDeletionDelayDays(days);
        return new { days };
    });

    [McpServerTool(ReadOnly = true), Description("Gets whether the app starts when the user signs in to Windows.")]
    public Task<string> GetStartWithWindows(CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token => await ui.RunAsync<object>(() => new { on = startWithWindows.IsOn() }, token).ConfigureAwait(false));

    [McpServerTool(Destructive = false), Description("Sets whether the app starts when the user signs in to Windows.")]
    public Task<string> SetStartWithWindows([Description("True to start with Windows.")] bool on, CancellationToken cancellationToken) =>
        run.RunAsync(cancellationToken, async token => await ui.RunAsync<object>(() =>
        {
            startWithWindows.Set(on);
            return new { on = startWithWindows.IsOn() };
        }, token).ConfigureAwait(false));
}
