using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The line that shows where a dragged task or group lands: thin, in the theme's accent color, with a small circle at its
/// start. It is drawn in the adorner layer above the list, so it takes no room and nothing moves while it shows. The line itself
/// has the automation id "DropLine".
/// </summary>
internal sealed class DropLine : Adorner
{
    private const double Thickness = 2;
    private const double Dot = 6;

    private readonly Grid _mark = new() { Height = Dot, IsHitTestVisible = false };
    private double _x;
    private double _y;
    private double _width;
    private bool _shown;

    public DropLine(UIElement adorned) : base(adorned)
    {
        IsHitTestVisible = false;
        var line = new Rectangle { Height = Thickness, VerticalAlignment = VerticalAlignment.Center };
        line.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        AutomationProperties.SetAutomationId(line, "DropLine");
        var dot = new Ellipse { Width = Dot, Height = Dot, HorizontalAlignment = HorizontalAlignment.Left };
        dot.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        _mark.Children.Add(line);
        _mark.Children.Add(dot);
        AddVisualChild(_mark);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0 ? _mark : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Shows the line with its middle at <paramref name="y"/>, from <paramref name="x"/>, <paramref name="width"/> wide, in the list's coordinates.</summary>
    public void Show(double x, double y, double width)
    {
        _x = x;
        _y = y;
        _width = Math.Max(width, 0);
        if (!_shown && AdornerLayer.GetAdornerLayer(AdornedElement) is { } layer)
        {
            layer.Add(this);
            _shown = true;
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>Takes the line away.</summary>
    public void Hide()
    {
        if (_shown && AdornerLayer.GetAdornerLayer(AdornedElement) is { } layer)
        {
            layer.Remove(this);
        }

        _shown = false;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _mark.Measure(new Size(_width, Dot));
        return new Size(AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _mark.Arrange(new Rect(_x, _y - Dot / 2, _width, Dot));
        return finalSize;
    }
}
