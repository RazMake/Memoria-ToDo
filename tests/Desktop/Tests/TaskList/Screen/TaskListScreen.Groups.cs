using System.Windows;
using System.Windows.Automation;
using Memoria.ToDo.Desktop.Fakes;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a Task View test drags a group and reads the drop line, through what the screen shows. A group drag carries
/// <see cref="DraggedGroup"/> as a row drag carries <see cref="DraggedTask"/>. The drop line has automation id "DropLine", and
/// shows only while a drag is over a place a drop would land; it is drawn above the list, so it is found beside the rows
/// in the window's visuals. Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>
    /// Drops the group on the group named <paramref name="onGroup"/> as the mouse does: the pointer is over its heading's
    /// upper half, or over the bottom of its last shown row (its heading when collapsed) when <paramref name="lowerHalf"/>.
    /// </summary>
    /// <returns>Whether a handler took the drop.</returns>
    public bool DropGroup(string tag, string onGroup, bool lowerHalf = false)
    {
        var (element, at) = GroupTarget(onGroup, lowerHalf);
        var taken = SimulatedDrop.Data(element, new DraggedGroup(tag).ToData(), at);
        Refresh();
        return taken;
    }

    private (FrameworkElement Element, Point At) GroupTarget(string group, bool lowerHalf)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        var element = lowerHalf ? RowsOf(heading).OrderBy(PositionFromTop).LastOrDefault() ?? heading : heading;
        return (element, new Point(1, lowerHalf ? element.ActualHeight - 1 : 1));
    }
}
