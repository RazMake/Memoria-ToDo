using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// What every <see cref="ITagOrder"/> promises when tags have no place yet, written once, so the fake can't drift from the real
/// one. A test project derives one class from it, marks that class's kind, and supplies a new order for each test in which the
/// tags a, b, and c are in use and none has a place. Each check shows the tags with <see cref="ITagOrder.Sort"/> before it moves
/// one, as the Task list does, which is how a fake learns which tags are in use.
/// </summary>
public abstract class TagOrderContract
{
    private static readonly string[] InUse = ["a", "b", "c"];

    /// <summary>The order under test, new for each test, with a, b, and c in use and no tag placed.</summary>
    protected abstract ITagOrder Order { get; }

    private IReadOnlyList<string> Shown()
    {
        return Order.Sort(InUse);
    }

    [Fact]
    public void Puts_a_tag_before_the_first_neighbor_and_leaves_the_other_unplaced_tags_after_it()
    {
        Assert.Equal(["a", "b", "c"], Shown());

        Order.Move("c", null, "a");

        Assert.Equal(["c", "a", "b"], Shown());
    }

    [Fact]
    public void Puts_a_tag_after_the_last_neighbor()
    {
        Assert.Equal(["a", "b", "c"], Shown());

        Order.Move("a", "c", null);

        Assert.Equal(["b", "c", "a"], Shown());
    }

    [Fact]
    public void Puts_a_tag_between_two_neighbors_that_have_no_place()
    {
        Assert.Equal(["a", "b", "c"], Shown());

        Order.Move("c", "a", "b");

        Assert.Equal(["a", "c", "b"], Shown());
    }

    [Fact]
    public void Changes_nothing_and_raises_no_notice_when_both_neighbors_are_open()
    {
        Assert.Equal(["a", "b", "c"], Shown());
        var notices = 0;
        Order.Changed += (_, _) => notices++;

        Order.Move("b", null, null);

        Assert.Equal(["a", "b", "c"], Shown());
        Assert.Equal(0, notices);
    }

    [Fact]
    public void Raises_one_notice_for_a_move_that_changes_the_order()
    {
        Assert.Equal(["a", "b", "c"], Shown());
        var notices = 0;
        Order.Changed += (_, _) => notices++;

        Order.Move("c", null, "a");

        Assert.Equal(1, notices);
    }
}
