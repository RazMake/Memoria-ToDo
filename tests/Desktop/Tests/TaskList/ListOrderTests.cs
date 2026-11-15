using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Where a task goes when the user nudges it with Alt+Up or Alt+Down, or drops it on a row: always among the tasks of its
/// own group shown, and described as the neighbors it lands between.
/// </summary>
[Unit]
public class ListOrderTests
{
    private readonly ListTasks _tasks = new();

    // Adds the titles so that they show in this order, and returns the tasks in that order.
    private List<TaskItem> Open(params string[] titles)
    {
        foreach (var title in titles.Reverse())
        {
            _tasks.Add(title);
        }

        return [.. titles.Select(Item)];
    }

    private List<TaskItem> Resolved(params string[] titles)
    {
        foreach (var title in titles.Reverse())
        {
            _tasks.AddResolved(title);
        }

        return [.. titles.Select(Item)];
    }

    private TaskItem Item(string title) => _tasks.Tasks.All.Single(task => task.Title == title);

    private string Id(string title) => Item(title).Id;

    private string? IdOrNull(string? title) => title is null ? null : Id(title);

    private Move? Nudge(List<TaskItem> open, List<TaskItem> resolved, string title, int step) =>
        ListOrder.Nudge(open, resolved, Id(title), step);

    private void AssertMove(Move? move, string title, string? after, string? before)
    {
        Assert.Equal(new Move(Id(title), IdOrNull(after), IdOrNull(before)), move);
    }

    [Fact]
    public void Nudging_down_puts_the_task_between_the_next_one_and_the_one_after()
    {
        var open = Open("A", "B", "C");

        AssertMove(Nudge(open, [], "A", 1), "A", after: "B", before: "C");
    }

    [Fact]
    public void Nudging_down_past_the_second_to_last_puts_the_task_last()
    {
        var open = Open("A", "B", "C");

        AssertMove(Nudge(open, [], "B", 1), "B", after: "C", before: null);
    }

    [Fact]
    public void Nudging_up_puts_the_task_between_the_one_before_the_previous_and_the_previous()
    {
        var open = Open("A", "B", "C");

        AssertMove(Nudge(open, [], "C", -1), "C", after: "A", before: "B");
    }

    [Fact]
    public void Nudging_up_to_the_top_puts_the_task_first()
    {
        var open = Open("A", "B");

        AssertMove(Nudge(open, [], "B", -1), "B", after: null, before: "A");
    }

    [Fact]
    public void Nudging_the_first_task_up_or_the_last_down_moves_nothing()
    {
        var open = Open("A", "B");

        Assert.Null(Nudge(open, [], "A", -1));
        Assert.Null(Nudge(open, [], "B", 1));
    }

    [Fact]
    public void Nudging_a_task_that_is_not_shown_moves_nothing()
    {
        var open = Open("A", "B");
        _tasks.Add("Hidden");

        Assert.Null(Nudge(open, [], "Hidden", 1));
    }

    [Fact]
    public void Nudging_moves_a_resolved_task_among_the_resolved_ones()
    {
        var open = Open("Open");
        var resolved = Resolved("X", "Y", "Z");

        AssertMove(Nudge(open, resolved, "X", 1), "X", after: "Y", before: "Z");
    }

    [Fact]
    public void Nudging_never_crosses_from_the_open_tasks_to_the_resolved_ones_or_back()
    {
        var open = Open("A", "B");
        var resolved = Resolved("X", "Y");

        Assert.Null(Nudge(open, resolved, "B", 1));
        Assert.Null(Nudge(open, resolved, "X", -1));
    }

    [Fact]
    public void Dropping_on_the_lower_half_of_a_row_puts_the_task_after_that_row()
    {
        var open = Open("A", "B", "C");

        var move = ListOrder.Drop(open, [], Id("B"), Id("A"), lowerHalf: true);

        AssertMove(move, "A", after: "B", before: "C");
    }

    [Fact]
    public void Dropping_on_the_upper_half_of_a_row_puts_the_task_before_that_row()
    {
        var open = Open("A", "B", "C");

        var move = ListOrder.Drop(open, [], Id("A"), Id("C"), lowerHalf: false);

        AssertMove(move, "C", after: null, before: "A");
    }

    [Fact]
    public void Dropping_on_the_last_row_lower_half_puts_the_task_last()
    {
        var open = Open("A", "B", "C");

        var move = ListOrder.Drop(open, [], Id("C"), Id("A"), lowerHalf: true);

        AssertMove(move, "A", after: "C", before: null);
    }

    [Fact]
    public void Dropping_a_task_on_itself_moves_nothing()
    {
        var open = Open("A", "B");

        Assert.Null(ListOrder.Drop(open, [], Id("A"), Id("A"), lowerHalf: true));
    }

    [Fact]
    public void Dropping_where_the_task_already_shows_describes_that_same_place()
    {
        var open = Open("A", "B", "C");

        var move = ListOrder.Drop(open, [], Id("B"), Id("A"), lowerHalf: false);

        AssertMove(move, "A", after: null, before: "B");
    }

    [Fact]
    public void Dropping_places_the_task_by_the_rows_shown_when_a_filter_hides_others()
    {
        var shown = Open("A", "C");

        var move = ListOrder.Drop(shown, [], Id("C"), Id("A"), lowerHalf: true);

        AssertMove(move, "A", after: "C", before: null);
    }

    [Fact]
    public void Dropping_a_resolved_task_on_a_resolved_row_moves_it_among_the_resolved_ones()
    {
        var open = Open("Open");
        var resolved = Resolved("X", "Y");

        var move = ListOrder.Drop(open, resolved, Id("Y"), Id("X"), lowerHalf: true);

        AssertMove(move, "X", after: "Y", before: null);
    }

    [Fact]
    public void Dropping_an_open_task_on_a_resolved_row_or_the_reverse_moves_nothing()
    {
        var open = Open("A", "B");
        var resolved = Resolved("X");

        Assert.Null(ListOrder.Drop(open, resolved, Id("X"), Id("A"), lowerHalf: false));
        Assert.Null(ListOrder.Drop(open, resolved, Id("A"), Id("X"), lowerHalf: false));
    }

    [Fact]
    public void Dropping_a_task_that_is_not_shown_still_describes_a_move_so_that_Tasks_can_say_it_is_gone()
    {
        var open = Open("A", "B");
        var gone = _tasks.Add("Gone");
        _tasks.Tasks.Delete(gone.Id);

        var move = ListOrder.Drop(open, [], Id("A"), gone.Id, lowerHalf: false);

        Assert.Equal(new Move(gone.Id, null, Id("A")), move);
    }
}
