using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Sync settings controls for this device: its name, its key (generate, copy, add at the host), and the host key
/// the device trusts or waits to trust (accept, forget).
/// </summary>
internal sealed class DeviceSection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly TextBox _name = new() { Margin = new Thickness(0, 0, 0, 5) };
    private readonly TextBlock _hostKey = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _accept = new() { Content = "Accept host key", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _forget = new() { Content = "Forget host key", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _message = ThemedText.Message();

    public DeviceSection(SyncUi ui)
    {
        _ui = ui;
        AutomationProperties.SetName(_name, "Device name");
        _name.LostFocus += async (_, _) => await SetNameAsync();
        _accept.Click += async (_, _) => await AcceptAsync();
        _forget.Click += async (_, _) => await ForgetAsync();

        Children.Add(new TextBlock { Text = "Device name", Margin = new Thickness(0, 12, 0, 4) });
        Children.Add(_name);
        Children.Add(Key("Generate key", () => Run(token => _ui.Sync.Setup.GenerateKeyAsync(token))));
        Children.Add(Key("Copy public key", CopyKey));
        Children.Add(Key("Add key at host", OpenKeyPage));
        Children.Add(_hostKey);
        Children.Add(_accept);
        Children.Add(_forget);
        Children.Add(_message);
        Refresh();
    }

    /// <summary>Shows the device name and host key the setup holds now, unless the name box has the focus.</summary>
    public void Refresh()
    {
        var state = _ui.Sync.Setup.State;
        if (!_name.IsKeyboardFocusWithin)
        {
            _name.Text = state.DeviceName;
        }

        var host = state.HostKey;
        _hostKey.Visibility = _accept.Visibility = _forget.Visibility = host is null ? Visibility.Collapsed : Visibility.Visible;
        if (host is not null)
        {
            _hostKey.Text = $"Host {host.Host}:{host.Port} key {host.Algorithm} {host.Fingerprint}";
            _accept.Visibility = host.CanAccept ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private Button Key(string content, Action click)
    {
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 5) };
        button.Click += (_, _) => click();
        return button;
    }

    private void Run(Func<CancellationToken, Task> operation) =>
        _ = _ui.RunAsync(operation, text => ThemedText.ShowMessage(_message, text));

    private void CopyKey()
    {
        var key = _ui.Sync.Setup.State.PublicKey;
        Report(key is null ? "Generate a key first." : null);
        if (key is not null)
        {
            Try(() => _ui.Actions.CopyPublicKey(key));
        }
    }

    private void OpenKeyPage()
    {
        var page = _ui.Sync.Setup.State.AddKeyPage;
        Report(page is null ? "The repository's host has no page to add a key." : null);
        if (page is not null)
        {
            Try(() => _ui.Actions.OpenKeyPage(page));
        }
    }

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Report(error.Message);
        }
    }

    private void Report(string? text) => ThemedText.ShowMessage(_message, text);

    private async Task SetNameAsync()
    {
        var name = _name.Text.Trim();
        if (name == _ui.Sync.Setup.State.DeviceName)
        {
            return;
        }

        Report(null);
        await _ui.RunAsync(token => _ui.Sync.Setup.SetDeviceNameAsync(name, token), Report);
        _name.Text = _ui.Sync.Setup.State.DeviceName;
    }

    private async Task AcceptAsync()
    {
        if (_ui.Sync.Setup.State.HostKey is { CanAccept: true } host)
        {
            await _ui.RunAsync(token => _ui.Sync.Setup.AcceptHostKeyAsync(host.Fingerprint, token), Report);
        }
    }

    private async Task ForgetAsync()
    {
        if (_ui.Sync.Setup.State.HostKey is { } host)
        {
            await _ui.RunAsync(token => _ui.Sync.Setup.ForgetHostKeyAsync(host.Host, host.Port, token), Report);
        }
    }
}
