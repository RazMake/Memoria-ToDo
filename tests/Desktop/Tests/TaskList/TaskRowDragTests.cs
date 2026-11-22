using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A press on a task row that moves past the system's drag distance drags the task, and a press that ended outside the row, or
/// on it, never starts a drag later. Mouse events are raised on a row in a bare container with no window (a screen test only
/// because WPF elements need a thread of their own), and the pointer is a <see cref="FakeDragPointer"/> because the mouse of the
/// PC can't be told where to be.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskRowDragTests
{
    private static readonly double Far = SystemParameters.MinimumHorizontalDragDistance + 20;

    private readonly FakeDragPointer _pointer = new();
    private readonly Shared.Tasks.TaskItem _task = new ListTasks().Add("Buy milk");
    private readonly TaskRow _row;

    public TaskRowDragTests()
    {
        _row = new TaskRow(_task, new FakeTheme(), new FakeDefaultBrowser(), _pointer, Serilog.Core.Logger.None);
    }

    private void PressAt(double x)
    {
        _pointer.Position = new Point(x, 5);
        _pointer.IsLeftButtonDown = true;
        Raise(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
    }

    private void MoveTo(double x)
    {
        _pointer.Position = new Point(x, 5);
        SimulatedPointer.Raise(_row, Mouse.MouseMoveEvent);
    }

    private void ReleaseOnRow()
    {
        _pointer.IsLeftButtonDown = false;
        Raise(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
    }

    private void PointerLeaves() => SimulatedPointer.Leave(_row);

    private void Raise(RoutedEventArgs args)
    {
        args.Source = _row;
        _row.RaiseEvent(args);
    }

    [WpfFact]
    public void Moving_past_the_drag_distance_with_the_button_held_drags_the_task()
    {
        PressAt(5);

        MoveTo(5 + Far);

        var data = Assert.Single(_pointer.Drags);
        Assert.Equal(_task.Id, DraggedTask.From(data)!.Task.Id);
    }

    [WpfFact]
    public void Moving_less_than_the_drag_distance_does_not_drag()
    {
        PressAt(5);

        MoveTo(5 + SystemParameters.MinimumHorizontalDragDistance / 2);

        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void A_drag_starts_once_however_far_the_pointer_goes()
    {
        PressAt(5);

        MoveTo(5 + Far);
        MoveTo(5 + 2 * Far);

        Assert.Single(_pointer.Drags);
    }

    [WpfFact]
    public void Moving_over_the_row_with_no_press_on_it_does_not_drag()
    {
        _pointer.IsLeftButtonDown = true;

        MoveTo(5 + Far);

        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void A_press_released_outside_the_row_does_not_start_a_drag_when_the_pointer_later_moves_over_it_with_the_button_held()
    {
        PressAt(5);
        PointerLeaves();
        _pointer.IsLeftButtonDown = false; // The release happens outside, where the row hears nothing.

        _pointer.IsLeftButtonDown = true; // A new press, on something else, drags back over the row.
        MoveTo(5 + Far);

        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void A_press_released_on_the_row_does_not_start_a_drag_when_the_pointer_later_moves_over_it_with_the_button_held()
    {
        PressAt(5);
        ReleaseOnRow();

        _pointer.IsLeftButtonDown = true; // A new press, on something else, drags back over the row.
        MoveTo(5 + Far);

        Assert.Empty(_pointer.Drags);
    }
}
