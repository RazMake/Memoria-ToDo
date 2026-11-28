using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Keeps a checkbox's middle level with the middle of the first line of the title beside it, at any text size and however many
/// lines the title wraps to. The checkbox sits at the top of its cell, and this moves it down by the difference.
/// </summary>
internal sealed class FirstLineAlignment
{
    private const double Tolerance = 0.25;

    private readonly FormattedTitle _title;
    private readonly CheckBox _box;
    private readonly double _gap;
    private bool _stale = true;

    /// <param name="gap">The space to the right of the checkbox.</param>
    public FirstLineAlignment(FormattedTitle title, CheckBox box, double gap)
    {
        _title = title;
        _box = box;
        _gap = gap;

        // The layout event is subscribed only while the title is in a window, so a removed row is not kept alive by it.
        title.Loaded += (_, _) =>
        {
            _stale = true;
            title.LayoutUpdated += OnLayoutUpdated;
        };
        title.Unloaded += (_, _) => title.LayoutUpdated -= OnLayoutUpdated;
        title.SizeChanged += (_, _) => _stale = true;
    }

    /// <summary>Asks for a new alignment at the next layout, as when the title's text changed.</summary>
    public void Invalidate() => _stale = true;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_stale || !_title.IsArrangeValid || !_box.IsArrangeValid || _title.ActualHeight <= 0)
        {
            return;
        }

        var first = _title.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (first.IsEmpty)
        {
            return;
        }

        _stale = false;
        var top = first.Top + first.Height / 2 - _box.ActualHeight / 2;
        if (Math.Abs(_box.Margin.Top - top) > Tolerance)
        {
            _box.Margin = new Thickness(0, top, _gap, 0);
        }
    }
}
