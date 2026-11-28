using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The selected task, which the right side shows: none when the list opens, kept while the list still shows the task, and
/// cleared when the task is deleted, filtered or searched out, or gone after a sync.
/// </summary>
[Unit]
public class ListSelectionTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter;
    private readonly ListContent _content;
    private readonly ListSelection _selection = new();

    public ListSelectionTests()
    {
        _filter = new ListFilter(_tasks.Contacts);
        _content = new ListContent(_tasks.Tasks, _filter);
    }

    private ListSnapshot Snapshot() => _content.Build(null, null);

    [Fact]
    public void Selects_nothing_when_the_list_opens()
    {
        _tasks.Add("Buy milk");

        Assert.Null(_selection.SelectedId);
        Assert.Null(_selection.Update(Snapshot()));
    }

    [Fact]
    public void Returns_the_selected_task_as_the_list_shows_it()
    {
        var task = _tasks.Add("Buy milk", "Oat milk");
        _selection.Select(task.Id);

        var selected = _selection.Update(Snapshot());

        Assert.Equal(task.Id, selected?.Id);
        Assert.Equal("Oat milk", selected?.Description);
        Assert.Equal(task.Id, _selection.SelectedId);
    }

    [Fact]
    public void Selecting_another_task_replaces_the_selection()
    {
        var first = _tasks.Add("First");
        var second = _tasks.Add("Second");
        _selection.Select(first.Id);

        _selection.Select(second.Id);

        Assert.False(_selection.IsSelected(first.Id));
        Assert.True(_selection.IsSelected(second.Id));
        Assert.Equal(second.Id, _selection.Update(Snapshot())?.Id);
    }

    [Fact]
    public void A_task_with_several_tags_is_selected_in_its_one_row()
    {
        var task = _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Other", "", "home");
        _selection.Select(task.Id);
        var groups = TagGroups.Of(Snapshot().Open, new Memoria.ToDo.Shared.Fakes.FakeTagOrder());

        var selectedRows = groups.SelectMany(group => group.Tasks).Where(row => _selection.IsSelected(row.Id)).ToList();

        Assert.Equal(["Buy milk"], selectedRows.Select(row => row.Title));
    }

    [Fact]
    public void Keeps_the_task_selected_when_its_first_tag_changes_and_it_moves_to_another_group()
    {
        var task = _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Other", "", "home");
        _selection.Select(task.Id);
        _selection.Update(Snapshot());

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, ["errand", "home"]));
        var selected = _selection.Update(Snapshot());
        var groups = TagGroups.Of(Snapshot().Open, new Memoria.ToDo.Shared.Fakes.FakeTagOrder());

        Assert.Equal(task.Id, selected?.Id);
        Assert.Equal(["errand", "home"], selected?.Tags);
        Assert.Equal("errand", selected?.GroupTag);
        Assert.Equal(task.Id, _selection.SelectedId);
        Assert.Equal(["Buy milk"], groups.Single(group => group.Tag == "errand").Tasks.Select(row => row.Title));
    }

    [Fact]
    public void Keeps_the_task_selected_when_its_last_tag_is_removed_and_it_moves_to_No_tag()
    {
        var task = _tasks.Add("Buy milk", "", "home");
        _selection.Select(task.Id);
        _selection.Update(Snapshot());

        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, task.Description, []));

        Assert.Equal(task.Id, _selection.Update(Snapshot())?.Id);
    }

    [Fact]
    public void Keeps_the_task_selected_when_the_search_matches_it_only_by_a_tag_that_is_not_its_first()
    {
        var task = _tasks.Add("Buy milk", "", "home", "errand");
        _tasks.Add("Other", "", "work");
        _selection.Select(task.Id);
        _filter.SearchText = "errand";

        Assert.Equal(task.Id, _selection.Update(Snapshot())?.Id);
    }

    [Fact]
    public void Keeps_the_selection_through_a_refresh_that_changes_the_task_and_the_others()
    {
        var task = _tasks.Add("Buy milk", "Oat milk");
        _selection.Select(task.Id);
        _selection.Update(Snapshot());

        _tasks.Add("Arrived from sync");
        _tasks.Tasks.SaveEdit(task, new TaskDraft(task.Id, "Buy oat milk", "Two litres", task.Tags));
        var selected = _selection.Update(Snapshot());

        Assert.Equal(task.Id, selected?.Id);
        Assert.Equal("Buy oat milk", selected?.Title);
        Assert.Equal("Two litres", selected?.Description);
    }

    [Fact]
    public void Clears_the_selection_when_the_task_is_deleted()
    {
        var task = _tasks.Add("Buy milk");
        _selection.Select(task.Id);
        _selection.Update(Snapshot());

        _tasks.Tasks.Delete(task.Id);

        Assert.Null(_selection.Update(Snapshot()));
        Assert.Null(_selection.SelectedId);
        Assert.False(_selection.IsSelected(task.Id));
    }

    [Fact]
    public void Clears_the_selection_when_the_task_waits_on_the_Undo_bar_to_be_deleted()
    {
        var task = _tasks.Add("Buy milk");
        _selection.Select(task.Id);
        _content.HiddenId = task.Id;

        Assert.Null(_selection.Update(Snapshot()));
    }

    [Fact]
    public void Clears_the_selection_when_the_search_hides_the_task_and_does_not_bring_it_back()
    {
        var task = _tasks.Add("Buy milk");
        _tasks.Add("Call Ana");
        _selection.Select(task.Id);
        _filter.SearchText = "ana";

        Assert.Null(_selection.Update(Snapshot()));

        _filter.ClearSearch();

        Assert.Null(_selection.Update(Snapshot()));
    }

    [Fact]
    public void Selects_a_resolved_task_while_the_resolved_tasks_show()
    {
        var task = _tasks.AddResolved("Done");
        _content.ShowResolved = true;
        _selection.Select(task.Id);

        Assert.Equal(task.Id, _selection.Update(Snapshot())?.Id);
    }

    [Fact]
    public void Following_a_link_to_a_task_a_search_hides_clears_the_search_and_selects_the_task()
    {
        var target = _tasks.Add("Home note");
        _tasks.Add("Other");
        _filter.SearchText = "other";

        var outcome = new ListLinks(_tasks.Tasks, _filter, _content).Follow(_tasks.Tasks.LinkAddress(target.Id));
        _selection.Select(outcome.TaskId!);
        var selected = _selection.Update(Snapshot());

        Assert.Equal(LinkOutcome.Kind.Show, outcome.What);
        Assert.Equal("", _filter.SearchText);
        Assert.Equal(target.Id, selected?.Id);
    }

    [Fact]
    public void Following_a_link_to_a_resolved_task_shows_the_resolved_tasks_and_selects_the_task()
    {
        var target = _tasks.AddResolved("Done note");
        _tasks.Add("Open");

        var outcome = new ListLinks(_tasks.Tasks, _filter, _content).Follow(_tasks.Tasks.LinkAddress(target.Id));
        _selection.Select(outcome.TaskId!);
        var snapshot = Snapshot();

        Assert.True(snapshot.ShowResolved);
        Assert.Equal(target.Id, _selection.Update(snapshot)?.Id);
    }
}
