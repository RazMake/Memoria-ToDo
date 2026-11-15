using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The 30 tag colors as a grid of 10 columns by 3 rows, a hue to a column and its shades down it, of which at most one is
/// picked. Arrow keys move between them, and a click or Space picks an unpicked color and unpicks the picked one.
/// </summary>
internal sealed class ColorGrid : Grid
{
    private const int Rows = 3;

    private readonly TagLook _look;
    private readonly List<ToggleButton> _buttons = [];
    private bool _showing;

    public ColorGrid(TagLook look)
    {
        _look = look;
        AutomationProperties.SetAutomationId(this, "TagColorGrid");
        for (var column = 0; column < TagColors.All.Count / Rows; column++)
        {
            ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < Rows; row++)
        {
            RowDefinitions.Add(new RowDefinition());
        }

        for (var i = 0; i < TagColors.All.Count; i++)
        {
            var button = Swatch(TagColors.All[i]);
            SetColumn(button, i / Rows);
            SetRow(button, i % Rows);
            Children.Add(button);
            _buttons.Add(button);
        }

        PreviewKeyDown += OnKeyDown;
        look.Follow(this, Repaint);
    }

    /// <summary>Raised with the name of the color the user picked, or null when they unpicked it.</summary>
    public event Action<string?>? Picked;

    /// <summary>Shows which color is picked, or none.</summary>
    public void Show(string? picked)
    {
        _showing = true;
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = TagColors.All[i].Name == picked;
            Outline(_buttons[i]);
        }

        _showing = false;
    }

    private ToggleButton Swatch(TagColor color)
    {
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        frame.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        frame.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var button = new ToggleButton
        {
            Template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = frame },
            Width = 24,
            Height = 24,
            Margin = new Thickness(2),
            Tag = color,
            ToolTip = color.Name,
        };
        AutomationProperties.SetAutomationId(button, $"Color {color.Name}");
        AutomationProperties.SetName(button, color.Name);
        Outline(button);
        button.Checked += (_, _) => OnToggled(color.Name);
        button.Unchecked += (_, _) => OnToggled(null);
        return button;
    }

    private void OnToggled(string? name)
    {
        if (!_showing)
        {
            Picked?.Invoke(name);
        }
    }

    private void Repaint()
    {
        foreach (var button in _buttons)
        {
            Outline(button);
        }
    }

    private void Outline(ToggleButton button)
    {
        button.Background = TagLook.Paint(_look.FillOf((TagColor)button.Tag).Background);
        button.BorderThickness = new Thickness(2);
        button.BorderBrush = button.IsChecked == true ? SystemColors.WindowTextBrush : Brushes.Transparent;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ToggleButton { Tag: TagColor color } button)
        {
            return;
        }

        var at = _buttons.IndexOf(button);
        var target = e.Key switch
        {
            Key.Left => at - Rows,
            Key.Right => at + Rows,
            Key.Up => at % Rows > 0 ? at - 1 : at,
            Key.Down => at % Rows < Rows - 1 ? at + 1 : at,
            Key.Space => -2,
            _ => -1,
        };
        if (target == -1)
        {
            return;
        }

        e.Handled = true;
        if (target == -2)
        {
            Picked?.Invoke(button.IsChecked == true ? null : color.Name);
        }
        else if (target >= 0 && target < _buttons.Count)
        {
            KeyboardFocus.MoveTo(_buttons[target]);
        }
    }
}
