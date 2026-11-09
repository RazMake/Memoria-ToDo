using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The tags in use, tag spelling, and suggestions.</summary>
[Unit]
public sealed class TagTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Has_no_tags_in_use_when_there_are_no_tasks()
    {
        Assert.Empty(f.Tasks.TagsInUse());
    }

    [Fact]
    public void Counts_how_many_tasks_have_each_tag()
    {
        f.SeedTagsAndUse(["work", "home"], ["work"], ["work", "errand"]);

        var use = f.Tasks.TagsInUse();

        Assert.Equal(new TagUse("work", 3), use[0]);
        Assert.Contains(new TagUse("home", 1), use);
        Assert.Contains(new TagUse("errand", 1), use);
    }

    [Fact]
    public void Counts_resolved_tasks_too()
    {
        f.Seed(TasksFixture.Id(1), tags: ["done"], resolved: "2026-03-04T05:06:07.890Z");
        f.Seed(TasksFixture.Id(2), rank: "a1", tags: ["done"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal([new TagUse("done", 2)], f.Tasks.TagsInUse());
    }

    [Fact]
    public void Lists_the_most_used_tag_first_and_ties_in_alphabetical_order_ignoring_case()
    {
        f.SeedTagsAndUse(["pear", "banana"], ["apple", "banana"], ["Cherry"], ["banana"], ["Cherry"]);

        Assert.Equal(
            [new TagUse("banana", 3), new TagUse("Cherry", 2), new TagUse("apple", 1), new TagUse("pear", 1)],
            f.Tasks.TagsInUse());
    }

    [Fact]
    public void Counts_tags_that_differ_only_in_case_as_one_tag()
    {
        f.SeedTagsAndUse(["Work"], ["Work"], ["work"]);

        Assert.Equal([new TagUse("Work", 3)], f.Tasks.TagsInUse());
    }

    [Fact]
    public void Spells_a_tag_as_most_of_the_tasks_spell_it()
    {
        f.SeedTagsAndUse(["work"], ["Work"], ["Work"]);

        Assert.Equal("Work", Assert.Single(f.Tasks.TagsInUse()).Tag);
    }

    [Fact]
    public void Spells_a_tag_as_the_first_in_alphabetical_order_on_a_tie()
    {
        f.SeedTagsAndUse(["beta"], ["Beta"]);

        Assert.Equal("Beta", Assert.Single(f.Tasks.TagsInUse()).Tag);
    }

    [Fact]
    public void Updates_the_tags_in_use_when_a_task_is_added()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.Add(f.Draft("T", "", "fresh"));

        Assert.Equal([new TagUse("fresh", 1)], f.Tasks.TagsInUse());
    }

    [Fact]
    public void Adds_a_typed_tag_at_the_end_of_the_tags()
    {
        Assert.Equal(["a", "b"], f.Tasks.WithTag(["a"], "b"));
    }

    [Fact]
    public void Adds_the_typed_tag_trimmed()
    {
        Assert.Equal(["work"], f.Tasks.WithTag([], "  work  "));
    }

    [Fact]
    public void Adds_the_typed_tag_in_the_spelling_of_the_matching_tag_in_use()
    {
        f.SeedTagsAndUse(["Work"]);

        Assert.Equal(["Work"], f.Tasks.WithTag([], "wORK"));
    }

    [Fact]
    public void Returns_the_tags_unchanged_when_the_tag_is_already_there_ignoring_case()
    {
        Assert.Equal(["Work", "home"], f.Tasks.WithTag(["Work", "home"], "work"));
    }

    [Fact]
    public void Does_not_change_the_tags_it_was_given()
    {
        string[] tags = ["a"];

        f.Tasks.WithTag(tags, "b");

        Assert.Equal(["a"], tags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_to_add_a_tag_that_is_empty_or_has_a_line_break(string typed)
    {
        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.WithTag(["a"], typed));
    }

    [Fact]
    public void Suggests_the_tags_in_use_that_start_with_the_typed_text_ignoring_case()
    {
        f.SeedTagsAndUse(["Work", "wonder"], ["Work"], ["home"]);

        Assert.Equal(["Work", "wonder"], f.Tasks.SuggestTags("WO", []));
    }

    [Fact]
    public void Suggests_nothing_for_empty_typed_text()
    {
        f.SeedTagsAndUse(["Work"], ["home"]);

        Assert.Empty(f.Tasks.SuggestTags("", []));
        Assert.Empty(f.Tasks.SuggestTags("   ", []));
    }

    [Fact]
    public void Trims_the_typed_text_before_matching()
    {
        f.SeedTagsAndUse(["Work"]);

        Assert.Equal(["Work"], f.Tasks.SuggestTags("  wo ", []));
    }

    [Fact]
    public void Suggests_only_tags_that_start_with_the_text_not_ones_that_contain_it()
    {
        f.SeedTagsAndUse(["network"]);

        Assert.Empty(f.Tasks.SuggestTags("work", []));
    }

    [Fact]
    public void Leaves_out_the_tags_the_task_already_has_ignoring_case()
    {
        f.SeedTagsAndUse(["Work", "wonder"]);

        Assert.Equal(["wonder"], f.Tasks.SuggestTags("wo", ["work"]));
    }

    [Fact]
    public void Suggests_in_the_order_of_the_tags_in_use()
    {
        f.SeedTagsAndUse(["wa", "wb"], ["wb"], ["wc"], ["wc"], ["wc"]);

        Assert.Equal(["wc", "wb", "wa"], f.Tasks.SuggestTags("w", []));
    }

    [Fact]
    public void Suggests_the_spelling_most_tasks_use()
    {
        f.SeedTagsAndUse(["work"], ["Work"], ["Work"]);

        Assert.Equal(["Work"], f.Tasks.SuggestTags("wo", []));
    }

    [Fact]
    public void Suggests_nothing_when_no_tag_matches()
    {
        f.SeedTagsAndUse(["Work"]);

        Assert.Empty(f.Tasks.SuggestTags("zzz", []));
    }
}

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

    [Fact]
    public void Finds_every_task_for_empty_search_text_in_rank_order()
    {
        SeedThree();

        Assert.Equal(["Buy milk", "Call the bank", "Plan trip"], Found(""));
    }

    [Fact]
    public void Finds_every_task_for_search_text_of_only_spaces()
    {
        SeedThree();

        Assert.Equal(["Buy milk", "Call the bank", "Plan trip"], Found("   "));
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
