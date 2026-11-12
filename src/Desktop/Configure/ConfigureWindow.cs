using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Creates the Configure window.</summary>
public static class ConfigureWindow
{
    /// <summary>Creates Configure. The window opens only on <see cref="IConfigureWindow.Show"/>.</summary>
    /// <param name="startWithWindows">The Start with Windows setting the window shows and switches.</param>
    /// <param name="settings">The same store's shared settings, including shortcut text.</param>
    /// <param name="sync">The device's setup operations and cached sync status.</param>
    /// <param name="setupActions">The app's clipboard and add-key-page actions.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static IConfigureWindow Create(IStartWithWindows startWithWindows, ISharedTaskSettings settings,
        ISyncService sync, ISetupActions setupActions) =>
        new ConfigureShow(startWithWindows, settings, sync, setupActions);
}

/// <summary>The Configure window, as Shell uses it.</summary>
public interface IConfigureWindow : IDisposable
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Connects the two shortcut settings. Configure applies each stored shortcut, or its default, then each
    /// valid pick on that row, on the UI thread. Returns true only when both initial shortcuts registered; an action that
    /// returns false or throws counts as not registered. Dispose detaches service notices and closes Configure;
    /// Shell owns unregistering the actions and disposing Sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either action is null.</exception>
    bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks);
}
