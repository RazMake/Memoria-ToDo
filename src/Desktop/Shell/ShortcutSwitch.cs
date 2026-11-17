namespace Memoria.ToDo.Desktop.Shell;

/// <summary>A window a shortcut may have to close before showing its own.</summary>
/// <param name="IsOpen">Whether the window is open.</param>
/// <param name="Close">Closes the window; true when it closed.</param>
internal readonly record struct OtherWindow(Func<bool> IsOpen, Func<bool> Close);

/// <summary>
/// One window's shortcut, by the shell design's switching table. Brings its own window forward when it is open, and
/// does nothing while Configure is open. Otherwise it closes the one other window that is open, if any, and shows its own window only if that closed.
/// With two of the other windows open it does nothing.
/// </summary>
/// <param name="configureOpen">Whether Configure is open.</param>
/// <param name="ownOpen">Whether the shortcut's own window is open.</param>
/// <param name="others">The other windows the shortcut may close first.</param>
/// <param name="showOwn">Shows the shortcut's own window.</param>
internal sealed class ShortcutSwitch(
    Func<bool> configureOpen,
    Func<bool> ownOpen,
    OtherWindow[] others,
    Action showOwn)
{
    /// <summary>Runs the shortcut's press.</summary>
    public void Press()
    {
        if (ownOpen())
        {
            showOwn();
            return;
        }

        if (configureOpen())
        {
            return;
        }

        OtherWindow? open = null;
        foreach (var other in others)
        {
            if (!other.IsOpen())
            {
                continue;
            }

            if (open is not null)
            {
                return;
            }

            open = other;
        }

        if (open is null || open.Value.Close())
        {
            showOwn();
        }
    }
}
