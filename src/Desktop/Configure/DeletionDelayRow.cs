using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The deletion delay, in days, kept in the shared settings and saved when the box loses the focus.</summary>
internal sealed class DeletionDelayRow : StackPanel
{
    private readonly DeletionDelaySetting _setting;
    private readonly TextBox _days = new() { Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _message = ThemedText.Message();

    public DeletionDelayRow(ISharedTaskSettings settings)
    {
        _setting = new DeletionDelaySetting(settings);
        AutomationProperties.SetName(_days, "Deletion delay (days)");
        _days.LostFocus += (_, _) => Commit();
        Children.Add(new TextBlock { Text = "Deletion delay (days)", Margin = new Thickness(0, 12, 0, 4) });
        Children.Add(_days);
        Children.Add(_message);
        Refresh();
    }

    /// <summary>
    /// Shows the saved value, unless the box has the focus. When the settings can't be read, the box keeps the last
    /// value it showed (empty if none) and the message says why.
    /// </summary>
    public void Refresh()
    {
        if (_days.IsKeyboardFocusWithin)
        {
            return;
        }

        _setting.Refresh();
        _days.Text = _setting.Shown;
        ThemedText.ShowMessage(_message, _setting.Message);
    }

    private void Commit()
    {
        _setting.Commit(_days.Text);
        ThemedText.ShowMessage(_message, _setting.Message);
        if (_setting.Message is not null)
        {
            _days.Text = _setting.Shown;
        }
    }
}
