using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>Which section of the window is open: exactly one always is, the first at first.</summary>
[Unit]
public class AccordionStateTests
{
    [Fact]
    public void The_first_section_is_open_at_first()
    {
        var state = new AccordionState(3);

        Assert.Equal(0, state.Open);
    }

    [Fact]
    public void Selecting_a_closed_section_opens_it_and_closes_the_open_one()
    {
        var state = new AccordionState(3);

        var changed = state.Select(2);

        Assert.True(changed);
        Assert.Equal(2, state.Open);
    }

    [Fact]
    public void Selecting_the_open_section_changes_nothing()
    {
        var state = new AccordionState(3);
        state.Select(1);

        var changed = state.Select(1);

        Assert.False(changed);
        Assert.Equal(1, state.Open);
    }

    [Fact]
    public void Selecting_the_first_section_while_it_is_open_changes_nothing()
    {
        var state = new AccordionState(3);

        Assert.False(state.Select(0));
        Assert.Equal(0, state.Open);
    }

    [Fact]
    public void Sections_can_be_opened_one_after_another_and_again()
    {
        var state = new AccordionState(3);

        state.Select(1);
        state.Select(2);
        state.Select(0);

        Assert.Equal(0, state.Open);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Rejects_a_section_that_does_not_exist_and_keeps_the_open_one(int index)
    {
        var state = new AccordionState(3);
        state.Select(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Select(index));
        Assert.Equal(1, state.Open);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Needs_at_least_one_section(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccordionState(count));
    }
}
