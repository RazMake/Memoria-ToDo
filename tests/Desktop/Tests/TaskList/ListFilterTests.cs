using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What the search text leaves of the tasks, and when it tells the list to refresh. The list has no tag filter.</summary>
[Unit]
public class ListFilterTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter;
    private int _changed;

    public ListFilterTests()
    {
        _filter = new ListFilter(_tasks.Contacts);
        _filter.Changed += (_, _) => _changed++;
    }

    private IReadOnlyList<string> Titles() => [.. _filter.Matching(_tasks.Tasks).Select(task => task.Title)];

    [Fact]
    public void Shows_every_task_while_nothing_filters()
    {
        _tasks.Add("B");
        _tasks.Add("A");

        Assert.Equal(["A", "B"], Titles());
        Assert.Equal("", _filter.SearchText);
    }

    [Fact]
    public void Search_matches_titles_descriptions_and_tags_ignoring_case()
    {
        _tasks.Add("Buy MILK");
        _tasks.Add("Call Ana", "about the milk");
        _tasks.Add("Wash car", "", "Milk");
        _tasks.Add("Other");

        _filter.SearchText = "milk";

        Assert.Equal(["Wash car", "Call Ana", "Buy MILK"], Titles());
    }

    [Fact]
    public void Clearing_the_search_shows_every_task_again()
    {
        _tasks.Add("Buy milk");
        _tasks.Add("Call Ana");
        _filter.SearchText = "milk";

        _filter.ClearSearch();

        Assert.Equal(["Call Ana", "Buy milk"], Titles());
        Assert.Equal("", _filter.SearchText);
    }

    [Fact]
    public void Searching_with_an_empty_text_shows_every_task_again()
    {
        _tasks.Add("Buy milk");
        _tasks.Add("Call Ana");
        _filter.SearchText = "milk";

        _filter.SearchText = "";

        Assert.Equal(["Call Ana", "Buy milk"], Titles());
    }

    [Fact]
    public void Tells_the_list_to_refresh_when_the_search_changes()
    {
        _filter.SearchText = "milk";

        Assert.Equal(1, _changed);
    }

    [Fact]
    public void Tells_the_list_nothing_when_the_search_is_set_to_the_text_it_already_has()
    {
        _filter.SearchText = "milk";

        _filter.SearchText = "milk";

        Assert.Equal(1, _changed);
    }
}
