namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>What Configure and the Task list use.</summary>
public interface IKeepAwakeStatus
{
    /// <summary>The state now.</summary>
    KeepAwakeState State { get; }

    /// <summary>Raised on the UI thread after <see cref="State"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Turns the setting on or off and saves it on this device. Fails, keeping the previous value, when the setting can't be saved.
    /// A request Windows refuses doesn't fail the call: it shows in <see cref="KeepAwakeState.Failure"/>.
    /// </summary>
    void SetOn(bool on);
}
