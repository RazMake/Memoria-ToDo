namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Creates the Configure window.</summary>
public static class ConfigureWindow
{
    /// <summary>Creates Configure. The window opens only on <see cref="IConfigureWindow.Show"/>.</summary>
    /// <param name="startWithWindows">The Start with Windows setting the window shows and switches.</param>
    /// <param name="repositoryFolder">The clone the Sync settings section shows and sets.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static IConfigureWindow Create(IStartWithWindows startWithWindows, IRepositoryFolder repositoryFolder) =>
        new ConfigureShow(startWithWindows, repositoryFolder);
}

/// <summary>The Configure window, as Shell uses it.</summary>
public interface IConfigureWindow
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Connects the two shortcut settings. Configure registers each default at once through its action, then each
    /// valid pick on that row, on the UI thread. Returns true only when both defaults registered; an action that
    /// returns false or throws counts as not registered.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either action is null.</exception>
    bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks);
}
