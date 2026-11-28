using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The line that shows where a dragged item lands. Laid out in a bare adorner decorator with no window, since the adorner
/// layer needs no window; positions are read from the line in the adorned element's own coordinates.
/// </summary>
[Unit]
public class DropLineTests
{
    private readonly Border _adorned = new() { Width = 400, Height = 300 };
    private readonly AdornerDecorator _decorator = new();
    private readonly DropLine _line;

    public DropLineTests()
    {
        _decorator.Child = _adorned;
        _line = new DropLine(_adorned);
        Layout();
    }

    private void Layout()
    {
        // With no window, nothing lays the tree out on its own, and a change doesn't mark its parents dirty: mark every element
        // dirty so a change made since the last layout is measured.
        foreach (var element in Descendants(_decorator).OfType<UIElement>().Prepend(_decorator))
        {
            element.InvalidateMeasure();
            element.InvalidateArrange();
        }
        _decorator.Measure(new Size(400, 300));
        _decorator.Arrange(new Rect(0, 0, 400, 300));
    }

    private IReadOnlyList<Adorner> Adorners => AdornerLayer.GetAdornerLayer(_adorned)?.GetAdorners(_adorned) ?? [];

    private FrameworkElement Line => Descendants(_line).OfType<FrameworkElement>().Single(e => AutomationProperties.GetAutomationId(e) == "DropLine");

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var grandChild in Descendants(child))
            {
                yield return grandChild;
            }
        }
    }

    [WpfFact]
    public void Show_puts_the_line_in_the_adorner_layer_of_the_adorned_element()
    {
        _line.Show(10, 100, 200);

        Assert.Contains(_line, Adorners);
    }

    [WpfFact]
    public void Show_places_the_line_at_the_given_x_y_and_width_in_the_adorned_elements_coordinates()
    {
        _line.Show(10, 100, 200);
        Layout();

        var start = Line.TranslatePoint(new Point(0, Line.ActualHeight / 2), _adorned);
        Assert.InRange(start.X, 9.5, 10.5);
        Assert.InRange(start.Y, 99.5, 100.5);
        Assert.InRange(Line.ActualWidth, 199.5, 200.5);
    }

    [WpfFact]
    public void Showing_again_moves_the_line()
    {
        _line.Show(10, 100, 200);
        Layout();

        _line.Show(30, 50, 120);
        Layout();

        var start = Line.TranslatePoint(new Point(0, Line.ActualHeight / 2), _adorned);
        Assert.InRange(start.X, 29.5, 30.5);
        Assert.InRange(start.Y, 49.5, 50.5);
        Assert.InRange(Line.ActualWidth, 119.5, 120.5);
        Assert.Single(Adorners, _line);
    }

    [WpfFact]
    public void The_line_has_the_automation_id_DropLine()
    {
        Assert.Single(Descendants(_line), e => AutomationProperties.GetAutomationId(e) == "DropLine");
    }

    [WpfFact]
    public void Hide_takes_the_line_away()
    {
        _line.Show(10, 100, 200);

        _line.Hide();

        Assert.DoesNotContain(_line, Adorners);
    }

    [WpfFact]
    public void Rejects_a_missing_adorned_element()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new DropLine(null!));

        Assert.Equal("adorned", error.ParamName);
    }
}
