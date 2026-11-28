using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>When a press that moves has gone far enough to be a drag: a row's drag and a group heading's drag share this rule.</summary>
[Unit]
public class DragDistanceTests
{
    [Fact]
    public void A_move_shorter_than_both_system_distances_is_not_a_drag()
    {
        Assert.False(DragDistance.Passed(dx: 3, dy: -3, minimumX: 4, minimumY: 4));
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(-4, 0)]
    [InlineData(0, 4)]
    [InlineData(0, -4)]
    [InlineData(10, 10)]
    public void A_move_that_reaches_either_system_distance_is_a_drag(double dx, double dy)
    {
        Assert.True(DragDistance.Passed(dx, dy, minimumX: 4, minimumY: 4));
    }

    [Fact]
    public void The_horizontal_and_vertical_distances_apply_to_their_own_direction()
    {
        Assert.False(DragDistance.Passed(dx: 0, dy: 5, minimumX: 4, minimumY: 8));
        Assert.True(DragDistance.Passed(dx: 5, dy: 0, minimumX: 4, minimumY: 8));
    }
}
