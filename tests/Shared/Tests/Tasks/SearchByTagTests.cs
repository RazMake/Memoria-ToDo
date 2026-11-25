using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Searching by text narrowed to the tasks that have every named tag.</summary>
[Unit]
public sealed class SearchByTagTests
{
    private readonly TasksFixture f = new();

    public SearchByTagTests()
    {
        f.Seed(TasksFixture.Id(1), "Buy milk", "a0", tags: ["home"]);
        f.Seed(TasksFixture.Id(2), "Buy paint", "a1", tags: ["Home", "work"]);
        f.Seed(TasksFixture.Id(3), "Call bank", "a2", tags: ["work"], resolved: "2026-02-02T00:00:00.000Z");
        f.Seed(TasksFixture.Id(4), "Buy stamps", "a3");
        f.Seed(TasksFixture.Id(5), "Buy tools", "a4", tags: ["work", "my  home"]);
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    private IReadOnlyList<string> Found(string text, params string[] tags) => f.Tasks.Search(text, tags).Select(t => t.Title).ToList();

    [Fact]
    public void Refuses_an_empty_collection_of_tags()
    {
        Assert.Throws<ArgumentException>(() => f.Tasks.Search("buy", Array.Empty<string>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" \r\n ")]
    public void Refuses_a_tag_that_is_empty_once_normalized(string tag)
    {
        Assert.Throws<ArgumentException>(() => f.Tasks.Search("buy", ["home", tag]));
    }

    [Fact]
    public void Finds_the_tasks_with_the_tag_that_match_the_text()
    {
        Assert.Equal(["Buy milk", "Buy paint"], Found("buy", "home"));
    }

    [Fact]
    public void Finds_a_task_by_a_tag_that_is_not_its_first()
    {
        Assert.Equal(["Buy paint", "Buy tools"], Found("buy", "work"));
    }

    [Fact]
    public void Normalizes_the_tags_it_is_given()
    {
        Assert.Equal(["Buy milk", "Buy paint"], Found("buy", "HOME"));
        Assert.Equal(["Buy tools"], Found("buy", " My  Home "));
    }

    [Fact]
    public void Keeps_only_tasks_that_have_every_named_tag_in_any_place()
    {
        Assert.Equal(["Buy paint"], Found("", "home", "work"));
        Assert.Equal(["Buy paint"], Found("", "work", "home"));
        Assert.Equal(["Buy tools"], Found("", "my-home", "work"));
    }

    [Fact]
    public void Counts_a_repeated_tag_once()
    {
        Assert.Equal(["Buy milk", "Buy paint"], Found("buy", "home", "HOME", " home"));
    }

    [Fact]
    public void Leaves_out_matches_without_the_tag()
    {
        Assert.DoesNotContain("Buy stamps", Found("buy", "home"));
    }

    [Fact]
    public void Leaves_out_tasks_with_the_tag_that_do_not_match_the_text()
    {
        Assert.DoesNotContain("Call bank", Found("buy", "work"));
    }

    [Fact]
    public void Includes_resolved_tasks()
    {
        Assert.Equal(["Call bank"], Found("bank", "work"));
    }

    [Fact]
    public void Finds_nothing_for_a_tag_no_task_has()
    {
        Assert.Empty(Found("buy", "nothing"));
    }

    [Fact]
    public void Finds_nothing_when_one_of_the_named_tags_is_missing()
    {
        Assert.Empty(Found("", "home", "nothing"));
    }
}
