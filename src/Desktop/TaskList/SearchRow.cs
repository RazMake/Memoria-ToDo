using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The search box with the tag filter beside it. The search box takes the width the filter leaves free; when that is less
/// than the box's smallest width, the filter wraps to a row below it.
/// </summary>
internal sealed class SearchRow : Panel
{
    private const double RowGap = 4;

    private readonly FrameworkElement _search;
    private readonly UIElement _filter;
    private bool _wrapped;
    private double _filterWidth;

    public SearchRow(FrameworkElement search, UIElement filter)
    {
        _search = search;
        _filter = filter;
        Children.Add(search);
        Children.Add(filter);
    }

    protected override Size MeasureOverride(Size available)
    {
        _filter.Measure(new Size(double.PositiveInfinity, available.Height));
        _filterWidth = _filter.DesiredSize.Width;
        var smallestSearch = _search.MinWidth + _search.Margin.Left + _search.Margin.Right;
        _wrapped = smallestSearch + _filterWidth > available.Width;
        if (!_wrapped)
        {
            _search.Measure(new Size(available.Width - _filterWidth, available.Height));
            return new Size(available.Width, Math.Max(_search.DesiredSize.Height, _filter.DesiredSize.Height));
        }

        var width = Math.Max(available.Width, smallestSearch);
        _search.Measure(new Size(width, available.Height));
        _filter.Measure(new Size(width, available.Height));
        return new Size(width, _search.DesiredSize.Height + RowGap + _filter.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_wrapped)
        {
            var searchHeight = _search.DesiredSize.Height;
            _search.Arrange(new Rect(0, 0, finalSize.Width, searchHeight));
            _filter.Arrange(new Rect(0, searchHeight + RowGap, finalSize.Width, _filter.DesiredSize.Height));
        }
        else
        {
            var searchWidth = Math.Max(0, finalSize.Width - _filterWidth);
            _search.Arrange(new Rect(0, 0, searchWidth, finalSize.Height));
            _filter.Arrange(new Rect(searchWidth, 0, _filterWidth, finalSize.Height));
        }

        return finalSize;
    }
}
