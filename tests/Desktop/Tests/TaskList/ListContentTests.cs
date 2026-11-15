using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What the list shows: the open tasks and the resolved ones the filter leaves, without the task waiting on the Undo bar,
/// and the task being edited, which stays where it was even when it is gone, filtered out, or resolved elsewhere.
/// </summary>
[Unit]
public class ListContentTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter = new();
    private readonly ListContent _content;

    public ListContentTests()
    {
        _content = new ListContent(_tasks.Tasks, _filter);
    }

    private ListSnapshot Build(TaskItem? editing = null, ListSnapshot? previous = null) =>
        _content.Build(editing, previous);

    private static IReadOnlyList<string> Titles(IEnumerable<TaskItem> tasks) => [.. tasks.Select(task => task.Title)];

    [Fact]
    public void Shows_the_open_tasks_in_rank_order_and_no_resolved_one_until_they_are_chosen()
    {
        _tasks.Add("C");
        _tasks.Add("B");
        _tasks.AddResolved("Done");
        _tasks.Add("A");

        var snapshot = Build();

        Assert.Equal(["A", "B", "C"], Titles(snapshot.Open));
        Assert.Equal(["Done"], Titles(snapshot.Resolved));
        Assert.Equal(["A", "B", "C"], Titles(snapshot.Shown));
        Assert.Equal(1, snapshot.ResolvedCount);
    }

    [Fact]
    public void Shows_the_resolved_tasks_in_rank_order_after_the_open_ones_once_chosen()
    {
        _tasks.AddResolved("Older");
        _tasks.AddResolved("Newer");
        _tasks.Add("Open");
        _content.ShowResolved = true;

        var snapshot = Build();

        Assert.Equal(["Open", "Newer", "Older"], Titles(snapshot.Shown));
        Assert.True(snapshot.ShowResolved);
    }

    [Fact]
    public void Counts_no_resolved_tasks_while_nothing_is_resolved()
    {
        _tasks.Add("Open");

        Assert.Equal(0, Build().ResolvedCount);
    }

    [Fact]
    public void Leaves_out_the_task_waiting_on_the_Undo_bar()
    {
        _tasks.Add("B");
        var a = _tasks.Add("A");
        _content.HiddenId = a.Id;

        Assert.Equal(["B"], Titles(Build().Open));

        _content.HiddenId = null;

        Assert.Equal(["A", "B"], Titles(Build().Open));
    }

    [Fact]
    public void Shows_only_the_tasks_the_search_leaves_and_counts_only_the_resolved_ones_it_leaves()
    {
        _tasks.Add("Open home");
        _tasks.Add("Open work");
        _tasks.AddResolved("Done home");
        _tasks.AddResolved("Done work");
        _filter.SearchText = "work";

        var snapshot = Build();

        Assert.Equal(["Open work"], Titles(snapshot.Open));
        Assert.Equal(["Done work"], Titles(snapshot.Resolved));
        Assert.Equal(1, snapshot.ResolvedCount);
    }

    [Fact]
    public void Counts_no_resolved_tasks_when_none_matches_the_search()
    {
        _tasks.AddResolved("Done");
        _tasks.Add("Open");
        _filter.SearchText = "open";

        Assert.Equal(0, Build().ResolvedCount);
    }

    [Fact]
    public void Shows_a_task_added_elsewhere_at_the_top()
    {
        _tasks.Add("Old");
        Build();

        _tasks.Add("New");

        Assert.Equal(["New", "Old"], Titles(Build().Open));
    }

    [Fact]
    public void Moves_a_task_resolved_elsewhere_to_the_resolved_ones_and_back_when_reopened()
    {
        var task = _tasks.Add("Task");
        _tasks.Add("Other");

        _tasks.Tasks.Resolve(task.Id);
        var resolved = Build();
        _tasks.Tasks.Reopen(task.Id);
        var reopened = Build();

        Assert.Equal(["Other"], Titles(resolved.Open));
        Assert.Equal(["Task"], Titles(resolved.Resolved));
        Assert.Equal(["Task", "Other"], Titles(reopened.Open).OrderByDescending(title => title));
    }

    [Fact]
    public void Keeps_the_task_being_edited_in_its_place_when_the_filter_now_hides_it()
    {
        _tasks.Add("C");
        var b = _tasks.Add("B");
        _tasks.Add("A");
        var before = Build();
        _filter.SearchText = "zzz";

        var snapshot = Build(b, before);

        Assert.Equal(["B"], Titles(snapshot.Open));
    }

    [Fact]
    public void Keeps_the_task_being_edited_at_its_old_position_among_the_others_the_search_leaves()
    {
        _tasks.Add("C work");
        var b = _tasks.Add("B home");
        _tasks.Add("A work");
        var before = Build();
        _filter.SearchText = "work";

        var snapshot = Build(b, before);

        Assert.Equal(["A work", "B home", "C work"], Titles(snapshot.Open));
    }

    [Fact]
    public void Keeps_the_task_being_edited_when_it_was_deleted_elsewhere()
    {
        var task = _tasks.Add("Editing");
        _tasks.Add("Other");
        var before = Build();
        _tasks.Tasks.Delete(task.Id);

        var snapshot = Build(task, before);

        Assert.Equal(["Other", "Editing"], Titles(snapshot.Open));
    }

    [Fact]
    public void Keeps_the_task_being_edited_open_when_it_was_resolved_elsewhere()
    {
        var task = _tasks.Add("Editing");
        _tasks.Tasks.Resolve(task.Id);
        var resolved = _tasks.Tasks.All.Single(t => t.Id == task.Id);

        var snapshot = Build(resolved);

        Assert.Equal(["Editing"], Titles(snapshot.Open));
        Assert.Empty(snapshot.Resolved);
    }
}
