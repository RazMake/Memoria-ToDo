using System.ComponentModel;

namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>Whether this Windows session is locked. The composition root implements it.</summary>
public interface ISessionLock
{
    /// <summary>True while the session is locked or disconnected.</summary>
    /// <exception cref="Win32Exception">Windows can't say.</exception>
    bool IsLocked { get; }

    /// <summary>Raised on the UI thread after the session locks, unlocks, disconnects, or reconnects.</summary>
    event EventHandler? Changed;
}
