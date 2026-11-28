using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The bar above the list: the time range, the minimum level, the components, and Refresh. A change the user makes goes to the
/// model, then raises <see cref="Changed"/>; <see cref="Show"/> brings the controls to what the model holds.
/// </summary>
internal sealed class LogFilterBar : Border
{
    private readonly LogsModel _model;
    private readonly ComboBox _preset = new() { MinWidth = 110 };
    private readonly DateTimeField _from = new("From", "00:00");
    private readonly DateTimeField _to = new("To", "23:59");
    private readonly TextBlock _fromLabel = new() { Text = "From:" };
    private readonly TextBlock _toLabel = new() { Text = "To:" };
    private readonly ComboBox _level = new() { MinWidth = 130 };
    private readonly MenuItem _components = new() { Header = "Components" };
    private IReadOnlyList<ComponentChoice>? _shownComponents;
    private bool _showing;

    public LogFilterBar(LogsModel model)
    {
        _model = model;
        Describe(_preset, "Time range");
        Describe(_level, "Minimum severity");
        foreach (var preset in Enum.GetValues<TimePreset>())
        {
            _preset.Items.Add(TimePresets.Label(preset));
        }

        foreach (var choice in model.Levels)
        {
            _level.Items.Add(choice.Label);
        }

        var refresh = new Button { Content = "Refresh", ToolTip = "Read the log again (F5)", Padding = new Thickness(12, 2, 12, 2) };
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        var bar = new WrapPanel { Orientation = Orientation.Horizontal };
        Add(bar, new TextBlock { Text = "Time:" });
        Add(bar, _preset);
        Add(bar, _fromLabel);
        Add(bar, _from);
        Add(bar, _toLabel);
        Add(bar, _to);
        Add(bar, _level);
        Add(bar, new Menu { Background = System.Windows.Media.Brushes.Transparent, Items = { _components } });
        Add(bar, refresh);
        Padding = new Thickness(8, 4, 8, 4);
        Child = bar;

        _preset.SelectionChanged += (_, _) => Apply(() => _model.Preset = Enum.GetValues<TimePreset>()[_preset.SelectedIndex]);
        _from.Changed += (_, _) => Apply(() => _model.From = _from.Text);
        _to.Changed += (_, _) => Apply(() => _model.To = _to.Text);
        _level.SelectionChanged += (_, _) => Apply(() => _model.MinimumLevel = _model.Levels[_level.SelectedIndex].Level);
    }

    /// <summary>Raised after a change of the user went to the model.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the user asks to read the log again.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Shows what the model holds: the preset, the boxes and which are red, the level, and the components.</summary>
    public void Show()
    {
        _showing = true;
        try
        {
            _preset.SelectedIndex = (int)_model.Preset;
            _from.Show(_model.From, _model.FromIsInvalid);
            _to.Show(_model.To, _model.ToIsInvalid);
            // All needs no bound, the other presets only a start; Custom shows both.
            ShowIf(_model.Preset != TimePreset.All, _fromLabel, _from);
            ShowIf(_model.Preset == TimePreset.Custom, _toLabel, _to);
            _level.SelectedIndex = _model.Levels.ToList().FindIndex(choice => choice.Level == _model.MinimumLevel);
            ShowComponents();
        }
        finally
        {
            _showing = false;
        }
    }

    private static void Describe(DependencyObject control, string name) => AutomationProperties.SetName(control, name);

    private static void ShowIf(bool shown, params ReadOnlySpan<UIElement> controls)
    {
        foreach (var control in controls)
        {
            control.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void Add(Panel bar, FrameworkElement control)
    {
        control.Margin = new Thickness(0, 2, 8, 2);
        control.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(control);
    }

    private void Apply(Action change)
    {
        if (!_showing)
        {
            change();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // The items are made again only when a load gave new components, so a menu the user holds open keeps open as boxes are checked.
    private void ShowComponents()
    {
        if (!ReferenceEquals(_shownComponents, _model.Components))
        {
            _shownComponents = _model.Components;
            _components.Items.Clear();
            _components.Items.Add(CheckItem("All", () => _model.AllComponentsChecked, value => _model.AllComponentsChecked = value));
            _components.Items.Add(new Separator());
            foreach (var choice in _model.Components)
            {
                _components.Items.Add(CheckItem(choice.Name, () => choice.IsChecked, value => choice.IsChecked = value));
            }
        }

        foreach (var item in _components.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is Func<bool> isChecked && isChecked();
        }
    }

    private MenuItem CheckItem(string header, Func<bool> isChecked, Action<bool> setChecked)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, StaysOpenOnClick = true, Tag = isChecked };
        item.Click += (_, _) => Apply(() => setChecked(item.IsChecked));
        return item;
    }
}
