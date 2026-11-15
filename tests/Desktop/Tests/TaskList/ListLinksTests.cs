using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What following a link to a task does to the list: it clears the search when it hides the task, chooses the resolved
/// tasks for a resolved one, and says when the task is gone or is the one waiting on the Undo bar.
/// </summary>
[Unit]
public class ListLinksTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter = new();
    private readonly ListContent _content;
    private readonly ListLinks _links;

    public ListLinksTests()
    {
        _content = new ListContent(_tasks.Tasks, _filter);
        _links = new ListLinks(_tasks.Tasks, _filter, _content);
    }

    // The list the search tests share: the search "note" shows "Other note" and whichever target has "note" in its title.
    private string Target(string title, bool resolved = false)
    {
        _tasks.Add("Unrelated");
        _tasks.Add("Other note");
        var target = resolved ? _tasks.AddResolved(title) : _tasks.Add(title);
        _tasks.Add("Plain work");
        _filter.SearchText = "note";
        return _tasks.Tasks.LinkAddress(target.Id);
    }

    private string IdOf(string title) => _tasks.Tasks.All.Single(task => task.Title == title).Id;

    [Fact]
    public void A_link_to_a_task_the_search_shows_clears_nothing_and_shows_it()
    {
        var address = Target("Work note");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Work note")), outcome);
        Assert.Equal("note", _filter.SearchText);
    }

    [Fact]
    public void A_link_to_a_task_the_search_hides_clears_the_search()
    {
        var address = Target("Call Ana");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Call Ana")), outcome);
        Assert.Equal("", _filter.SearchText);
    }

    [Fact]
    public void A_link_to_a_resolved_task_chooses_the_resolved_tasks_and_clears_what_hides_it()
    {
        var address = Target("Old plan", resolved: true);

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Old plan")), outcome);
        Assert.True(_content.ShowResolved);
        Assert.Equal("", _filter.SearchText);
    }

    [Fact]
    public void A_link_to_an_open_task_leaves_the_resolved_tasks_as_they_are()
    {
        var address = Target("Work note");

        _links.Follow(address);

        Assert.False(_content.ShowResolved);
    }

    [Fact]
    public void A_link_to_a_missing_task_says_it_is_gone_and_clears_no_search()
    {
        var address = Target("Gone");
        _tasks.Tasks.Delete(IdOf("Gone"));

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Gone, outcome);
        Assert.Equal("note", _filter.SearchText);
    }

    [Fact]
    public void A_link_to_the_task_waiting_on_the_Undo_bar_says_it_is_gone_and_clears_no_search()
    {
        var address = Target("Deleted soon");
        _content.HiddenId = IdOf("Deleted soon");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Gone, outcome);
        Assert.Equal("note", _filter.SearchText);
    }

    [Theory]
    [InlineData("file:///C:/secrets.txt")]
    [InlineData("https://example.com/shop")]
    [InlineData("")]
    public void A_link_that_is_not_a_task_is_ignored_and_clears_no_search(string address)
    {
        Target("Call Ana");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Ignored, outcome);
        Assert.Equal("note", _filter.SearchText);
    }
}
