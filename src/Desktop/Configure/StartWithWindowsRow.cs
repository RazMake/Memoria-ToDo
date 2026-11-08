using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Start with Windows setting as the window shows it: a check box that switches it at once, and its error.</summary>
internal sealed class StartWithWindowsRow : StackPanel
{
    private readonly IStartWithWindows _startWithWindows;
    private readonly CheckBox _box = new() { Content = "Start with Windows" };
    private readonly TextBlock _problem = ThemedText.Message();
    private bool _reverting;

    public StartWithWindowsRow(IStartWithWindows startWithWindows)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        _startWithWindows = startWithWindows;
        _box.IsChecked = startWithWindows.IsOn();
        _box.Checked += (_, _) => Switch(true);
        _box.Unchecked += (_, _) => Switch(false);

        Children.Add(_box);
        Children.Add(_problem);
    }

    private void Switch(bool on)
    {
        if (_reverting)
        {
            return;
        }

        try
        {
            _startWithWindows.Set(on);
            _problem.Text = "";
        }
        catch (Exception error)
        {
            _problem.Text = error.Message;
            _reverting = true;
            _box.IsChecked = !on;
            _reverting = false;
        }
    }
}
