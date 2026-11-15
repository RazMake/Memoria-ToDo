using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Sync settings status: the last sync, pending changes, skipped files, and a Sync now button.</summary>
internal sealed class StatusSection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBlock _message = ThemedText.Message();

    public StatusSection(SyncUi ui)
    {
        _ui = ui;
        var syncNow = new Button { Content = "Sync now", HorizontalAlignment = HorizontalAlignment.Left };
        syncNow.Click += async (_, _) => await SyncNowAsync();
        Children.Add(_text);
        Children.Add(syncNow);
        Children.Add(_message);
        Refresh();
    }

    /// <summary>Shows the sync status the service holds now.</summary>
    public void Refresh() => _text.Text = SyncText.Status(_ui.Sync.Status);

    private async Task SyncNowAsync()
    {
        ThemedText.ShowMessage(_message, null);
        await _ui.RunAsync(
            token => _ui.SyncNowAsync(text => ThemedText.ShowMessage(_message, text), token),
            text => ThemedText.ShowMessage(_message, text));
    }
}
