using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Two sides with a draggable bar between them, laid out in a bare container with no window or theme, so the numbers are the
/// panes' own. The bar's color comes from a theme resource, which the tests put on the panes themselves.
/// </summary>
[Unit]
public class SplitPanesTests
{
    private const string BarKey = "CardStrokeColorDefaultBrush";
    private const double BarWidth = 6;

    [WpfFact]
    public void Lays_the_sides_out_in_the_given_share_ratio()
    {
        var host = new Host(totalWidth: 800 + BarWidth, leftShare: 1, leftMinWidth: 0, rightShare: 3, rightMinWidth: 0);

        Assert.InRange(host.Left.ActualWidth, 199.5, 200.5);
        Assert.InRange(host.Right.ActualWidth, 599.5, 600.5);
    }

    [WpfFact]
    public void The_left_side_keeps_at_least_its_minimum_width()
    {
        var host = new Host(totalWidth: 1000 + BarWidth, leftShare: 1, leftMinWidth: 300, rightShare: 9, rightMinWidth: 0);

        Assert.True(host.Left.ActualWidth >= 299.5, $"The left side was {host.Left.ActualWidth} wide.");
    }

    [WpfFact]
    public void The_right_side_keeps_at_least_its_minimum_width()
    {
        var host = new Host(totalWidth: 1000 + BarWidth, leftShare: 9, leftMinWidth: 0, rightShare: 1, rightMinWidth: 300);

        Assert.True(host.Right.ActualWidth >= 299.5, $"The right side was {host.Right.ActualWidth} wide.");
    }

    [WpfFact]
    public void The_bar_between_the_sides_is_6_wide()
    {
        var host = new Host(totalWidth: 806, leftShare: 1, leftMinWidth: 0, rightShare: 1, rightMinWidth: 0);

        Assert.InRange(host.Bar.ActualWidth, BarWidth - 0.5, BarWidth + 0.5);
    }

    [WpfFact]
    public void The_bar_takes_no_focus_and_is_not_a_tab_stop()
    {
        var host = new Host(totalWidth: 806, leftShare: 1, leftMinWidth: 0, rightShare: 1, rightMinWidth: 0);

        Assert.False(host.Bar.Focusable);
        Assert.False(host.Bar.IsTabStop);
    }

    [WpfFact]
    public void The_bar_takes_its_color_from_the_theme_resource_and_follows_it()
    {
        var host = new Host(totalWidth: 806, leftShare: 1, leftMinWidth: 0, rightShare: 1, rightMinWidth: 0);
        host.Panes.Resources[BarKey] = new SolidColorBrush(Colors.Red);
        Assert.Equal(Colors.Red, ((SolidColorBrush)host.Bar.Background).Color);

        host.Panes.Resources[BarKey] = new SolidColorBrush(Colors.Blue);

        Assert.Equal(Colors.Blue, ((SolidColorBrush)host.Bar.Background).Color);
    }

    [WpfFact]
    public void A_zero_minimum_width_is_allowed()
    {
        var host = new Host(totalWidth: 806, leftShare: 1, leftMinWidth: 0, rightShare: 1, rightMinWidth: 0);

        Assert.NotNull(host.Panes);
    }

    [WpfFact]
    public void A_left_side_that_already_carries_a_column_still_lays_out_on_the_left()
    {
        var left = new Border();
        Grid.SetColumn(left, 2);
        var right = new Border();
        var panes = new SplitPanes(left, 1, 0, right, 3, 0);
        panes.Measure(new Size(806, 100));
        panes.Arrange(new Rect(0, 0, 806, 100));

        Assert.InRange(left.TranslatePoint(new Point(0, 0), panes).X, -0.5, 0.5);
        Assert.InRange(left.ActualWidth, 199.5, 200.5);
    }

    [WpfFact]
    public void Rejects_a_missing_left_side()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new SplitPanes(null!, 1, 0, new Border(), 1, 0));

        Assert.Equal("left", error.ParamName);
    }

    [WpfFact]
    public void Rejects_a_missing_right_side()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new SplitPanes(new Border(), 1, 0, null!, 1, 0));

        Assert.Equal("right", error.ParamName);
    }

    [WpfTheory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_a_left_share_that_is_not_a_positive_finite_number(double share)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitPanes(new Border(), share, 0, new Border(), 1, 0));
    }

    [WpfTheory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_a_right_share_that_is_not_a_positive_finite_number(double share)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitPanes(new Border(), 1, 0, new Border(), share, 0));
    }

    [WpfTheory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_a_left_minimum_width_that_is_negative_or_not_finite(double minWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitPanes(new Border(), 1, minWidth, new Border(), 1, 0));
    }

    [WpfTheory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_a_right_minimum_width_that_is_negative_or_not_finite(double minWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitPanes(new Border(), 1, 0, new Border(), 1, minWidth));
    }

    private sealed class Host
    {
        public Host(double totalWidth, double leftShare, double leftMinWidth, double rightShare, double rightMinWidth)
        {
            Left = new Border();
            Right = new Border();
            Panes = new SplitPanes(Left, leftShare, leftMinWidth, Right, rightShare, rightMinWidth);
            Panes.Measure(new Size(totalWidth, 100));
            Panes.Arrange(new Rect(0, 0, totalWidth, 100));
        }

        public Border Left { get; }

        public Border Right { get; }

        public SplitPanes Panes { get; }

        public GridSplitter Bar => Panes.Children.OfType<GridSplitter>().Single();
    }
}
