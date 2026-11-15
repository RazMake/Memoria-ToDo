using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What the search text and the one chosen tag leave of the tasks, and when choosing a tag clears it.</summary>
[Unit]
public class ListFilterTests
{
    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter = new();
    private int _changed;

    public ListFilterTests()
    {
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
        Assert.Null(_filter.TagFilter);
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
    public void Choosing_a_tag_shows_only_the_tasks_with_that_tag()
    {
        _tasks.Add("B", "", "home");
        _tasks.Add("A", "", "work");

        _filter.ToggleTag("work");

        Assert.Equal("work", _filter.TagFilter);
        Assert.Equal(["A"], Titles());
    }

    [Fact]
    public void Choosing_the_same_tag_again_clears_it()
    {
        _tasks.Add("A", "", "work");
        _filter.ToggleTag("work");

        _filter.ToggleTag("work");

        Assert.Null(_filter.TagFilter);
        Assert.Equal(["A"], Titles());
    }

    [Fact]
    public void Choosing_the_chosen_tag_in_other_letters_clears_it_too()
    {
        _tasks.Add("A", "", "work");
        _filter.ToggleTag("work");

        _filter.ToggleTag("Work");

        Assert.Null(_filter.TagFilter);
    }

    [Fact]
    public void Says_a_tag_is_chosen_in_any_letters_and_no_other_tag_is()
    {
        _filter.ToggleTag("work");

        Assert.True(_filter.IsChosen("WORK"));
        Assert.False(_filter.IsChosen("home"));
    }

    [Fact]
    public void Choosing_another_tag_replaces_the_chosen_one_since_one_tag_filters_at_a_time()
    {
        _tasks.Add("B", "", "home");
        _tasks.Add("A", "", "work");
        _filter.ToggleTag("work");

        _filter.ToggleTag("home");

        Assert.Equal("home", _filter.TagFilter);
        Assert.Equal(["B"], Titles());
    }

    [Fact]
    public void Search_combines_with_the_tag()
    {
        _tasks.Add("Buy milk", "", "home");
        _tasks.Add("Buy tea", "", "work");
        _tasks.Add("Call Ana", "", "work");
        _filter.SearchText = "buy";

        _filter.ToggleTag("work");

        Assert.Equal(["Buy tea"], Titles());
    }

    [Fact]
    public void Clearing_the_tag_keeps_the_search()
    {
        _tasks.Add("Buy milk", "", "home");
        _tasks.Add("Buy tea", "", "work");
        _filter.SearchText = "buy";
        _filter.ToggleTag("work");

        _filter.ClearTag();

        Assert.Null(_filter.TagFilter);
        Assert.Equal("buy", _filter.SearchText);
        Assert.Equal(["Buy tea", "Buy milk"], Titles());
    }

    [Fact]
    public void Tells_the_list_to_refresh_when_the_search_or_the_tag_changes()
    {
        _filter.SearchText = "milk";
        var afterSearch = _changed;
        _filter.ToggleTag("work");
        var afterTag = _changed;
        _filter.ClearTag();

        Assert.True(afterSearch > 0);
        Assert.True(afterTag > afterSearch);
        Assert.True(_changed > afterTag);
    }
}
