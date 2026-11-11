using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Searching by text narrowed to a tag.</summary>
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
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    private IReadOnlyList<string> Found(string text, string tag) => f.Tasks.Search(text, tag).Select(t => t.Title).ToList();

    [Theory]
    [InlineData("")]
    [InlineData("home\nwork")]
    [InlineData("home\r\nwork")]
    public void Refuses_an_empty_or_multi_line_tag(string tag)
    {
        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Search("buy", tag));
    }

    [Fact]
    public void Finds_the_tasks_with_the_tag_that_match_the_text()
    {
        Assert.Equal(["Buy milk", "Buy paint"], Found("buy", "home"));
    }

    [Fact]
    public void Ignores_the_case_of_the_tag()
    {
        Assert.Equal(["Buy milk", "Buy paint"], Found("buy", "HOME"));
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
    public void Gives_the_same_result_as_a_plain_search_followed_by_the_tag_filter()
    {
        var expected = f.Tasks.Search("buy").Where(t => t.Tags.Contains("home", StringComparer.OrdinalIgnoreCase)).ToList();

        Assert.Equal(expected, f.Tasks.Search("buy", "home"));
    }
}
