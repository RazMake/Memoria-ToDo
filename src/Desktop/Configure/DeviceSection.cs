using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Git Access card's controls for this device: its name, its key (generate, copy and add at the host), and the host key
/// the device trusts or waits to trust (accept).
/// </summary>
internal sealed class DeviceSection : StackPanel
{
    private const string ForgetKeyText =
        "Delete this device's SSH key? Sync stops until you generate a new key and add it at your repository's host. "
        + "The old key stays listed at the host until you remove it there.";

    private readonly SyncUi _ui;
    private readonly DeviceActions _actions;
    private readonly TextBox _name = new();
    private readonly Button _generate;
    private readonly Button _copy;
    private readonly Button _forgetKey;
    private readonly Button _accept = ConfigureButton.Make("Accept host key", new Thickness(0, 6, 6, 0));
    private readonly TextBlock _message = ThemedText.Message();

    public DeviceSection(SyncUi ui)
    {
        _ui = ui;
        _actions = new DeviceActions(ui, ShowMessage);
        AutomationProperties.SetName(_name, "Device name");
        _name.LostFocus += async (_, _) => await SetNameAsync();
        _accept.Click += async (_, _) => await _actions.AcceptHostKeyAsync();

        _generate = KeyButton("Generate key", "Makes this device's SSH key, then copies its public key.", () => _ = _actions.GenerateKeyAsync());
        _copy = KeyButton("Copy public key", "Copies this device's public key.", _actions.CopyPublicKey);
        _forgetKey = KeyButton("Forget key", "Deletes this device's SSH key, after you confirm.", () => _ = ForgetKeyAsync());
        var keys = new StackPanel { Orientation = Orientation.Horizontal };
        keys.Children.Add(_generate);
        keys.Children.Add(_copy);
        keys.Children.Add(_forgetKey);
        var hostButtons = new StackPanel { Orientation = Orientation.Horizontal };
        hostButtons.Children.Add(_accept);

        Children.Add(Rows(("Device name", _name), ("SSH key", keys)));
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
        _accept.Visibility = host is { CanAccept: true } ? Visibility.Visible : Visibility.Collapsed;

        var hasKey = state.PublicKey is not null;
        _generate.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
        _copy.Visibility = _forgetKey.Visibility = hasKey ? Visibility.Visible : Visibility.Collapsed;
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

    private static Button KeyButton(string content, string tip, Action click)
    {
        var button = ConfigureButton.Make(content, new Thickness(0, 0, 6, 0));
        button.ToolTip = tip;
        button.Click += (_, _) => click();
        return button;
    }

    private void ShowMessage() => ThemedText.ShowMessage(_message, _actions.Message);

    private async Task SetNameAsync()
    {
        await _actions.SetNameAsync(_name.Text);
        _name.Text = _ui.Sync.Setup.State.DeviceName;
    }

    private async Task ForgetKeyAsync()
    {
        var forget = QuestionDialog.Ask(
            Window.GetWindow(this)!, "Forget key", ForgetKeyText,
            [("Forget key", true), ("Cancel", false)], false, false, _ui.Logger);
        if (forget)
        {
            await _actions.ForgetKeyAsync();
        }
    }
}
