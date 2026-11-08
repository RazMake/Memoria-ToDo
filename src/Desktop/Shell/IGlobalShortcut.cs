using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Port to Windows' system-wide keyboard shortcuts.</summary>
public interface IGlobalShortcut
{
    /// <summary>
    /// Asks Windows to run <paramref name="pressed"/>, on the UI thread, whenever <paramref name="shortcut"/> is pressed.
    /// Returns false when another app holds the shortcut; throws on any other error.
    /// </summary>
    bool Register(Shortcut shortcut, Action pressed);

    /// <summary>Gives the shortcut back to Windows. Does nothing when it isn't registered.</summary>
    void Unregister(Shortcut shortcut);
}
