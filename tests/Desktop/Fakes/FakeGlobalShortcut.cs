using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IGlobalShortcut"/>. Like Windows, it refuses a shortcut that another app holds
/// or that is already registered, and it does nothing when a shortcut is released that isn't registered. A test
/// can hold a shortcut for another app, make the port fail, and press a registered shortcut.
/// </summary>
public sealed class FakeGlobalShortcut : IGlobalShortcut
{
    private readonly Dictionary<Shortcut, Action> registered = [];
    private readonly HashSet<Shortcut> heldByAnotherApp = [];
    private readonly Dictionary<Shortcut, Exception> unregisterFailures = [];

    /// <summary>The shortcuts that are registered now.</summary>
    public IReadOnlyCollection<Shortcut> Registered => [.. registered.Keys];

    /// <summary>How many times Register was called, whatever it answered.</summary>
    public int RegisterCount { get; private set; }

    /// <summary>When set, Register throws it, as when the port fails for a reason other than a taken shortcut.</summary>
    public Exception? RegisterFailure { get; set; }

    /// <summary>Makes releasing the shortcut throw the failure and leave it registered, as when the port fails to release it.</summary>
    public void FailUnregister(Shortcut shortcut, Exception failure) => unregisterFailures[shortcut] = failure;

    /// <summary>Makes another app hold the shortcut, so Register answers false.</summary>
    public void HoldByAnotherApp(Shortcut shortcut) => heldByAnotherApp.Add(shortcut);

    /// <summary>Presses a registered shortcut, which runs its action.</summary>
    /// <exception cref="InvalidOperationException">The shortcut isn't registered.</exception>
    public void Press(Shortcut shortcut)
    {
        if (!registered.TryGetValue(shortcut, out var pressed))
        {
            throw new InvalidOperationException($"{shortcut} isn't registered, so pressing it does nothing.");
        }

        pressed();
    }

    /// <inheritdoc />
    public bool Register(Shortcut shortcut, Action pressed)
    {
        if (RegisterFailure is not null)
        {
            throw RegisterFailure;
        }

        RegisterCount++;
        if (heldByAnotherApp.Contains(shortcut) || registered.ContainsKey(shortcut))
        {
            return false;
        }

        registered[shortcut] = pressed;
        return true;
    }

    /// <inheritdoc />
    public void Unregister(Shortcut shortcut)
    {
        if (unregisterFailures.TryGetValue(shortcut, out var failure))
        {
            throw failure;
        }

        registered.Remove(shortcut);
    }
}
