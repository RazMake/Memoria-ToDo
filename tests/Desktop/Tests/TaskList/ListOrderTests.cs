using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Where a task goes when the user nudges it with Alt+Up or Alt+Down, or drops it on a row: always among the tasks of one
/// group, described as the neighbors it lands between in that group. A group is the list of its tasks in rank order.
/// </summary>
[Unit]
public class ListOrderTests
{
    private readonly ListTasks _tasks = new();

    // Adds the titles so that they rank in this order.
    private void AddInOrder(params string[] titles)
    {
        foreach (var title in titles.Reverse())
        {
            _tasks.Add(title);
        }
    }

    private TaskItem Item(string title) => _tasks.Tasks.All.Single(task => task.Title == title);

    private string Id(string title) => Item(title).Id;

    private string? IdOrNull(string? title) => title is null ? null : Id(title);

    // The tasks with these titles, in the order given: one group, as the list shows it. Each group gets a key of its own, which
    // stays the same when the list shows the group again after a refresh (see Refreshed).
    private List<TaskItem> Group(params string[] titles)
    {
        var group = Refreshed(null, titles);
        _keys[group] = new GroupKey($"group {_keys.Count}", Resolved: false);
        return group;
    }

    // The same group as shown after a refresh: a new list, in the same place, with the titles it has now.
    private List<TaskItem> Refreshed(List<TaskItem>? before, params string[] titles)
    {
        var group = new List<TaskItem>(titles.Select(Item));
        if (before is not null)
        {
            _keys[group] = _keys[before] with { };
        }

        return group;
    }

    private readonly Dictionary<List<TaskItem>, GroupKey> _keys = new(ReferenceEqualityComparer.Instance);

    private Move? Nudge(List<TaskItem> group, string title, int step) => ListOrder.Nudge(group, Id(title), step);

    // The dragged row was in the group it is dropped on, unless the test names the group it was dragged from.
    private Move? Drop(List<TaskItem> target, List<TaskItem> shown, string onTitle, string droppedTitle, bool lowerHalf, List<TaskItem>? from = null) =>
        ListOrder.Drop(_keys[from ?? target], _keys[target], target, shown, Id(onTitle), Id(droppedTitle), lowerHalf);

    private void AssertMove(Move? move, string title, string? after, string? before)
    {
        Assert.Equal(new Move(Id(title), IdOrNull(after), IdOrNull(before)), move);
    }

    [Fact]
    public void Nudging_down_puts_the_task_between_the_next_one_and_the_one_after()
    {
        AddInOrder("A", "B", "C");

        AssertMove(Nudge(Group("A", "B", "C"), "A", 1), "A", after: "B", before: "C");
    }

    [Fact]
    public void Nudging_down_past_the_second_to_last_puts_the_task_last()
    {
        AddInOrder("A", "B", "C");

        AssertMove(Nudge(Group("A", "B", "C"), "B", 1), "B", after: "C", before: null);
    }

    [Fact]
    public void Nudging_up_puts_the_task_between_the_one_before_the_previous_and_the_previous()
    {
        AddInOrder("A", "B", "C");

        AssertMove(Nudge(Group("A", "B", "C"), "C", -1), "C", after: "A", before: "B");
    }

    [Fact]
    public void Nudging_up_to_the_top_puts_the_task_first()
    {
        AddInOrder("A", "B");

        AssertMove(Nudge(Group("A", "B"), "B", -1), "B", after: null, before: "A");
    }

    [Fact]
    public void Nudging_the_first_task_of_a_group_up_or_the_last_down_moves_nothing()
    {
        AddInOrder("A", "B");

        Assert.Null(Nudge(Group("A", "B"), "A", -1));
        Assert.Null(Nudge(Group("A", "B"), "B", 1));
    }

    [Fact]
    public void Nudging_a_task_that_is_not_in_the_group_moves_nothing()
    {
        AddInOrder("A", "B", "Elsewhere");

        Assert.Null(Nudge(Group("A", "B"), "Elsewhere", 1));
    }

    [Fact]
    public void Nudging_goes_by_the_neighbors_in_the_group_not_the_ones_between_them_in_the_whole_list()
    {
        AddInOrder("A work", "B home", "C work", "D home", "E work");

        AssertMove(Nudge(Group("A work", "C work", "E work"), "A work", 1), "A work", after: "C work", before: "E work");
    }

    [Fact]
    public void Nudging_a_task_in_two_groups_goes_by_the_group_that_was_given()
    {
        AddInOrder("A", "Both", "C", "D");

        AssertMove(Nudge(Group("A", "Both", "D"), "Both", 1), "Both", after: "D", before: null);
        AssertMove(Nudge(Group("Both", "C"), "Both", 1), "Both", after: "C", before: null);
    }

    [Fact]
    public void Nudging_moves_a_resolved_task_among_the_resolved_ones()
    {
        _tasks.AddResolved("Z");
        _tasks.AddResolved("Y");
        _tasks.AddResolved("X");

        AssertMove(Nudge(Group("X", "Y", "Z"), "X", 1), "X", after: "Y", before: "Z");
    }

    [Fact]
    public void Dropping_on_the_lower_half_of_a_row_puts_the_task_after_that_row()
    {
        AddInOrder("A", "B", "C");
        var all = Group("A", "B", "C");

        AssertMove(Drop(all, all, "B", "A", lowerHalf: true), "A", after: "B", before: "C");
    }

    [Fact]
    public void Dropping_on_the_upper_half_of_a_row_puts_the_task_before_that_row()
    {
        AddInOrder("A", "B", "C");
        var all = Group("A", "B", "C");

        AssertMove(Drop(all, all, "A", "C", lowerHalf: false), "C", after: null, before: "A");
    }

    [Fact]
    public void Dropping_on_the_last_row_lower_half_puts_the_task_last()
    {
        AddInOrder("A", "B", "C");
        var all = Group("A", "B", "C");

        AssertMove(Drop(all, all, "C", "A", lowerHalf: true), "A", after: "C", before: null);
    }

    [Fact]
    public void Dropping_a_task_on_itself_moves_nothing()
    {
        AddInOrder("A", "B");
        var all = Group("A", "B");

        Assert.Null(Drop(all, all, "A", "A", lowerHalf: true));
    }

    [Fact]
    public void Dropping_where_the_task_already_shows_describes_that_same_place()
    {
        AddInOrder("A", "B", "C");
        var all = Group("A", "B", "C");

        AssertMove(Drop(all, all, "B", "A", lowerHalf: false), "A", after: null, before: "B");
    }

    [Fact]
    public void Dropping_puts_the_task_between_the_neighbors_in_its_group_not_in_the_whole_list()
    {
        AddInOrder("A work", "B home", "C work", "D home", "E work");
        var work = Group("A work", "C work", "E work");
        var shown = Group("A work", "B home", "C work", "D home", "E work");

        AssertMove(Drop(work, shown, "C work", "E work", lowerHalf: false), "E work", after: "A work", before: "C work");
    }

    [Fact]
    public void Dropping_a_task_of_one_group_on_another_group_moves_nothing()
    {
        AddInOrder("A work", "B home", "C home");
        var work = Group("A work");
        var home = Group("B home", "C home");
        var shown = Group("A work", "B home", "C home");

        Assert.Null(Drop(home, shown, "B home", "A work", lowerHalf: false));
        Assert.Null(Drop(work, shown, "A work", "C home", lowerHalf: true));
    }

    [Fact]
    public void Dropping_a_task_that_is_in_both_groups_on_a_row_of_one_of_them_moves_it_in_that_group()
    {
        AddInOrder("A", "Both", "C");
        var second = Group("Both", "C");
        var shown = Group("A", "Both", "C");

        AssertMove(Drop(second, shown, "C", "Both", lowerHalf: true), "Both", after: "C", before: null);
    }

    [Fact]
    public void Dropping_a_task_that_is_in_both_groups_from_one_group_on_a_row_of_the_other_moves_nothing()
    {
        AddInOrder("Home only", "Both", "Work only");
        var home = Group("Home only", "Both");
        var work = Group("Both", "Work only");
        var shown = Group("Home only", "Both", "Both", "Work only");

        Assert.Null(Drop(work, shown, "Work only", "Both", lowerHalf: true, from: home));
        Assert.Null(Drop(home, shown, "Home only", "Both", lowerHalf: false, from: work));
    }

    [Fact]
    public void Dropping_a_task_that_is_in_both_groups_within_the_group_it_was_dragged_from_still_moves_it()
    {
        AddInOrder("Home only", "Both", "Work only");
        var work = Group("Both", "Work only");
        var shown = Group("Home only", "Both", "Both", "Work only");

        AssertMove(Drop(work, shown, "Work only", "Both", lowerHalf: true, from: work), "Both", after: "Work only", before: null);
    }

    [Fact]
    public void Dropping_a_resolved_task_on_a_resolved_row_moves_it_among_the_resolved_ones()
    {
        AddInOrder("Open");
        _tasks.AddResolved("Y");
        _tasks.AddResolved("X");
        var resolved = Group("X", "Y");

        AssertMove(Drop(resolved, Group("Open", "X", "Y"), "Y", "X", lowerHalf: true), "X", after: "Y", before: null);
    }

    [Fact]
    public void Dropping_an_open_task_on_a_resolved_row_or_the_reverse_moves_nothing()
    {
        AddInOrder("A", "B");
        _tasks.AddResolved("X");
        var open = Group("A", "B");
        var resolved = Group("X");
        var shown = Group("A", "B", "X");

        Assert.Null(Drop(resolved, shown, "X", "A", lowerHalf: false));
        Assert.Null(Drop(open, shown, "A", "X", lowerHalf: false));
    }

    [Fact]
    public void Dropping_a_task_that_is_not_shown_still_describes_a_move_so_that_Tasks_can_say_it_is_gone()
    {
        AddInOrder("A", "B");
        var gone = _tasks.Add("Gone");
        _tasks.Tasks.Delete(gone.Id);
        var all = Group("A", "B");

        var move = ListOrder.Drop(_keys[all], _keys[all], all, all, Id("A"), gone.Id, lowerHalf: false);

        Assert.Equal(new Move(gone.Id, null, Id("A")), move);
    }

    // A refresh while a task is dragged, such as a sync or a tag-order change, shows its groups as new lists. The drag still
    // knows the group it came from by the group's place, not by the list it was shown in.

    [Fact]
    public void Dropping_after_a_refresh_still_moves_the_task_within_its_group()
    {
        AddInOrder("A", "B", "C");
        var before = Group("A", "B", "C");
        var after = Refreshed(before, "A", "B", "C");

        AssertMove(ListOrder.Drop(_keys[before], _keys[after], after, after, Id("A"), Id("C"), lowerHalf: false), "C", after: null, before: "A");
    }

    [Fact]
    public void Dropping_after_a_refresh_that_added_a_task_to_the_group_goes_by_the_group_as_it_is_now()
    {
        AddInOrder("A", "B", "C");
        _tasks.Add("New");
        var before = Group("A", "B", "C");
        var after = Refreshed(before, "New", "A", "B", "C");

        AssertMove(ListOrder.Drop(_keys[before], _keys[after], after, after, Id("B"), Id("C"), lowerHalf: false), "C", after: "A", before: "B");
    }

    [Fact]
    public void Dropping_a_task_deleted_during_the_drag_after_a_refresh_still_describes_a_move_so_that_Tasks_can_say_it_is_gone()
    {
        AddInOrder("A", "B");
        var gone = _tasks.Add("Gone");
        var before = Group("A", "B", "Gone");
        _tasks.Tasks.Delete(gone.Id);
        var after = Refreshed(before, "A", "B");

        var move = ListOrder.Drop(_keys[before], _keys[after], after, after, Id("A"), gone.Id, lowerHalf: false);

        Assert.Equal(new Move(gone.Id, null, Id("A")), move);
    }

    [Fact]
    public void Dropping_after_a_refresh_in_a_gap_still_moves_the_task_within_its_group()
    {
        AddInOrder("A", "B", "C");
        var before = Group("A", "B", "C");
        var after = Refreshed(before, "A", "B", "C");

        AssertMove(ListOrder.DropIn(_keys[before], _keys[after], after, after, Id("C"), gap: 1), "C", after: "A", before: "B");
    }

    [Fact]
    public void Dropping_after_a_refresh_on_another_group_than_the_one_it_was_dragged_from_still_moves_nothing()
    {
        AddInOrder("A work", "B home");
        var work = Group("A work");
        var home = Group("B home");
        var shown = Group("A work", "B home");
        // The refresh also shows the dragged task in the target group, as when it gained that tag during the drag.
        var after = Refreshed(home, "A work", "B home");

        Assert.Null(ListOrder.Drop(_keys[work], _keys[after], after, shown, Id("B home"), Id("A work"), lowerHalf: false));
    }

    // Dropping in a gap: the gap before the task at that place, or the end when the gap is the group's count.

    private Move? DropIn(List<TaskItem> target, string droppedTitle, int gap, List<TaskItem>? from = null) =>
        ListOrder.DropIn(_keys[from ?? target], _keys[target], target, target, Id(droppedTitle), gap);

    [Fact]
    public void Dropping_in_the_gap_before_a_task_puts_the_dropped_task_between_that_task_and_the_one_before_it()
    {
        AddInOrder("A", "B", "C");

        AssertMove(DropIn(Group("A", "B", "C"), "C", gap: 1), "C", after: "A", before: "B");
    }

    [Fact]
    public void Dropping_in_the_first_gap_puts_the_dropped_task_first()
    {
        AddInOrder("A", "B", "C");

        AssertMove(DropIn(Group("A", "B", "C"), "C", gap: 0), "C", after: null, before: "A");
    }

    [Fact]
    public void Dropping_in_the_last_gap_puts_the_dropped_task_last()
    {
        AddInOrder("A", "B", "C");

        AssertMove(DropIn(Group("A", "B", "C"), "A", gap: 3), "A", after: "C", before: null);
    }

    [Fact]
    public void Dropping_in_the_gap_next_to_the_dropped_task_describes_the_place_it_already_has()
    {
        AddInOrder("A", "B", "C");

        AssertMove(DropIn(Group("A", "B", "C"), "B", gap: 2), "B", after: "A", before: "C");
    }

    [Fact]
    public void Dropping_in_a_gap_of_another_group_than_the_one_it_was_dragged_from_moves_nothing()
    {
        AddInOrder("A", "B", "C");

        Assert.Null(DropIn(Group("B", "C"), "A", gap: 1, from: Group("A", "B")));
    }
}
