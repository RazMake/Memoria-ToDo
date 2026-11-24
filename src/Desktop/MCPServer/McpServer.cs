using System.IO;
using Memoria.ToDo.Desktop.Logs;
using Memoria.ToDo.Desktop.MCPServer.Tools;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>Makes the MCP server, which lets AI assistants read and change tasks and contacts, over a named pipe only the current user can open.</summary>
public static class McpServer
{
    /// <summary>The argument that starts the app's program as the bridge: <c>--mcp</c>.</summary>
    public const string BridgeArgument = "--mcp";

    /// <summary>
    /// Makes the server, not started and not listening. <paramref name="deviceFolder"/> keeps its on/off switch, <paramref name="pipeName"/>
    /// is the named pipe it listens on, and <paramref name="bridgePath"/> is the app's executable that the connection lines run.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">A folder or path isn't fully qualified, or the pipe name is empty.</exception>
    public static IMcpServer Create(
        ITasks tasks, IDirectTaskChanges direct, ITagColors tagColors, ITagOrder tagOrder, ISharedTaskSettings settings, IContacts contacts, ISyncService sync,
        IUpdateStatus updates, IStartWithWindows startWithWindows, ILogSource logs, IFileSystem files,
        string deviceFolder, string pipeName, string bridgePath, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(direct);
        ArgumentNullException.ThrowIfNull(tagColors);
        ArgumentNullException.ThrowIfNull(tagOrder);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(deviceFolder);
        ArgumentNullException.ThrowIfNull(pipeName);
        ArgumentNullException.ThrowIfNull(bridgePath);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (!Path.IsPathFullyQualified(deviceFolder))
        {
            throw new ArgumentException("The device folder must be a full path.", nameof(deviceFolder));
        }

        if (!Path.IsPathFullyQualified(bridgePath))
        {
            throw new ArgumentException("The bridge path must be a full path.", nameof(bridgePath));
        }

        var runner = new ToolRunner(logger);
        var ui = new UiContext();
        var clone = new CloneCheck(sync);
        var groups = new object[]
        {
            new TaskReadTools(runner, clone, tasks),
            new TaskChangeTools(runner, clone, tasks, direct),
            new TagTools(runner, clone, tasks, tagColors, tagOrder),
            new SettingsTools(runner, ui, settings, startWithWindows),
            new ContactTools(runner, clone, contacts),
            new SyncTools(runner, sync),
            new UpdateTools(runner, ui, updates),
            new LogTools(runner, logs, time, logger),
        };
        return new McpServerService(
            new OnOffSettingFile(files, Path.Combine(deviceFolder, "mcp-server.txt"), whenMissing: true, "The MCP server's switch file", logger), runner, new ConnectionServer(ToolCatalog.Of(groups), logger), ui,
            pipeName, ConnectionLines.For(bridgePath), logger);
    }
}
