using Memoria.ToDo.Desktop.Configure;
using Serilog;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// The Contacts shortcut's action. It keeps the shortcut Configure gave it, and holds it in Windows only while contacts
/// are available. While they aren't, Register checks nothing with Windows, keeps the shortcut, and answers true.
/// </summary>
/// <param name="globalShortcut">Windows' shortcuts, through which the action holds its shortcut while contacts are available.</param>
/// <param name="pressed">Runs when the shortcut is pressed.</param>
/// <param name="isAvailable">Whether contacts are available now.</param>
/// <param name="taken">Runs when the kept shortcut can't be held because Windows or another app has it.</param>
/// <param name="logger">Where a taken shortcut is logged.</param>
internal sealed class ContactsShortcutAction(
    IGlobalShortcut globalShortcut, Action pressed, Func<bool> isAvailable, Action taken, ILogger logger) : IShortcutAction
{
    private readonly ActiveShortcut _held = new(globalShortcut, pressed, "Contacts", logger);
    private Shortcut? _kept;

    /// <inheritdoc />
    public bool Register(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if (isAvailable() && !_held.Register(shortcut))
        {
            return false;
        }

        _kept = shortcut;
        return true;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        _kept = null;
        _held.Unregister();
    }

    /// <summary>
    /// Follows a change of availability: holds the kept shortcut, if any, when contacts became available, forgetting it
    /// and running <c>taken</c> when it's taken, and releases it, keeping it, when they became unavailable. A port
    /// failure other than a taken shortcut passes on.
    /// </summary>
    public void AvailabilityChanged()
    {
        if (!isAvailable())
        {
            _held.Unregister();
        }
        else if (_kept is { } kept && !_held.Register(kept))
        {
            _kept = null;
            taken();
        }
    }
}
