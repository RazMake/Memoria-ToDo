using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What a group's drop asks Tasks' tag order for: the tag moved, and the two shown groups it lands between. The gap is a
/// position among the shown groups, as the drop rule gives it: 0 is before the first, and the shown count is after the last.
/// A hidden group (by search) is not in the list given, so it is never a neighbor. "No tag" is a null tag, and is never a neighbor.
/// </summary>
[Unit]
public class GroupMovesTests
{
    private static readonly string?[] Shown = ["work", "home", "play", null];

    [Fact]
    public void A_group_dropped_before_the_first_group_has_no_previous_tag()
    {
        Assert.Equal(new GroupMove("play", null, "work"), GroupMoves.Of(Shown, "play", gap: 0));
    }

    [Fact]
    public void A_group_dropped_between_two_groups_lands_between_them()
    {
        Assert.Equal(new GroupMove("work", "home", "play"), GroupMoves.Of(Shown, "work", gap: 2));
    }

    [Fact]
    public void A_group_dropped_before_No_tag_has_no_next_tag()
    {
        Assert.Equal(new GroupMove("work", "play", null), GroupMoves.Of(Shown, "work", gap: 3));
    }

    [Fact]
    public void A_group_dropped_after_the_last_group_when_there_is_no_No_tag_group_has_no_next_tag()
    {
        Assert.Equal(new GroupMove("work", "play", null), GroupMoves.Of(["work", "home", "play"], "work", gap: 3));
    }

    [Fact]
    public void A_group_dropped_in_its_own_gap_or_the_gap_after_it_is_no_move()
    {
        // "home" is the second group, so the gaps before and after it are 1 and 2.
        Assert.Null(GroupMoves.Of(Shown, "home", gap: 1));
        Assert.Null(GroupMoves.Of(Shown, "home", gap: 2));
    }

    [Fact]
    public void A_group_dropped_in_the_gap_after_the_last_group_that_is_itself_is_no_move()
    {
        Assert.Null(GroupMoves.Of(["work", "home", "play"], "play", gap: 3));
    }

    [Fact]
    public void A_group_dropped_between_two_shown_groups_ignores_the_hidden_groups_between_them()
    {
        // Search hides "home", which sits between "work" and "play" in the tag order.
        Assert.Equal(new GroupMove("lone", "work", "play"), GroupMoves.Of(["work", "play", "lone", null], "lone", gap: 1));
    }

    [Fact]
    public void A_tag_is_found_in_the_shown_groups_ignoring_letter_case()
    {
        // "WORK" is the shown group "work", so the gaps next to it, 0 and 1, are its own.
        Assert.Null(GroupMoves.Of(Shown, "WORK", gap: 0));
        Assert.Null(GroupMoves.Of(Shown, "WORK", gap: 1));
    }
}
