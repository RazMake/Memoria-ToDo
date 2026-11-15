using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The sync status: a Sync button in front of the last successful sync, then pending changes, why the sync fails, and
/// the skipped files, then the message about a sync the button started.
/// </summary>
internal sealed class StatusSection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly TextBlock _lastSync = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _message = ThemedText.Message();

    public StatusSection(SyncUi ui)
    {
        _ui = ui;
        var sync = ConfigureButton.Make("Sync");
        sync.Click += async (_, _) => await SyncNowAsync();
        var firstLine = new StackPanel { Orientation = Orientation.Horizontal };
        firstLine.Children.Add(sync);
        firstLine.Children.Add(_lastSync);
        Margin = new Thickness(0, 0, 0, 8);
        Children.Add(firstLine);
        Children.Add(_details);
        Children.Add(_message);
        Refresh();
    }

    /// <summary>Shows the sync status the service holds now.</summary>
    public void Refresh()
    {
        _lastSync.Text = SyncText.LastSync(_ui.Sync.Status);
        ThemedText.ShowMessage(_details, SyncText.StatusDetails(_ui.Sync.Status));
    }

    private async Task SyncNowAsync()
    {
        ThemedText.ShowMessage(_message, null);
        await _ui.RunAsync(
            token => _ui.SyncNowAsync(text => ThemedText.ShowMessage(_message, text), token),
            text => ThemedText.ShowMessage(_message, text));
    }
}
