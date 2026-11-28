using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Sends every task window action to Configure until the device's sync setup is complete.</summary>
internal sealed class SetupRedirect(ISyncService sync, IConfigureWindow configure)
{
    /// <summary>An action that runs <paramref name="show"/> once the device is configured, and opens Configure before.</summary>
    public Action Through(Action show) => () =>
    {
        if (sync.Setup.State.IsConfigured)
        {
            show();
        }
        else
        {
            configure.Show();
        }
    };
}
