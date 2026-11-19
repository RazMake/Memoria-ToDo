using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>When a list scrolls during a drag, and how fast: in a band along its top or bottom edge, faster toward the edge.</summary>
[Unit]
public class EdgeBandTests
{
    private const double Height = 400;

    [Fact]
    public void Does_not_scroll_in_the_middle_of_the_list()
    {
        Assert.Equal(0, EdgeBand.Step(Height / 2, Height));
    }

    [Theory]
    [InlineData(EdgeBand.Size)]
    [InlineData(Height - EdgeBand.Size)]
    public void Does_not_scroll_where_the_band_begins(double y)
    {
        Assert.Equal(0, EdgeBand.Step(y, Height));
    }

    [Fact]
    public void Scrolls_up_with_a_negative_step_in_the_top_band()
    {
        Assert.True(EdgeBand.Step(EdgeBand.Size / 2, Height) < 0);
    }

    [Fact]
    public void Scrolls_down_with_a_positive_step_in_the_bottom_band()
    {
        Assert.True(EdgeBand.Step(Height - EdgeBand.Size / 2, Height) > 0);
    }

    [Fact]
    public void Scrolls_the_most_at_the_very_edge_in_either_direction()
    {
        Assert.Equal(-EdgeBand.MaxStep, EdgeBand.Step(0, Height));
        Assert.Equal(EdgeBand.MaxStep, EdgeBand.Step(Height, Height));
    }

    [Fact]
    public void Scrolls_the_most_for_a_pointer_beyond_the_edge()
    {
        Assert.Equal(-EdgeBand.MaxStep, EdgeBand.Step(-30, Height));
        Assert.Equal(EdgeBand.MaxStep, EdgeBand.Step(Height + 30, Height));
    }

    [Fact]
    public void Scrolls_faster_the_closer_the_pointer_is_to_the_edge()
    {
        Assert.True(EdgeBand.Step(10, Height) < EdgeBand.Step(30, Height));
        Assert.True(EdgeBand.Step(Height - 10, Height) > EdgeBand.Step(Height - 30, Height));
    }

    [Fact]
    public void Scrolls_half_the_most_halfway_into_the_band()
    {
        Assert.Equal(-EdgeBand.MaxStep / 2, EdgeBand.Step(EdgeBand.Size / 2, Height), precision: 6);
        Assert.Equal(EdgeBand.MaxStep / 2, EdgeBand.Step(Height - EdgeBand.Size / 2, Height), precision: 6);
    }
}
