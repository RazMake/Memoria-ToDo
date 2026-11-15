namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// One window's shortcut, by the shell design's switching table. Does nothing while Configure or its own window is
/// open, which also covers both windows being open. Otherwise it first closes the other window when that is open, and
/// shows its own window only if the other one closed.
/// </summary>
/// <param name="configureOpen">Whether Configure is open.</param>
/// <param name="ownOpen">Whether the shortcut's own window is open.</param>
/// <param name="otherOpen">Whether the other window is open.</param>
/// <param name="closeOther">Closes the other window; true when it closed.</param>
/// <param name="showOwn">Shows the shortcut's own window.</param>
internal sealed class ShortcutSwitch(
    Func<bool> configureOpen,
    Func<bool> ownOpen,
    Func<bool> otherOpen,
    Func<bool> closeOther,
    Action showOwn)
{
    /// <summary>Runs the shortcut's press.</summary>
    public void Press()
    {
        if (configureOpen() || ownOpen())
        {
            return;
        }

        if (!otherOpen() || closeOther())
        {
            showOwn();
        }
    }
}
