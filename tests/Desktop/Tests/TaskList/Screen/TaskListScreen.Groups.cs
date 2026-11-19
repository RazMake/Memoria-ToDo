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

    /// <summary>
    /// Moves the pointer of a group's drag over the group named <paramref name="onGroup"/>, as <see cref="DropGroup"/> places it.
    /// </summary>
    /// <returns>The effects the drop allows there: <see cref="DragDropEffects.None"/> where it is refused.</returns>
    public DragDropEffects DragGroupOver(string tag, string onGroup, bool lowerHalf = false)
    {
        var (element, at) = GroupTarget(onGroup, lowerHalf);
        var effects = SimulatedDrop.Over(element, new DraggedGroup(tag).ToData(), at);
        Refresh();
        return effects;
    }

    /// <summary>Moves the pointer of a task's drag over a row, as <see cref="Drop"/> places it.</summary>
    /// <returns>The effects the drop allows there: <see cref="DragDropEffects.None"/> where it is refused.</returns>
    public DragDropEffects DragTaskOver(Shared.Tasks.TaskItem task, string onRowTitle, bool lowerHalf = false, string? onGroup = null, string? fromGroup = null)
    {
        var row = Row(onRowTitle, onGroup);
        var effects = SimulatedDrop.Over(row, DragOf(task, fromGroup).ToData(), new Point(1, lowerHalf ? row.ActualHeight - 1 : 1));
        Refresh();
        return effects;
    }

    /// <summary>The window's y of the drop line's top, or null while no line shows.</summary>
    public double? DropLineTop
    {
        get
        {
            Refresh();
            var line = Visuals(Window).OfType<FrameworkElement>()
                .FirstOrDefault(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "DropLine");
            return line?.TranslatePoint(new Point(0, 0), Window).Y;
        }
    }

    /// <summary>The window's y of the top of the group's heading.</summary>
    public double HeadingTop(string group) => PositionFromTop(Headings().Single(h => AutomationProperties.GetName(h) == group));

    private (FrameworkElement Element, Point At) GroupTarget(string group, bool lowerHalf)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        var element = lowerHalf ? RowsOf(heading).OrderBy(PositionFromTop).LastOrDefault() ?? heading : heading;
        return (element, new Point(1, lowerHalf ? element.ActualHeight - 1 : 1));
    }
}
