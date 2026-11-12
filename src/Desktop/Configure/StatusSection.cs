using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Sync settings status: the last sync, pending changes, skipped files, and a Sync now button.</summary>
internal sealed class StatusSection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 5) };
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
    public void Refresh()
    {
        var status = _ui.Sync.Status;
        var lines = new List<string>
        {
            status.LastSuccessfulSync is { } at
                ? $"Last successful sync: {at.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}"
                : "Not synced yet.",
        };
        if (status.HasPendingChanges)
        {
            lines.Add("Changes wait to be synced.");
        }

        if (status.State is SyncState.Error or SyncState.Offline && status.Details is not null)
        {
            lines.Add(status.Details);
        }

        lines.AddRange(status.SkippedFiles.Select(file => $"Skipped {file.RelativePath}: {file.Reason}"));
        _text.Text = string.Join(Environment.NewLine, lines);
    }

    private async Task SyncNowAsync()
    {
        ThemedText.ShowMessage(_message, null);
        await _ui.RunAsync(
            async token =>
            {
                var status = await _ui.Sync.SyncNowAsync(token);
                if (status.State is SyncState.Error or SyncState.Offline)
                {
                    ThemedText.ShowMessage(_message, status.Details);
                }
            },
            text => ThemedText.ShowMessage(_message, text));
    }
}
