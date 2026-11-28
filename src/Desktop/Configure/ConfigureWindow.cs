using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Creates the Configure window.</summary>
public static class ConfigureWindow
{
    /// <summary>Creates Configure. The window opens only on <see cref="IConfigureWindow.Show"/>.</summary>
    /// <param name="startWithWindows">The Start with Windows setting the window shows and switches.</param>
    /// <param name="settings">The same store's shared settings, including shortcut text.</param>
    /// <param name="sync">The device's setup operations and cached sync status.</param>
    /// <param name="setupActions">The app's clipboard and add-key-page actions.</param>
    /// <param name="closeWindowKeys">The platform's Close window keys; Configure sets them and attaches its window.</param>
    /// <param name="theme">The app's theme; the section titles show in heading 1's color for it and follow its switches.</param>
    /// <param name="updates">The update status; the Update card in Application shows it and calls it for each control.</param>
    /// <param name="mcpServer">The MCP server's status; the AI assistants row in Behavior and its Examples dialog show it and call it for the switch.</param>
    /// <param name="saveKeys">The platform's Save keys; Configure sets them from the stored or default Save shortcut.</param>
    /// <param name="keepAwake">Keep awake's status; the Prevent screen saver row in Behavior shows it and calls it for its switch.</param>
    /// <param name="logger">Where Configure logs each failure it catches and shows, at Error.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static IConfigureWindow Create(IStartWithWindows startWithWindows, ISharedTaskSettings settings,
        ISyncService sync, ISetupActions setupActions, CloseWindowKeys closeWindowKeys, ITheme theme,
        IUpdateStatus updates, IMcpServerStatus mcpServer, SaveKeys saveKeys, IKeepAwakeStatus keepAwake, ILogger logger) =>
        new ConfigureShow(startWithWindows, settings, sync, setupActions, closeWindowKeys, theme, updates, mcpServer, saveKeys, keepAwake, logger);
}

/// <summary>The Configure window, as Shell uses it.</summary>
public interface IConfigureWindow : IDisposable
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>Closes the window when it is open; does nothing otherwise.</summary>
    void Close();

    /// <summary>
    /// Connects the four shortcut settings. Configure applies each stored shortcut, or its default, then each
    /// valid pick on that row, on the UI thread. Returns true only when all initial shortcuts registered; an action that
    /// returns false or throws counts as not registered. Dispose detaches service notices and closes Configure;
    /// Shell owns unregistering the actions and disposing Sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any action is null.</exception>
    bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks, IShortcutAction configure, IShortcutAction contacts);
}
