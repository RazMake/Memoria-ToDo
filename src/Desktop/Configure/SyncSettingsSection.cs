using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Sync settings section's content, in the groups Repository, Device, Status, and Tasks, kept current from the sync notices of its window.</summary>
internal sealed class SyncSettingsSection : StackPanel
{
    private readonly RepositorySection _repository;
    private readonly DeviceSection _device;
    private readonly StatusSection _status;

    public SyncSettingsSection(SyncUi ui, DeletionDelayRow delay)
    {
        _repository = new RepositorySection(ui);
        _device = new DeviceSection(ui);
        _status = new StatusSection(ui);
        Group("Repository", _repository);
        Group("Device", _device);
        Group("Status", _status);
        Group("Tasks", delay);
        ui.Changed += Refresh;
    }

    private void Group(string heading, UIElement content)
    {
        Children.Add(ThemedText.Subheading(heading, new Thickness(0, Children.Count == 0 ? 0 : 16, 0, 6)));
        Children.Add(content);
    }

    private void Refresh()
    {
        _repository.Refresh();
        _device.Refresh();
        _status.Refresh();
    }
}
