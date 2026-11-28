using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The two small headers, "Edit" and "Preview", over an edited description. The selected one is underlined in the theme's accent;
/// they take no keyboard focus, so Tab skips them and they draw no focus rectangle.
/// </summary>
internal sealed class EditTabs : StackPanel
{
    private readonly Button _edit;
    private readonly Button _preview;

    public EditTabs()
    {
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 0, 0, 8);
        _edit = Header("Edit", preview: false);
        _preview = Header("Preview", preview: true);
        Children.Add(_edit);
        Children.Add(_preview);
        Show(preview: false);
    }

    /// <summary>Raised when the user clicks a header, with true for Preview and false for Edit.</summary>
    public event EventHandler<bool>? Chosen;

    /// <summary>Marks the Preview header selected, or the Edit header.</summary>
    public void Show(bool preview)
    {
        Mark(_edit, !preview);
        Mark(_preview, preview);
    }

    private Button Header(string label, bool preview)
    {
        var header = new Button
        {
            Content = label,
            Template = Flat(),
            Focusable = false,
            FocusVisualStyle = null,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        AutomationProperties.SetName(header, label);
        header.Click += (_, _) => Chosen?.Invoke(this, preview);
        return header;
    }

    private static void Mark(Button header, bool selected)
    {
        header.SetResourceReference(Control.ForegroundProperty, selected ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        if (selected)
        {
            header.SetResourceReference(Control.BorderBrushProperty, "AccentFillColorDefaultBrush");
        }
        else
        {
            header.BorderBrush = Brushes.Transparent;
        }

        AutomationProperties.SetItemStatus(header, selected ? "Selected" : "");
    }

    // The theme's button chrome is too heavy for a tab header: just the label over a line under it.
    private static ControlTemplate Flat()
    {
        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
        line.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        line.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        line.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        line.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return new ControlTemplate(typeof(Button)) { VisualTree = line };
    }
}
