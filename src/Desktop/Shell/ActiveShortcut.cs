using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Keeps one shortcut registered with Windows at a time, and runs <paramref name="pressed"/> when it's pressed.</summary>
internal sealed class ActiveShortcut(IGlobalShortcut globalShortcut, Action pressed) : IShortcutAction
{
    private Shortcut? active;

    /// <summary>
    /// Makes <paramref name="shortcut"/> the active one. Returns false, keeping the previous shortcut, when another
    /// app holds it; a port failure passes on to the caller, also keeping the previous shortcut. When releasing the
    /// previous one fails and releasing the new one again fails too, both stay registered and both failures pass on.
    /// </summary>
    public bool Register(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if (shortcut == active)
        {
            return true;
        }

        if (!globalShortcut.Register(shortcut, pressed))
        {
            return false;
        }

        if (active is not null)
        {
            try
            {
                globalShortcut.Unregister(active);
            }
            catch (Exception releaseFailure)
            {
                try
                {
                    globalShortcut.Unregister(shortcut);
                }
                catch (Exception undoFailure)
                {
                    throw new AggregateException(releaseFailure, undoFailure);
                }

                throw;
            }
        }

        active = shortcut;
        return true;
    }

    /// <summary>Unregisters the active shortcut, if any.</summary>
    public void Unregister()
    {
        if (active is not null)
        {
            globalShortcut.Unregister(active);
            active = null;
        }
    }
}
