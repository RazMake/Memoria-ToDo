using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Sync settings section's content: three cards, Repository, Git Access, and Auto, kept current from the sync
/// notices of its window.
/// </summary>
internal sealed class SyncSettingsSection : StackPanel
{
    private readonly RepositorySection _repository;
    private readonly DeviceSection _device;
    private readonly StatusSection _status;
    private readonly ITheme _theme;

    public SyncSettingsSection(SyncUi ui, DeletionDelayRow delay, ITheme theme)
    {
        _theme = theme;
        _status = new StatusSection(ui);
        _repository = new RepositorySection(ui, _status);
        _device = new DeviceSection(ui);
        Card("Repository", _repository);
        Card("Git Access", _device);
        Card("Auto", delay);
        ui.Changed += Refresh;
    }

    private void Card(string title, UIElement content)
    {
        var card = ThemedBox.Card(title, content, _theme);
        card.Margin = new Thickness(0, Children.Count == 0 ? 0 : 12, 0, 0);
        Children.Add(card);
    }

    private void Refresh()
    {
        _repository.Refresh();
        _device.Refresh();
        _status.Refresh();
    }
}
