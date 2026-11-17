using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The rows the groups show once some are collapsed: a collapsed group shows none and says how many it hides, and Up and Down
/// skip its rows. Groups come as the left side builds them, with a "No tag" group last.
/// </summary>
[Unit]
public class GroupPlanTests
{
    private readonly ListTasks _tasks = new();
    private readonly GroupCollapse _collapse = new();

    private IReadOnlyList<PlannedGroup> Plan()
    {
        var open = _tasks.Tasks.All.Where(task => task.Resolved is null).OrderBy(task => task.Rank, StringComparer.Ordinal).ToList();
        return GroupPlan.Of(TagGroups.Of(open, new Memoria.ToDo.Shared.Fakes.FakeTagOrder()), _collapse);
    }

    private static IReadOnlyList<string> Titles(PlannedGroup group) => [.. group.Rows.Select(task => task.Title)];

    private TaskItem Task(string title) => _tasks.Tasks.All.Single(task => task.Title == title);

    private RowPosition At(string? tag, string title) => new(tag, Task(title).Id);

    private string? NeighborTitle(RowPosition from, int step) =>
        GroupPlan.Neighbor(Plan(), from, step) is { } next ? _tasks.Tasks.All.Single(task => task.Id == next.TaskId).Title : null;

    // ---- What each group shows ----

    [Fact]
    public void An_expanded_group_shows_all_its_rows_in_order_and_no_count()
    {
        _tasks.Add("B", "", "home");
        _tasks.Add("A", "", "home");

        var group = Plan().Single();

        Assert.Equal("home", group.Tag);
        Assert.False(group.Collapsed);
        Assert.Equal(["A", "B"], Titles(group));
        Assert.Equal(2, group.TaskCount);
        Assert.Equal("", group.CountText);
    }

    [Fact]
    public void A_collapsed_group_shows_no_rows_and_a_count_of_the_tasks_it_hides()
    {
        _tasks.Add("C", "", "home");
        _tasks.Add("B", "", "home");
        _tasks.Add("A", "", "home");
        _collapse.Toggle("home");

        var group = Plan().Single();

        Assert.True(group.Collapsed);
        Assert.Empty(group.Rows);
        Assert.Equal(3, group.TaskCount);
        Assert.Equal("( 3 )", group.CountText);
    }

    [Fact]
    public void The_No_tag_group_collapses_too()
    {
        _tasks.Add("Plain");
        _tasks.Add("Tagged", "", "home");
        _collapse.Toggle(null);

        var plan = Plan();

        Assert.Equal(["home", null], plan.Select(group => group.Tag));
        Assert.False(plan[0].Collapsed);
        Assert.True(plan[1].Collapsed);
        Assert.Equal("( 1 )", plan[1].CountText);
    }

    [Fact]
    public void A_collapsed_group_stays_where_it_is_among_the_groups()
    {
        _tasks.Add("One", "", "charlie");
        _tasks.Add("Two", "", "beta");
        _tasks.Add("Three", "", "alpha");
        _collapse.Toggle("beta");

        Assert.Equal(["alpha", "beta", "charlie"], Plan().Select(group => group.Tag));
    }

    [Fact]
    public void A_task_in_two_groups_is_hidden_only_from_the_collapsed_one()
    {
        _tasks.AddOlder("Buy milk", "", "home", "errand");
        _collapse.Toggle("home");

        var plan = Plan();

        Assert.Equal(["errand", "home"], plan.Select(group => group.Tag));
        Assert.Equal(["Buy milk"], Titles(plan[0]));
        Assert.Empty(plan[1].Rows);
        Assert.Equal(1, plan[1].TaskCount);
    }

    // ---- Up and Down ----

    [Fact]
    public void Down_goes_to_the_next_row_in_the_group_and_then_to_the_first_row_of_the_next_group()
    {
        _tasks.Add("B2", "", "work");
        _tasks.Add("B1", "", "work");
        _tasks.Add("A2", "", "home");
        _tasks.Add("A1", "", "home");

        Assert.Equal("A2", NeighborTitle(At("home", "A1"), 1));
        Assert.Equal("B1", NeighborTitle(At("home", "A2"), 1));
        Assert.Equal("B2", NeighborTitle(At("work", "B1"), 1));
    }

    [Fact]
    public void Up_goes_back_across_groups()
    {
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");

        Assert.Equal("A1", NeighborTitle(At("work", "B1"), -1));
    }

    [Fact]
    public void There_is_no_row_above_the_first_or_below_the_last()
    {
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");

        Assert.Null(NeighborTitle(At("home", "A1"), -1));
        Assert.Null(NeighborTitle(At("work", "B1"), 1));
    }

    [Fact]
    public void Down_skips_the_rows_of_a_collapsed_group()
    {
        _tasks.Add("C1", "", "zoo");
        _tasks.Add("B2", "", "work");
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");
        _collapse.Toggle("work");

        Assert.Equal("C1", NeighborTitle(At("home", "A1"), 1));
    }

    [Fact]
    public void Up_skips_the_rows_of_a_collapsed_group()
    {
        _tasks.Add("C1", "", "zoo");
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");
        _collapse.Toggle("work");

        Assert.Equal("A1", NeighborTitle(At("zoo", "C1"), -1));
    }

    [Fact]
    public void There_is_no_row_below_when_every_later_group_is_collapsed()
    {
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");
        _collapse.Toggle("work");

        Assert.Null(NeighborTitle(At("home", "A1"), 1));
    }

    [Fact]
    public void Down_goes_from_the_last_tagged_group_to_the_No_tag_group()
    {
        _tasks.Add("Plain");
        _tasks.Add("Tagged", "", "home");

        Assert.Equal("Plain", NeighborTitle(At("home", "Tagged"), 1));
    }

    [Fact]
    public void A_task_in_two_groups_has_a_place_in_each_and_the_group_decides_where_Down_goes()
    {
        _tasks.Add("Z", "", "work");
        _tasks.AddOlder("Buy milk", "", "home", "errand");
        _tasks.Add("Next", "", "errand");

        // errand shows "Next" then "Buy milk"; home shows "Buy milk" only.
        Assert.Equal("Buy milk", NeighborTitle(At("errand", "Next"), 1));
        Assert.Equal("Z", NeighborTitle(At("home", "Buy milk"), 1));
    }

    [Fact]
    public void A_row_that_does_not_show_has_no_neighbor()
    {
        _tasks.Add("B1", "", "work");
        _tasks.Add("A1", "", "home");
        _tasks.Add("A2", "", "home");
        _collapse.Toggle("home");

        Assert.Null(NeighborTitle(At("home", "A1"), 1));
        Assert.Null(NeighborTitle(At("home", "A1"), -1));
    }
}
