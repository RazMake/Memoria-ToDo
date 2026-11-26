using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class MoveMarkersTests
{
    [Fact]
    public void Concurrent_moves_get_distinct_positive_markers()
    {
        var markers = new MoveMarkers(100);
        var values = new nint[500];
        Parallel.For(0, values.Length, i => values[i] = markers.Next());
        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.All(values, value => Assert.True(value > 0));
    }

    [Fact]
    public void Running_out_fails_instead_of_reusing_a_marker()
    {
        var markers = new MoveMarkers(int.MaxValue);
        Assert.Equal((nint)int.MaxValue, markers.Next());
        Assert.Throws<InvalidOperationException>(() => markers.Next());
        Assert.Throws<InvalidOperationException>(() => markers.Next());
    }

    [Fact]
    public void Shared_moves_fit_the_positive_32_bit_marker_range()
    {
        Assert.InRange(MoveMarkers.Shared.Next(), (nint)1, (nint)int.MaxValue);
    }

    [Theory]
    [InlineData(0x80000000L)]
    [InlineData(0xFF5156FFL)]
    public void Refuses_a_start_beyond_the_portable_marker_range(long first) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveMarkers(unchecked((nint)first)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Refuses_a_nonpositive_start(int first) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveMarkers(first));
}
