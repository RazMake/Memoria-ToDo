using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The deletion delay, in days, kept in the shared settings and saved when the box loses the focus.</summary>
internal sealed class DeletionDelayRow : StackPanel
{
    private readonly ISharedTaskSettings _settings;
    private readonly TextBox _days = new() { Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _message = ThemedText.Message();
    private string _shown = "";
    private bool _readFailed;

    public DeletionDelayRow(ISharedTaskSettings settings)
    {
        _settings = settings;
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

        try
        {
            _shown = _settings.DeletionDelayDays.ToString(CultureInfo.InvariantCulture);
            if (_readFailed)
            {
                _readFailed = false;
                ThemedText.ShowMessage(_message, null);
            }
        }
        catch (Exception error)
        {
            _readFailed = true;
            ThemedText.ShowMessage(_message, $"The deletion delay couldn't be read: {error.Message}");
        }

        _days.Text = _shown;
    }

    private void Commit()
    {
        var text = _days.Text.Trim();
        string? problem = null;
        try
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var days))
            {
                problem = $"The deletion delay wasn't changed: \"{text}\" isn't a whole number of days.";
            }
            else
            {
                _settings.SetDeletionDelayDays(days);
                _shown = days.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            problem = $"The deletion delay wasn't changed: {text} is outside the allowed range.";
        }
        catch (Exception error)
        {
            problem = $"The deletion delay wasn't saved: {error.Message}";
        }

        ThemedText.ShowMessage(_message, problem);
        if (problem is not null)
        {
            _days.Text = _shown;
        }
    }
}
