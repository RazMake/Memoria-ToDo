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
    private readonly DeviceActions _actions;
    private readonly TextBox _name = new() { Margin = new Thickness(0, 0, 0, 5) };
    private readonly TextBlock _hostKey = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _accept = new() { Content = "Accept host key", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _forget = new() { Content = "Forget host key", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _message = ThemedText.Message();

    public DeviceSection(SyncUi ui)
    {
        _ui = ui;
        _actions = new DeviceActions(ui, ShowMessage);
        AutomationProperties.SetName(_name, "Device name");
        _name.LostFocus += async (_, _) => await SetNameAsync();
        _accept.Click += async (_, _) => await _actions.AcceptHostKeyAsync();
        _forget.Click += async (_, _) => await _actions.ForgetHostKeyAsync();

        Children.Add(new TextBlock { Text = "Device name", Margin = new Thickness(0, 12, 0, 4) });
        Children.Add(_name);
        Children.Add(Key("Generate key", () => _ = _actions.GenerateKeyAsync()));
        Children.Add(Key("Copy public key", _actions.CopyPublicKey));
        Children.Add(Key("Add key at host", _actions.OpenKeyPage));
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
            _hostKey.Text = SyncText.HostKey(host);
            _accept.Visibility = host.CanAccept ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static Button Key(string content, Action click)
    {
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 5) };
        button.Click += (_, _) => click();
        return button;
    }

    private void ShowMessage() => ThemedText.ShowMessage(_message, _actions.Message);

    private async Task SetNameAsync()
    {
        await _actions.SetNameAsync(_name.Text);
        _name.Text = _ui.Sync.Setup.State.DeviceName;
    }
}
