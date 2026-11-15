using System.Windows;
using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IUpdateStatus"/>, for the tests of the windows that show updates. A test sets the state
/// with <see cref="Publish"/>, which raises <see cref="Changed"/> on the calling thread, and reads the calls the window made.
/// It starts with no update, on version 2.1.0, with Auto update off.
/// </summary>
public sealed class FakeUpdateStatus : IUpdateStatus
{
    private readonly List<bool> setAutoUpdateCalls = [];
    private readonly List<Window> whatIsNewOwners = [];

    /// <inheritdoc />
    public UpdateState State { get; set; } = new(UpdateStage.NoUpdate, "2.1.0", null, null);

    /// <inheritdoc />
    public bool AutoUpdate { get; set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>When set, <see cref="SetAutoUpdate"/> throws it and Auto update stays as it was.</summary>
    public Exception? SetAutoUpdateFailure { get; set; }

    /// <summary>The values <see cref="SetAutoUpdate"/> was called with, in order, including those that failed.</summary>
    public IReadOnlyList<bool> SetAutoUpdateCalls => setAutoUpdateCalls;

    /// <summary>How many times <see cref="UpdateNow"/> was called.</summary>
    public int UpdateNowCount { get; private set; }

    /// <summary>The windows <see cref="ShowWhatIsNew"/> was called with, in order. It shows nothing.</summary>
    public IReadOnlyList<Window> WhatIsNewOwners => whatIsNewOwners;

    /// <summary>Sets <see cref="State"/>, then raises <see cref="Changed"/>.</summary>
    public void Publish(UpdateState state)
    {
        State = state;
        RaiseChanged();
    }

    /// <summary>Raises <see cref="Changed"/> on the calling thread.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void SetAutoUpdate(bool on)
    {
        setAutoUpdateCalls.Add(on);
        if (SetAutoUpdateFailure is not null)
        {
            throw SetAutoUpdateFailure;
        }

        AutoUpdate = on;
        RaiseChanged();
    }

    /// <inheritdoc />
    public void UpdateNow() => UpdateNowCount++;

    /// <inheritdoc />
    public void ShowWhatIsNew(Window owner) => whatIsNewOwners.Add(owner);
}
