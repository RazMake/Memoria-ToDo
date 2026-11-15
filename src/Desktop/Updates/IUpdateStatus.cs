using System.Windows;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>What Add, Task View, and Configure use.</summary>
public interface IUpdateStatus
{
    /// <summary>The state now.</summary>
    UpdateState State { get; }

    /// <summary>Raised on the UI thread after <see cref="State"/> or <see cref="AutoUpdate"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Whether Auto update is on.</summary>
    bool AutoUpdate { get; }

    /// <summary>
    /// Turns Auto update on or off and saves it on this device. Fails, keeping the previous value, when
    /// the setting can't be saved, or when the app isn't installed or is updating.
    /// </summary>
    void SetAutoUpdate(bool on);

    /// <summary>Starts an update to the available version. Does nothing unless <see cref="State"/>'s stage is Available.</summary>
    void UpdateNow();

    /// <summary>Opens What is new, modal and owned by the window. Does nothing unless a version is available or updating.</summary>
    void ShowWhatIsNew(Window owner);
}
