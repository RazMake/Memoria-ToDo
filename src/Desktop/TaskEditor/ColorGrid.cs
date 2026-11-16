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
/// marked. Arrow keys move between them, and a click, Space, or Enter on a color reports it.
/// </summary>
internal sealed class ColorGrid : Grid
{
    private const int Rows = 3;

    private readonly TagLook _look;
    private readonly List<ToggleButton> _buttons = [];

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

    /// <summary>Raised with the name of the color the user chose.</summary>
    public event Action<string>? Chosen;

    /// <summary>Puts the cursor in the first color.</summary>
    public void FocusFirst() => KeyboardFocus.MoveTo(_buttons[0]);

    /// <summary>Marks the color, or none.</summary>
    public void Show(string? marked)
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = TagColors.All[i].Name == marked;
            Outline(_buttons[i]);
        }
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
        button.Click += (_, _) => Chosen?.Invoke(color.Name);
        return button;
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

        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            Chosen?.Invoke(color.Name);
            return;
        }

        var at = _buttons.IndexOf(button);
        var target = e.Key switch
        {
            Key.Left => at - Rows,
            Key.Right => at + Rows,
            Key.Up => at % Rows > 0 ? at - 1 : at,
            Key.Down => at % Rows < Rows - 1 ? at + 1 : at,
            _ => -1,
        };
        if (target == -1)
        {
            return;
        }

        e.Handled = true;
        if (target >= 0 && target < _buttons.Count)
        {
            KeyboardFocus.MoveTo(_buttons[target]);
        }
    }
}
