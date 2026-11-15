using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Git Access card's controls for this device: its name, its key (generate, copy and add at the host), and the host key
/// the device trusts or waits to trust (accept, forget).
/// </summary>
internal sealed class DeviceSection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly DeviceActions _actions;
    private readonly TextBox _name = new();
    private readonly TextBlock _hostKey = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _accept = ConfigureButton.Make("Accept host key", new Thickness(0, 6, 6, 0));
    private readonly Button _forget = ConfigureButton.Make("Forget host key", new Thickness(0, 6, 0, 0));
    private readonly TextBlock _message = ThemedText.Message();

    public DeviceSection(SyncUi ui)
    {
        _ui = ui;
        _actions = new DeviceActions(ui, ShowMessage);
        AutomationProperties.SetName(_name, "Device name");
        _name.LostFocus += async (_, _) => await SetNameAsync();
        _accept.Click += async (_, _) => await _actions.AcceptHostKeyAsync();
        _forget.Click += async (_, _) => await _actions.ForgetHostKeyAsync();

        var generate = KeyButton("Generate key", () => _ = _actions.GenerateKeyAsync());
        var copy = KeyButton("Copy public key", _actions.CopyPublicKey);
        copy.ToolTip = "Copies the public key, then opens the repository host's page where you add this device's SSH key.";
        var keys = new StackPanel { Orientation = Orientation.Horizontal };
        keys.Children.Add(generate);
        keys.Children.Add(copy);
        var hostButtons = new StackPanel { Orientation = Orientation.Horizontal };
        hostButtons.Children.Add(_accept);
        hostButtons.Children.Add(_forget);

        Children.Add(Rows(("Device name", _name), ("SSH key", keys)));
        Children.Add(_hostKey);
        Children.Add(hostButtons);
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

    // A grid of label and control rows; the label column is as wide as the longest label.
    private static Grid Rows(params (string Label, UIElement Control)[] rows)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() },
        };
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new Thickness(0, i == 0 ? 0 : 6, 0, 0);
            var label = new TextBlock { Text = rows[i].Label, Margin = new Thickness(0, top.Top, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var control = (FrameworkElement)rows[i].Control;
            control.Margin = new Thickness(control.Margin.Left, top.Top, control.Margin.Right, control.Margin.Bottom);
            Grid.SetRow(label, i);
            Grid.SetRow(control, i);
            Grid.SetColumn(control, 1);
            grid.Children.Add(label);
            grid.Children.Add(control);
        }

        return grid;
    }

    private static Button KeyButton(string content, Action click)
    {
        var button = ConfigureButton.Make(content, new Thickness(0, 0, 6, 0));
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
