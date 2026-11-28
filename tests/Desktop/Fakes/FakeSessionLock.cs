using Memoria.ToDo.Desktop.KeepAwake;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="ISessionLock"/>: the session starts unlocked. A test changes it with <see cref="Lock"/>
/// and <see cref="Unlock"/>, which raise <see cref="Changed"/> on the calling thread after the state changed, as Windows
/// does, or with <see cref="Raise"/> for an event that changes nothing. It can be told to be unable to say.
/// </summary>
public sealed class FakeSessionLock : ISessionLock
{
    /// <summary>Whether the session is locked now, as <see cref="IsLocked"/> says when <see cref="Failure"/> is null. Setting it raises no event.</summary>
    public bool Locked { get; set; }

    /// <summary>When set, reading <see cref="IsLocked"/> throws it, as when Windows can't say. Null ends it.</summary>
    public Exception? Failure { get; set; }

    /// <summary>How many times <see cref="IsLocked"/> was read, including reads that failed.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public bool IsLocked
    {
        get
        {
            Reads++;
            return Failure is not null ? throw Failure : Locked;
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Locks the session, then raises <see cref="Changed"/>.</summary>
    public void Lock() => Change(true);

    /// <summary>Unlocks the session, then raises <see cref="Changed"/>.</summary>
    public void Unlock() => Change(false);

    /// <summary>Raises <see cref="Changed"/> without changing the state, as a repeated event does.</summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private void Change(bool locked)
    {
        Locked = locked;
        Raise();
    }
}
