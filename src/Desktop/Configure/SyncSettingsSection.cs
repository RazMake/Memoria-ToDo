using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Sync settings section's content, kept current from the sync notices of its window.</summary>
internal sealed class SyncSettingsSection : StackPanel
{
    private readonly RepositorySection _repository;
    private readonly DeviceSection _device;
    private readonly StatusSection _status;

    public SyncSettingsSection(SyncUi ui)
    {
        _repository = new RepositorySection(ui);
        _device = new DeviceSection(ui);
        _status = new StatusSection(ui);
        Children.Add(_repository);
        Children.Add(_device);
        Children.Add(_status);
        ui.Changed += Refresh;
    }

    private void Refresh()
    {
        _repository.Refresh();
        _device.Refresh();
        _status.Refresh();
    }
}
