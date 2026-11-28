using Memoria.ToDo.Desktop.KeepAwake;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IKeepAwakeStatus"/>, for the tests of the windows that show Keep awake. A test sets the state
/// with <see cref="Publish"/>, which raises <see cref="Changed"/> on the calling thread, and reads the calls the window made.
/// It starts off, not paused, with no failure.
/// </summary>
public sealed class FakeKeepAwakeStatus : IKeepAwakeStatus
{
    private readonly List<bool> setOnCalls = [];

    /// <inheritdoc />
    public KeepAwakeState State { get; set; } = new(false, false, null);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>When set, <see cref="SetOn"/> throws it and the state stays as it was.</summary>
    public Exception? SetOnFailure { get; set; }

    /// <summary>The values <see cref="SetOn"/> was called with, in order, including those that failed.</summary>
    public IReadOnlyList<bool> SetOnCalls => setOnCalls;

    /// <summary>Sets <see cref="State"/>, then raises <see cref="Changed"/>.</summary>
    public void Publish(KeepAwakeState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void SetOn(bool on)
    {
        setOnCalls.Add(on);
        if (SetOnFailure is not null)
        {
            throw SetOnFailure;
        }

        Publish(new KeepAwakeState(on, false, null));
    }
}
