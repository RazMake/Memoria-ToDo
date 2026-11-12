using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// One call to a <see cref="FakeShortcutAction"/>: Register with a shortcut, or Unregister when
/// <see cref="Registered"/> is null.
/// </summary>
public readonly record struct ShortcutCall(Shortcut? Registered)
{
    /// <summary>Whether the call was Unregister.</summary>
    public bool IsUnregister => Registered is null;

    /// <summary>The call Register(<paramref name="shortcut"/>), to compare with <see cref="FakeShortcutAction.Calls"/>.</summary>
    public static ShortcutCall Register(Shortcut shortcut) => new(shortcut);

    /// <summary>The call Unregister, to compare with <see cref="FakeShortcutAction.Calls"/>.</summary>
    public static ShortcutCall Unregister() => new(null);
}

/// <summary>
/// The shared fake of <see cref="IShortcutAction"/>: records every Register and Unregister call in order. A test sets
/// what Register answers, or makes either call throw, at any time.
/// </summary>
public sealed class FakeShortcutAction : IShortcutAction
{
    private readonly List<ShortcutCall> calls = [];

    /// <summary>Every call since the fake was made or <see cref="ClearCalls"/> ran, in order.</summary>
    public IReadOnlyList<ShortcutCall> Calls => calls;
    public Shortcut? Registered { get; private set; }

    /// <summary>What Register answers for a shortcut; true for every shortcut when not set.</summary>
    public Func<Shortcut, bool> Answer { get; set; } = _ => true;

    /// <summary>When set, Register throws it after recording the call.</summary>
    public Exception? RegisterFailure { get; set; }

    /// <summary>When set, Unregister throws it after recording the call.</summary>
    public Exception? UnregisterFailure { get; set; }

    /// <summary>Forgets the calls so far, so a test can look at what follows.</summary>
    public void ClearCalls() => calls.Clear();

    /// <inheritdoc />
    public bool Register(Shortcut shortcut)
    {
        calls.Add(ShortcutCall.Register(shortcut));
        var accepted = RegisterFailure is null ? Answer(shortcut) : throw RegisterFailure;
        if (accepted)
        {
            Registered = shortcut;
        }
        return accepted;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        calls.Add(ShortcutCall.Unregister());
        if (UnregisterFailure is not null)
        {
            throw UnregisterFailure;
        }
        Registered = null;
    }
}
