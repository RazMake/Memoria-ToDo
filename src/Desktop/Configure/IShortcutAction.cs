namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// One of the app's global shortcuts, as Configure uses it: Shell implements it for Add new task and again for Manage
/// tasks. Configure calls it on the UI thread.
/// </summary>
public interface IShortcutAction
{
    /// <summary>
    /// Registers <paramref name="shortcut"/> in place of the action's previous one. Returns true at once when it's
    /// already the action's shortcut, and false, keeping the previous one, when Windows or another app holds it.
    /// </summary>
    /// <exception cref="Exception">Any other failure; the previous shortcut stays.</exception>
    bool Register(Shortcut shortcut);

    /// <summary>
    /// Releases the action's shortcut, so its keys reach the focused window. Does nothing when there's none.
    /// </summary>
    /// <exception cref="Exception">The release failed; the shortcut stays registered.</exception>
    void Unregister();
}
