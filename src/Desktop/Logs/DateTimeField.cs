using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// A date picker and a time box that together edit one bound's text, in the layout <see cref="TimeRange"/> reads. No date is
/// no bound, and the time box is off until a date is picked; picking a date while the time is empty fills in the default time.
/// The time box lists every half hour and takes any time typed.
/// </summary>
internal sealed class DateTimeField : StackPanel
{
    private const string DateLayout = "yyyy-MM-dd";

    private readonly DatePicker _date = new() { Width = 140 };
    private readonly ComboBox _time = new() { IsEditable = true, Width = 100, Margin = new Thickness(4, 0, 0, 0) };
    private readonly string _defaultTime;
    private bool _setting;

    public DateTimeField(string name, string defaultTime)
    {
        _defaultTime = defaultTime;
        Orientation = Orientation.Horizontal;
        AutomationProperties.SetName(_date, $"{name} date");
        AutomationProperties.SetName(_time, $"{name} time");
        _time.ToolTip = "A time such as 14:30.";
        for (var minutes = 0; minutes < 24 * 60; minutes += 30)
        {
            _time.Items.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)).ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        _date.SelectedDateChanged += (_, _) =>
        {
            if (!_setting && _date.SelectedDate is not null && _time.Text.Length == 0)
            {
                SetTime(_defaultTime);
            }

            Edited();
        };
        _time.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Edited()));
        Children.Add(_date);
        Children.Add(_time);
        UpdateTimeEnabled();
    }

    /// <summary>Raised when the user changes the date or the time.</summary>
    public event EventHandler? Changed;

    /// <summary>The bound's text: empty without a date, else the date and the time box's text.</summary>
    public string Text => _date.SelectedDate is { } date ? $"{date.ToString(DateLayout, CultureInfo.InvariantCulture)} {_time.Text.Trim()}" : "";

    /// <summary>Shows the text, leaving each control the user edits alone while it already shows its part.</summary>
    public void Show(string text, bool isInvalid)
    {
        _setting = true;
        try
        {
            if (text != Text)
            {
                var split = text.IndexOf(' ', StringComparison.Ordinal);
                var datePart = split < 0 ? text : text[..split];
                var date = DateTime.TryParseExact(datePart, DateLayout, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : (DateTime?)null;
                if (_date.SelectedDate != date)
                {
                    _date.SelectedDate = date;
                }

                SetTime(date is null || split < 0 ? "" : text[(split + 1)..].Trim());
            }

            UpdateTimeEnabled();
            MarkInvalid(_date, isInvalid);
            MarkInvalid(_time, isInvalid);
        }
        finally
        {
            _setting = false;
        }
    }

    private static void MarkInvalid(Control control, bool isInvalid)
    {
        if (isInvalid)
        {
            control.SetResourceReference(Control.BorderBrushProperty, "SystemFillColorCriticalBrush");
        }
        else
        {
            control.ClearValue(Control.BorderBrushProperty);
        }
    }

    private void SetTime(string time)
    {
        if (_time.Text != time)
        {
            var wasSetting = _setting;
            _setting = true;
            _time.Text = time;
            _setting = wasSetting;
        }
    }

    private void UpdateTimeEnabled() => _time.IsEnabled = _date.SelectedDate is not null;

    private void Edited()
    {
        UpdateTimeEnabled();
        if (!_setting)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
