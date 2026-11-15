using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What following a link to a task does to the list: it clears only the filters that hide the task, chooses the resolved
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

    // The list the filter tests share: a work-tagged source note, and the target with the given tag.
    private string Target(string title, string tag, bool resolved = false)
    {
        _tasks.Add("Unrelated", "", "home");
        _tasks.Add("Other note", "", "work");
        var target = resolved ? _tasks.AddResolved(title, "", tag) : _tasks.Add(title, "", tag);
        _tasks.Add("Plain work", "", "work");
        _filter.ToggleTag("work");
        _filter.SearchText = "note";
        return _tasks.Tasks.LinkAddress(target.Id);
    }

    private string IdOf(string title) => _tasks.Tasks.All.Single(task => task.Title == title).Id;

    [Fact]
    public void A_link_to_a_task_both_the_search_and_the_tag_show_clears_nothing_and_shows_it()
    {
        var address = Target("Work note", "work");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Work note")), outcome);
        Assert.Equal("note", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }

    [Fact]
    public void A_link_to_a_task_only_the_search_hides_clears_the_search_and_keeps_the_tag()
    {
        var address = Target("Call Ana", "work");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Call Ana")), outcome);
        Assert.Equal("", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }

    [Fact]
    public void A_link_to_a_task_only_the_tag_hides_clears_the_tag_and_keeps_the_search()
    {
        var address = Target("Home note", "home");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Home note")), outcome);
        Assert.Equal("note", _filter.SearchText);
        Assert.Null(_filter.TagFilter);
    }

    [Fact]
    public void A_link_to_a_task_both_hide_clears_the_search_and_the_tag()
    {
        var address = Target("Call Ana", "home");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Call Ana")), outcome);
        Assert.Equal("", _filter.SearchText);
        Assert.Null(_filter.TagFilter);
    }

    [Fact]
    public void A_link_to_a_resolved_task_chooses_the_resolved_tasks_and_clears_what_hides_it()
    {
        var address = Target("Old plan", "work", resolved: true);

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Show(IdOf("Old plan")), outcome);
        Assert.True(_content.ShowResolved);
        Assert.Equal("", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }

    [Fact]
    public void A_link_to_an_open_task_leaves_the_resolved_tasks_as_they_are()
    {
        var address = Target("Work note", "work");

        _links.Follow(address);

        Assert.False(_content.ShowResolved);
    }

    [Fact]
    public void A_link_to_a_missing_task_says_it_is_gone_and_clears_no_filter()
    {
        var address = Target("Gone", "home");
        _tasks.Tasks.Delete(IdOf("Gone"));

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Gone, outcome);
        Assert.Equal("note", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }

    [Fact]
    public void A_link_to_the_task_waiting_on_the_Undo_bar_says_it_is_gone_and_clears_no_filter()
    {
        var address = Target("Deleted soon", "home");
        _content.HiddenId = IdOf("Deleted soon");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Gone, outcome);
        Assert.Equal("note", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }

    [Theory]
    [InlineData("file:///C:/secrets.txt")]
    [InlineData("https://example.com/shop")]
    [InlineData("")]
    public void A_link_that_is_not_a_task_is_ignored_and_clears_no_filter(string address)
    {
        Target("Call Ana", "home");

        var outcome = _links.Follow(address);

        Assert.Equal(LinkOutcome.Ignored, outcome);
        Assert.Equal("note", _filter.SearchText);
        Assert.Equal("work", _filter.TagFilter);
    }
}
