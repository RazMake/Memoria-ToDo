using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The Sync button and the line beside it for one Task View window. It shows what <see cref="SyncRequests"/> says: that a
/// pass is running, and why a pass it requested failed.
/// </summary>
internal sealed class ListSync : StackPanel, IDisposable
{
    private readonly SyncRequests _requests;
    private readonly Button _button = new() { Content = "Sync", Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _failure = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    public ListSync(ISyncService service)
    {
        _requests = new SyncRequests(service, post => Dispatcher.InvokeAsync(post), Show);
        _failure.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        AutomationProperties.SetName(_button, "Sync");
        _button.Click += async (_, _) => await SyncAsync();
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0);
        Children.Add(_button);
        Children.Add(_failure);
    }

    /// <inheritdoc cref="SyncRequests.SyncAsync"/>
    public Task SyncAsync() => _requests.SyncAsync();

    public void Dispose() => _requests.Dispose();

    private void Show()
    {
        _failure.Text = _requests.FailureText;
        _button.Content = _requests.IsWorking ? "Syncing…" : "Sync";
    }
}
