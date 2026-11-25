using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Finding tasks by words, and the task: addresses of links.</summary>
[Unit]
public sealed class SearchAndLinkTests
{
    private readonly TasksFixture f = new();

    private void SeedThree()
    {
        f.Seed(TasksFixture.Id(1), title: "Buy milk", description: "Two litres of oat", rank: "a0", tags: ["shop"]);
        f.Seed(TasksFixture.Id(2), title: "Call the bank", description: "About the loan", rank: "a1", tags: ["phone", "money"]);
        f.Seed(TasksFixture.Id(3), title: "Plan trip", description: "Book MILK delivery", rank: "a2", resolved: "2026-03-04T05:06:07.890Z");
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    private IReadOnlyList<string> Found(string text) => f.Tasks.Search(text).Select(t => t.Title).ToList();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Finds_every_task_for_empty_search_text_in_rank_order(string text)
    {
        SeedThree();

        Assert.Equal(["Buy milk", "Call the bank", "Plan trip"], Found(text));
    }

    [Fact]
    public void Finds_a_task_by_a_word_in_its_title()
    {
        SeedThree();

        Assert.Equal(["Call the bank"], Found("bank"));
    }

    [Fact]
    public void Finds_a_task_by_a_word_in_its_description()
    {
        SeedThree();

        Assert.Equal(["Call the bank"], Found("loan"));
    }

    [Fact]
    public void Finds_a_task_by_a_word_in_its_tags()
    {
        SeedThree();

        Assert.Equal(["Call the bank"], Found("money"));
    }

    [Fact]
    public void Finds_a_task_by_a_word_in_a_tag_that_is_not_its_first()
    {
        SeedThree();

        Assert.Equal(["Call the bank"], Found("money"));
        Assert.Empty(Found("moneyless"));
    }

    [Fact]
    public void Finds_a_task_by_a_word_in_a_hyphenated_tag_read_from_a_file()
    {
        f.SeedAndUse(TasksFixture.Id(1), title: "Task", tags: ["x", "My  Work"]);

        Assert.Equal(["Task"], Found("my-work"));
    }

    [Fact]
    public void Ignores_case_in_the_search_text_and_in_the_task()
    {
        SeedThree();

        Assert.Equal(["Buy milk", "Plan trip"], Found("MiLk"));
    }

    [Fact]
    public void Needs_every_typed_word_to_appear()
    {
        SeedThree();

        Assert.Equal(["Buy milk"], Found("milk oat"));
        Assert.Empty(Found("milk bank"));
    }

    [Fact]
    public void Lets_the_words_appear_in_different_fields()
    {
        SeedThree();

        Assert.Equal(["Buy milk"], Found("shop litres buy"));
    }

    [Fact]
    public void Finds_resolved_tasks_too()
    {
        SeedThree();

        Assert.Contains("Plan trip", Found("trip"));
    }

    [Fact]
    public void Returns_the_matches_in_rank_order()
    {
        f.Seed(TasksFixture.Id(1), title: "milk c", rank: "c");
        f.Seed(TasksFixture.Id(2), title: "milk a", rank: "a");
        f.Seed(TasksFixture.Id(3), title: "milk b", rank: "b");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["milk a", "milk b", "milk c"], Found("milk"));
    }

    [Fact]
    public void Finds_nothing_when_no_task_matches()
    {
        SeedThree();

        Assert.Empty(Found("unicorn"));
    }

    [Fact]
    public void Finds_nothing_when_there_are_no_tasks()
    {
        Assert.Empty(Found("anything"));
    }

    [Fact]
    public void Finds_a_task_added_since_the_last_search()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        Assert.Empty(Found("fresh"));

        f.Tasks.Add(f.Draft("fresh start"));

        Assert.Equal(["fresh start"], Found("fresh"));
    }

    [Fact]
    public void Gives_a_task_link_address_of_task_colon_and_the_id()
    {
        Assert.Equal("task:" + TasksFixture.Id(7), f.Tasks.LinkAddress(TasksFixture.Id(7)));
    }

    [Fact]
    public void Reads_the_task_id_back_from_a_task_link_address()
    {
        Assert.Equal(TasksFixture.Id(7), f.Tasks.TaskIdIn(f.Tasks.LinkAddress(TasksFixture.Id(7))));
    }

    [Theory]
    [InlineData("https://example.com/task:01900000-0000-7000-8000-000000000007")]
    [InlineData("task:")]
    [InlineData("task:not-an-id")]
    [InlineData("task:01900000-0000-7000-8000-000000000007x")]
    [InlineData("task:01900000-0000-7000-8000-000000000007/more")]
    [InlineData("01900000-0000-7000-8000-000000000007")]
    [InlineData("")]
    [InlineData("mailto:someone@example.com")]
    public void Reads_no_task_id_from_an_address_that_is_not_a_task_link(string address)
    {
        Assert.Null(f.Tasks.TaskIdIn(address));
    }
}
