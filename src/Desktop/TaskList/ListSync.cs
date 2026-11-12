using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The Sync button and the line beside it for one View Tasks window. It requests passes from the shared service, shows
/// that one is running, and shows why a pass it requested failed. It never stops the shared pass: closing the window
/// cancels only this window's wait.
/// </summary>
internal sealed class ListSync : StackPanel, IDisposable
{
    private readonly ISyncService _service;
    private readonly Button _button = new() { Content = "Sync", Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _failure = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private CancellationTokenSource? _wait;
    private bool _closed;

    public ListSync(ISyncService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _failure.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        AutomationProperties.SetName(_button, "Sync");
        _button.Click += async (_, _) => await SyncAsync();
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(8, 8, 8, 0);
        Children.Add(_button);
        Children.Add(_failure);
        _service.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Requests a pass and waits for it, or does nothing while a pass this window requested is still running.
    /// A failure is shown, never thrown, so callers may await or observe it freely.
    /// </summary>
    public async Task SyncAsync()
    {
        if (_closed || _wait is not null)
        {
            return;
        }

        using var wait = new CancellationTokenSource();
        _wait = wait;
        _failure.Text = "";
        ShowWorking();
        try
        {
            var status = await _service.SyncNowAsync(wait.Token);
            if (!_closed && status.State is SyncState.Error or SyncState.Offline)
            {
                _failure.Text = status.Details ?? $"Sync failed: {status.Failure?.ToString() ?? status.State.ToString()}.";
            }
        }
        catch (OperationCanceledException) when (wait.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
            // The service stopped while the app shuts down; nothing is left to sync and nothing to report.
        }
        catch (Exception failure)
        {
            if (!_closed)
            {
                _failure.Text = failure.Message;
            }
        }
        finally
        {
            _wait = null;
            if (!_closed)
            {
                ShowWorking();
            }
        }
    }

    public void Dispose()
    {
        _closed = true;
        _service.StatusChanged -= OnStatusChanged;
        _wait?.Cancel();
    }

    // Raised on any thread; the button is touched only on the UI thread, and never after the window closed.
    private void OnStatusChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (!_closed)
            {
                ShowWorking();
            }
        });

    private void ShowWorking() =>
        _button.Content = _wait is not null || _service.Status.State == SyncState.Working ? "Syncing…" : "Sync";
}
