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

    // The tasks with these titles, in the order given: one group, as the list shows it.
    private List<TaskItem> Group(params string[] titles) => [.. titles.Select(Item)];

    private Move? Nudge(List<TaskItem> group, string title, int step) => ListOrder.Nudge(group, Id(title), step);

    // The dragged row was in the group it is dropped on, unless the test names the group it was dragged from.
    private Move? Drop(List<TaskItem> target, List<TaskItem> shown, string onTitle, string droppedTitle, bool lowerHalf, List<TaskItem>? from = null) =>
        ListOrder.Drop(from ?? target, target, shown, Id(onTitle), Id(droppedTitle), lowerHalf);

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

        var move = ListOrder.Drop(all, all, all, Id("A"), gone.Id, lowerHalf: false);

        Assert.Equal(new Move(gone.Id, null, Id("A")), move);
    }
}
