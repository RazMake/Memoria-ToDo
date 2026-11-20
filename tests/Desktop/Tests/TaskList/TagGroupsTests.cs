using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The groups the left side shows: one for each tag with the open tasks that carry it, and a last "No tag" group, built
/// from the tasks the list leaves after the search.
/// </summary>
[Unit]
public class TagGroupsTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter;

    public TagGroupsTests() => _filter = new ListFilter(_tasks.Contacts);

    private ListSnapshot Snapshot() => new ListContent(_tasks.Tasks, _filter).Build(null, null);

    private IReadOnlyList<TaskGroup> Groups() => TagGroups.Of(Snapshot().Open, _order);

    private readonly FakeTagOrder _order = new();

    private static IReadOnlyList<string?> Tags(IEnumerable<TaskGroup> groups) => [.. groups.Select(group => group.Tag)];

    private static IReadOnlyList<string> Titles(TaskGroup group) => [.. group.Tasks.Select(task => task.Title)];

    private TaskGroup Group(string? tag) => Groups().Single(group => group.Tag == tag);

    [Fact]
    public void Makes_a_group_for_each_tag_in_alphabetical_order_ignoring_case()
    {
        _tasks.Add("One", "", "charlie");
        _tasks.Add("Two", "", "Beta");
        _tasks.Add("Three", "", "alpha");

        Assert.Equal(["alpha", "Beta", "charlie"], Tags(Groups()));
    }

    [Fact]
    public void Puts_the_tasks_without_a_tag_in_a_last_group_named_by_a_null_tag()
    {
        _tasks.Add("Plain");
        _tasks.Add("Tagged", "", "zebra");

        var groups = Groups();

        Assert.Equal(["zebra", null], Tags(groups));
        Assert.Equal(["Plain"], Titles(groups[^1]));
    }

    [Fact]
    public void Shows_a_task_with_two_tags_in_both_groups()
    {
        _tasks.AddOlder("Buy milk", "", "home", "errand");

        var groups = Groups();

        Assert.Equal(["errand", "home"], Tags(groups));
        Assert.Equal(["Buy milk"], Titles(Group("errand")));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Keeps_each_groups_tasks_in_rank_order()
    {
        _tasks.Add("C", "", "work");
        _tasks.Add("B", "", "home");
        _tasks.Add("A", "", "work");

        Assert.Equal(["A", "C"], Titles(Group("work")));
    }

    [Fact]
    public void Lists_a_task_with_two_tags_at_its_own_rank_place_in_each_group()
    {
        _tasks.Add("D", "", "home");
        _tasks.AddOlder("Both", "", "work", "home");
        _tasks.Add("B", "", "work");
        _tasks.Add("A", "", "home");

        Assert.Equal(["A", "Both", "D"], Titles(Group("home")));
        Assert.Equal(["B", "Both"], Titles(Group("work")));
    }

    [Fact]
    public void Leaves_out_a_group_with_no_task_to_show()
    {
        _tasks.Add("Open", "", "work");
        _tasks.AddResolved("Done", "", "home");

        Assert.Equal(["work"], Tags(Groups()));
    }

    [Fact]
    public void Makes_no_group_when_there_is_no_open_task()
    {
        _tasks.AddResolved("Done", "", "home");

        Assert.Empty(Groups());
    }

    [Fact]
    public void Leaves_a_checked_task_out_of_every_group_at_once_and_counts_it_as_resolved()
    {
        var task = _tasks.AddOlder("Buy milk", "", "home", "errand");
        _tasks.Add("Other", "", "home");

        _tasks.Tasks.Resolve(task.Id);

        Assert.Equal(["home"], Tags(Groups()));
        Assert.Equal(["Other"], Titles(Group("home")));
        Assert.Equal(1, Snapshot().ResolvedCount);
    }

    [Fact]
    public void Hides_the_groups_with_no_task_matching_the_search()
    {
        _tasks.Add("Buy milk", "", "home");
        _tasks.Add("Call Ana", "", "work");
        _tasks.Add("Buy tea");
        _filter.SearchText = "buy";

        var groups = Groups();

        Assert.Equal(["home", null], Tags(groups));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Shows_only_the_matching_tasks_of_a_group_when_searching()
    {
        _tasks.Add("Buy tea", "", "home");
        _tasks.Add("Buy milk", "", "home");
        _tasks.Add("Wash car", "", "home");
        _filter.SearchText = "buy";

        Assert.Equal(["Buy milk", "Buy tea"], Titles(Group("home")));
    }

    [Fact]
    public void Puts_the_groups_in_the_tag_orders_order()
    {
        _order.Seed("work", "home");
        _tasks.Add("One", "", "home");
        _tasks.Add("Two", "", "work");

        Assert.Equal(["work", "home"], Tags(Groups()));
    }

    [Fact]
    public void Puts_a_tag_with_no_place_on_top_of_the_placed_ones()
    {
        _order.Seed("work", "home");
        _tasks.Add("One", "", "home");
        _tasks.Add("Two", "", "work");
        _tasks.Add("Three", "", "alpha");

        Assert.Equal(["alpha", "work", "home"], Tags(Groups()));
    }

    [Fact]
    public void Keeps_the_No_tag_group_last_whatever_the_tag_order_says()
    {
        _order.Seed("work", "home");
        _tasks.Add("Plain");
        _tasks.Add("One", "", "home");
        _tasks.Add("Two", "", "work");

        Assert.Equal(["work", "home", null], Tags(Groups()));
    }

    [Fact]
    public void Keeps_the_order_of_the_groups_that_show_when_a_group_is_hidden_by_the_search()
    {
        _order.Seed("work", "play", "home");
        _tasks.Add("Buy one", "", "home");
        _tasks.Add("Call two", "", "play");
        _tasks.Add("Buy three", "", "work");
        _filter.SearchText = "buy";

        Assert.Equal(["work", "home"], Tags(Groups()));
    }
}
