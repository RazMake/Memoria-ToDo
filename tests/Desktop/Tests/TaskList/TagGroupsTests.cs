using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The groups the left side shows: one for each tag that is the first tag of an open task, with those tasks, and a last "No tag"
/// group, built from the tasks the list leaves after the search.
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
    public void Makes_a_group_for_each_tag_in_alphabetical_order()
    {
        _tasks.Add("One", "", "charlie");
        _tasks.Add("Two", "", "beta");
        _tasks.Add("Three", "", "alpha");

        Assert.Equal(["alpha", "beta", "charlie"], Tags(Groups()));
    }

    [Fact]
    public void Makes_a_group_for_each_first_tag_that_differs_only_in_a_letter_with_two_lowercase_forms()
    {
        _tasks.Add("One", "", "σ");
        _tasks.Add("Two", "", "ς");

        Assert.Equal(["ς", "σ"], Tags(Groups()).Order(StringComparer.Ordinal));
        Assert.Equal(["One"], Titles(Group("σ")));
        Assert.Equal(["Two"], Titles(Group("ς")));
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
    public void Shows_a_task_with_several_tags_only_in_its_first_tags_group()
    {
        _tasks.Add("Buy milk", "", "home", "errand");

        var groups = Groups();

        Assert.Equal(["home"], Tags(groups));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Makes_no_group_for_a_tag_that_is_never_the_first_tag_of_a_task()
    {
        _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Call Ana", "", "work", "errand", "home");

        Assert.Equal(["home", "work"], Tags(Groups()));
    }

    [Fact]
    public void Gives_a_tag_a_group_once_a_task_has_it_first()
    {
        _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Errands", "", "errand");

        Assert.Equal(["errand", "home"], Tags(Groups()));
        Assert.Equal(["Errands"], Titles(Group("errand")));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Moves_a_task_to_the_group_of_its_new_first_tag_when_the_first_tag_changes()
    {
        var task = _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Other", "", "home");

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, ["errand", "home"]));

        Assert.Equal(["errand", "home"], Tags(Groups()));
        Assert.Equal(["Buy milk"], Titles(Group("errand")));
        Assert.Equal(["Other"], Titles(Group("home")));
    }

    [Fact]
    public void Moves_a_task_to_the_No_tag_group_when_its_last_tag_is_removed()
    {
        var task = _tasks.Add("Buy milk", "", "home");

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, []));

        Assert.Equal([null], Tags(Groups()));
        Assert.Equal(["Buy milk"], Titles(Group(null)));
    }

    [Fact]
    public void Keeps_a_task_in_its_group_and_its_rank_place_when_a_change_keeps_the_first_tag()
    {
        _tasks.Add("C", "", "home");
        var task = _tasks.Add("B", "", "home", "errand");
        _tasks.Add("A", "", "home");

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, ["home", "milk"]));

        Assert.Equal(["home"], Tags(Groups()));
        Assert.Equal(["A", "B", "C"], Titles(Group("home")));
    }

    [Fact]
    public void Keeps_a_task_in_its_group_when_the_tags_after_the_first_are_reordered()
    {
        var task = _tasks.Add("Buy milk", "", "home", "errand", "milk");

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, ["home", "milk", "errand"]));

        Assert.Equal(["home"], Tags(Groups()));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Finds_a_task_by_a_tag_that_is_not_its_first_and_shows_it_in_its_first_tags_group()
    {
        _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Call Ana", "", "work");
        _filter.SearchText = "errand";

        var groups = Groups();

        Assert.Equal(["home"], Tags(groups));
        Assert.Equal(["Buy milk"], Titles(Group("home")));
    }

    [Fact]
    public void Finds_a_task_by_its_first_tag_too()
    {
        _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Call Ana", "", "work");
        _filter.SearchText = "home";

        Assert.Equal(["home"], Tags(Groups()));
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
    public void Lists_a_task_with_several_tags_at_its_own_rank_place_in_its_first_tags_group()
    {
        _tasks.Add("D", "", "home");
        _tasks.Add("Both", "", "work", "home");
        _tasks.Add("B", "", "work");
        _tasks.Add("A", "", "home");

        Assert.Equal(["A", "D"], Titles(Group("home")));
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
    public void Leaves_a_checked_task_out_of_its_group_at_once_and_counts_it_as_resolved()
    {
        var task = _tasks.Add("Buy milk", "", "errand", "home");
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
