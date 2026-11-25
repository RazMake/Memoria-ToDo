using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>The rule that moves one pill to another place in the list of tags, and leaves the list it was given alone.</summary>
[Unit]
public class TagOrderTests
{
    [Theory]
    [InlineData(0, 2, "b,c,a,d")]
    [InlineData(3, 0, "d,a,b,c")]
    [InlineData(1, 2, "a,c,b,d")]
    [InlineData(2, 1, "a,c,b,d")]
    [InlineData(0, 3, "b,c,d,a")]
    public void The_moved_tag_ends_at_the_target_place_and_the_others_keep_their_order(int from, int to, string expected) =>
        Assert.Equal(expected.Split(','), TagOrder.Move(["a", "b", "c", "d"], from, to));

    [Fact]
    public void Moving_a_tag_to_where_it_is_gives_the_same_order()
    {
        Assert.Equal(["a", "b", "c"], TagOrder.Move(["a", "b", "c"], 1, 1));
    }

    [Fact]
    public void The_list_it_was_given_is_left_as_it_was()
    {
        string[] tags = ["a", "b", "c"];

        TagOrder.Move(tags, 0, 2);

        Assert.Equal(["a", "b", "c"], tags);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    public void A_place_outside_the_list_is_refused(int from, int to)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TagOrder.Move(["a", "b", "c"], from, to));
    }

    [Fact]
    public void No_list_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => TagOrder.Move(null!, 0, 0));
    }
}
