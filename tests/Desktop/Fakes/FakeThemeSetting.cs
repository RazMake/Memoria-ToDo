using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IThemeSetting"/>. A test sets <see cref="IsDark"/>, raises
/// <see cref="MayHaveChanged"/> itself (from any thread), and can make reading fail. Like Windows' setting, it
/// tells only the handlers added at that moment.
/// </summary>
public sealed class FakeThemeSetting : IThemeSetting
{
    private readonly object _gate = new();
    private EventHandler? _handlers;
    private int _reads;
    private volatile bool _isDark;
    private bool? _switchAfterNextRead;

    /// <summary>What reading says. Starts light.</summary>
    public bool IsDark
    {
        get => _isDark;
        set => _isDark = value;
    }

    /// <summary>When set, every read throws it.</summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>How many times it was read, including reads that threw.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <summary>How many handlers are added to <see cref="MayHaveChanged"/> right now.</summary>
    public int Listeners
    {
        get
        {
            lock (_gate) { return _handlers?.GetInvocationList().Length ?? 0; }
        }
    }

    /// <inheritdoc />
    public event EventHandler? MayHaveChanged
    {
        add
        {
            lock (_gate) { _handlers += value; }
        }
        remove
        {
            lock (_gate) { _handlers -= value; }
        }
    }

    /// <inheritdoc />
    public bool ReadIsDark()
    {
        Interlocked.Increment(ref _reads);
        if (ReadFailure is { } failure)
        {
            throw failure;
        }

        var isDark = _isDark;
        bool? pending;
        lock (_gate)
        {
            pending = _switchAfterNextRead;
            _switchAfterNextRead = null;
        }

        if (pending is { } next)
        {
            Switch(next); // Windows switching just after this read answered, before its reader acts on the answer.
        }

        return isDark;
    }

    /// <summary>Raises <see cref="MayHaveChanged"/> on the calling thread, to the handlers added now.</summary>
    public void RaiseMayHaveChanged()
    {
        EventHandler? handlers;
        lock (_gate) { handlers = _handlers; }
        handlers?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets <see cref="IsDark"/>, then raises <see cref="MayHaveChanged"/>.</summary>
    public void Switch(bool isDark)
    {
        IsDark = isDark;
        RaiseMayHaveChanged();
    }

    /// <summary>Makes the next read answer with the current value, then switch to <paramref name="isDark"/> and raise <see cref="MayHaveChanged"/>.</summary>
    public void SwitchAfterNextRead(bool isDark)
    {
        lock (_gate) { _switchAfterNextRead = isDark; }
    }
}
