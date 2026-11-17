using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The press-to-drag gesture a task row and a group heading share: a press, then a move with the left button still held that
/// reaches the drag distance, starts one drag. A release before that is a click. A press the element never hears the end of,
/// because the pointer left it first, is forgotten, so a later move over the element with a button held starts no drag.
/// </summary>
[Unit]
public class PressDragTests
{
    private const double Horizontal = 4;
    private const double Vertical = 6;

    private readonly PressDrag _gesture = new(Horizontal, Vertical);

    private static Point At(double x, double y = 5) => new(x, y);

    [Fact]
    public void Moving_to_the_drag_distance_with_the_button_held_starts_a_drag()
    {
        _gesture.Press(At(10));

        Assert.True(_gesture.Move(At(10 + Horizontal), leftButtonDown: true));
    }

    [Fact]
    public void Moving_less_than_the_drag_distance_starts_no_drag()
    {
        _gesture.Press(At(10));

        Assert.False(_gesture.Move(At(10 + Horizontal - 1), leftButtonDown: true));
    }

    [Fact]
    public void A_drag_is_measured_from_the_press_in_each_direction_with_its_own_distance()
    {
        _gesture.Press(At(10, 10));

        Assert.False(_gesture.Move(At(10, 10 + Vertical - 1), leftButtonDown: true));
        Assert.True(_gesture.Move(At(10, 10 + Vertical), leftButtonDown: true));
    }

    [Fact]
    public void A_move_with_no_press_starts_no_drag()
    {
        Assert.False(_gesture.Move(At(100), leftButtonDown: true));
    }

    [Fact]
    public void A_move_with_the_button_up_starts_no_drag()
    {
        _gesture.Press(At(10));

        Assert.False(_gesture.Move(At(100), leftButtonDown: false));
    }

    [Fact]
    public void A_drag_starts_once_however_far_the_pointer_goes()
    {
        _gesture.Press(At(10));

        Assert.True(_gesture.Move(At(100), leftButtonDown: true));
        Assert.False(_gesture.Move(At(200), leftButtonDown: true));
    }

    [Fact]
    public void A_new_press_after_a_drag_can_start_another_drag()
    {
        _gesture.Press(At(10));
        _gesture.Move(At(100), leftButtonDown: true);
        _gesture.Release();

        _gesture.Press(At(10));

        Assert.True(_gesture.Move(At(100), leftButtonDown: true));
    }

    [Fact]
    public void A_release_before_the_drag_distance_is_a_click()
    {
        _gesture.Press(At(10));
        _gesture.Move(At(11), leftButtonDown: true);

        Assert.True(_gesture.Release());
    }

    [Fact]
    public void A_release_after_a_drag_started_is_no_click()
    {
        _gesture.Press(At(10));
        _gesture.Move(At(100), leftButtonDown: true);

        Assert.False(_gesture.Release());
    }

    [Fact]
    public void A_press_released_on_the_element_starts_no_drag_when_the_pointer_later_moves_with_a_button_held()
    {
        _gesture.Press(At(10));
        _gesture.Release();

        Assert.False(_gesture.Move(At(100), leftButtonDown: true));
    }

    [Fact]
    public void A_press_the_pointer_left_the_element_during_starts_no_drag_when_the_pointer_later_moves_over_it_with_a_button_held()
    {
        _gesture.Press(At(10));
        _gesture.Leave();

        Assert.False(_gesture.Move(At(100), leftButtonDown: true));
    }
}
