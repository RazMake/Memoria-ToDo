using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A press on a tag's heading that moves past the system's drag distance drags the group; a release before that is a click; and a
/// press that ended outside the heading never starts a drag later. Mouse events are raised on a heading in a bare container
/// with no window (a WPF fact only because WPF elements need a thread of their own), and the pointer is a <see cref="FakeDragPointer"/> because the mouse of the PC can't be told where to be.
/// </summary>
[Unit]
public class GroupHeadingDragTests
{
    private static readonly double Far = SystemParameters.MinimumHorizontalDragDistance + 20;

    private readonly FakeDragPointer _pointer = new();
    private readonly GroupHeading _heading;
    private int _clicks;

    public GroupHeadingDragTests()
    {
        _heading = new GroupHeading("work", new FakeTagColors(), new FakeTheme(), _pointer);
        _heading.Clicked += (_, _) => _clicks++;
    }

    private void PressAt(double x)
    {
        _pointer.Position = new Point(x, 5);
        _pointer.IsLeftButtonDown = true;
        Raise(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
    }

    private void MoveTo(double x)
    {
        _pointer.Position = new Point(x, 5);
        SimulatedPointer.Raise(_heading, Mouse.PreviewMouseMoveEvent);
    }

    private void ReleaseOnHeading()
    {
        _pointer.IsLeftButtonDown = false;
        Raise(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
    }

    private void PointerLeaves()
    {
        SimulatedPointer.Leave(_heading);
    }

    private void Raise(RoutedEventArgs args)
    {
        args.Source = _heading;
        _heading.RaiseEvent(args);
    }

    [WpfFact]
    public void Moving_past_the_drag_distance_with_the_button_held_drags_the_group_with_its_tag()
    {
        PressAt(5);

        MoveTo(5 + Far);

        var data = Assert.Single(_pointer.Drags);
        Assert.Equal("work", DraggedGroup.From(data)!.Tag);
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
    public void A_release_before_the_drag_distance_is_a_click()
    {
        PressAt(5);
        MoveTo(6);

        ReleaseOnHeading();

        Assert.Equal(1, _clicks);
        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void A_release_after_a_drag_started_is_no_click()
    {
        PressAt(5);
        MoveTo(5 + Far);

        ReleaseOnHeading();

        Assert.Equal(0, _clicks);
    }

    [WpfFact]
    public void Moving_over_the_heading_with_no_press_on_it_does_not_drag()
    {
        _pointer.IsLeftButtonDown = true;

        MoveTo(5 + Far);

        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void A_press_released_outside_the_heading_does_not_start_a_drag_when_the_pointer_later_moves_over_it_with_the_button_held()
    {
        PressAt(5);
        PointerLeaves();
        _pointer.IsLeftButtonDown = false; // The release happens outside, where the heading hears nothing.

        _pointer.IsLeftButtonDown = true; // A new press, on something else, drags back over the heading.
        MoveTo(5 + Far);

        Assert.Empty(_pointer.Drags);
    }

    [WpfFact]
    public void The_No_tag_heading_cannot_be_dragged()
    {
        var noTag = new GroupHeading(null, new FakeTagColors(), new FakeTheme(), _pointer);
        _pointer.Position = new Point(5, 5);
        _pointer.IsLeftButtonDown = true;
        noTag.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = noTag });

        _pointer.Position = new Point(5 + Far, 5);
        SimulatedPointer.Raise(noTag, Mouse.PreviewMouseMoveEvent);

        Assert.Empty(_pointer.Drags);
    }
}
