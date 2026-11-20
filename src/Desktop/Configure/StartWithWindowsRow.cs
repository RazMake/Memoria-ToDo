using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Start with Windows setting as the window shows it: a check box that switches it at once, and its error.</summary>
internal sealed class StartWithWindowsRow : StackPanel
{
    private readonly StartWithWindowsSetting _setting;
    private readonly CheckBox _box = new() { Content = "Start with Windows" };
    private readonly TextBlock _problem = ThemedText.Message();
    private bool _reverting;

    public StartWithWindowsRow(IStartWithWindows startWithWindows, ILogger logger)
    {
        _setting = new StartWithWindowsSetting(startWithWindows, logger);
        _box.IsChecked = _setting.IsOn;
        _box.Checked += (_, _) => Switch(true);
        _box.Unchecked += (_, _) => Switch(false);

        Children.Add(_box);
        Children.Add(ThemedText.Help(
            "Starts the app in the notification area when you sign in to Windows.", new Thickness(28, 2, 0, 0)));
        Children.Add(_problem);
    }

    private void Switch(bool on)
    {
        if (_reverting)
        {
            return;
        }

        _setting.Set(on);
        ThemedText.ShowMessage(_problem, _setting.Problem);
        if (_setting.IsOn != on)
        {
            _reverting = true;
            _box.IsChecked = _setting.IsOn;
            _reverting = false;
        }
    }
}
