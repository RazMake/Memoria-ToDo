using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The tags in use, adding a tag, and suggestions.</summary>
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
    public void Lists_the_most_used_tag_first_and_ties_in_ordinal_order()
    {
        f.SeedTagsAndUse(["pear", "banana"], ["apple", "banana"], ["cherry"], ["banana"], ["cherry"], ["Zed", "zed2"]);

        Assert.Equal(
            [new TagUse("banana", 3), new TagUse("cherry", 2), new TagUse("apple", 1), new TagUse("pear", 1), new TagUse("zed", 1), new TagUse("zed2", 1)],
            f.Tasks.TagsInUse());
    }

    [Fact]
    public void Sorts_tied_tags_ordinally_not_by_culture()
    {
        f.SeedTagsAndUse(["b", "a", "-", "_", "1"]);

        Assert.Equal(["-", "1", "_", "a", "b"], f.Tasks.TagsInUse().Select(u => u.Tag));
    }

    [Fact]
    public void Counts_tags_that_differ_only_in_case_or_spacing_as_one_normalized_tag()
    {
        f.SeedTagsAndUse(["Work"], ["Work"], ["work"], ["my  work"], ["MY-WORK"]);

        Assert.Equal([new TagUse("work", 3), new TagUse("my-work", 2)], f.Tasks.TagsInUse());
    }

    [Fact]
    public void Counts_every_tag_of_every_task_not_only_the_first()
    {
        f.SeedTagsAndUse(["a", "shared"], ["b", "shared"], ["shared", "c"]);

        var use = f.Tasks.TagsInUse();

        Assert.Equal(new TagUse("shared", 3), use[0]);
        Assert.Equal(["a", "b", "c", "shared"], use.Select(u => u.Tag).Order());
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
    public void Keeps_the_group_tag_when_a_tag_is_added_to_a_task_that_has_one()
    {
        var tags = f.Tasks.WithTag(["first", "second"], "third");

        Assert.Equal(["first", "second", "third"], tags);
    }

    [Fact]
    public void Adds_the_typed_tag_normalized()
    {
        Assert.Equal(["work"], f.Tasks.WithTag([], "  work  "));
        Assert.Equal(["my-work"], f.Tasks.WithTag([], " My \t Work "));
        Assert.Equal(["to-do"], f.Tasks.WithTag([], "to\ndo"));
    }

    [Fact]
    public void Adds_the_typed_tag_in_lowercase_even_when_a_tag_in_use_has_capitals()
    {
        f.SeedTagsAndUse(["Work"]);

        Assert.Equal(["work"], f.Tasks.WithTag([], "wORK"));
    }

    [Fact]
    public void Returns_the_tags_unchanged_when_the_normalized_tag_is_already_there()
    {
        Assert.Equal(["work", "home"], f.Tasks.WithTag(["work", "home"], " WORK "));
    }

    [Fact]
    public void Normalizes_the_tags_it_was_given_when_the_typed_tag_is_already_there()
    {
        Assert.Equal(["work"], f.Tasks.WithTag(["Work"], "work"));
    }

    [Fact]
    public void Normalizes_the_tags_it_was_given_and_drops_repeats_when_it_adds_a_tag()
    {
        Assert.Equal(["work", "my-home", "new"], f.Tasks.WithTag(["Work", "my  Home", "WORK"], "new"));
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
    [InlineData(" \r\n ")]
    public void Refuses_to_add_a_tag_that_is_empty_once_normalized(string typed)
    {
        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.WithTag(["a"], typed));
    }

    [Fact]
    public void Refuses_to_add_a_tag_when_the_list_already_holds_fifteen()
    {
        var fifteen = Enumerable.Range(1, 15).Select(n => $"tag{n}").ToArray();

        Assert.Throws<ArgumentException>(() => f.Tasks.WithTag(fifteen, "extra"));
    }

    [Fact]
    public void Adds_the_fifteenth_tag()
    {
        var fourteen = Enumerable.Range(1, 14).Select(n => $"tag{n}").ToArray();

        Assert.Equal([.. fourteen, "extra"], f.Tasks.WithTag(fourteen, "extra"));
    }

    [Fact]
    public void Suggests_the_tags_in_use_that_contain_the_typed_text_ignoring_case()
    {
        f.SeedTagsAndUse(["Work", "wonder"], ["Work"], ["home"]);

        Assert.Equal(["work", "wonder"], f.Tasks.SuggestTags("WO", []));
    }

    [Fact]
    public void Normalizes_the_typed_text_before_matching()
    {
        f.SeedTagsAndUse(["my-work"], ["home"]);

        Assert.Equal(["my-work"], f.Tasks.SuggestTags("My w", []));
    }

    [Fact]
    public void Suggests_a_tag_when_the_typed_text_matches_the_middle_of_it()
    {
        f.SeedTagsAndUse(["Work"], ["home"]);

        Assert.Equal(["work"], f.Tasks.SuggestTags("ork", []));
    }

    [Fact]
    public void Suggests_tags_that_contain_the_text_most_used_first_then_alphabetically()
    {
        f.SeedTagsAndUse(["network", "artwork"], ["artwork"], ["work"], ["work"], ["work"]);

        Assert.Equal(["work", "artwork", "network"], f.Tasks.SuggestTags("WORK", []));
    }

    [Fact]
    public void Leaves_out_the_task_tags_when_the_typed_text_matches_the_middle_of_them()
    {
        f.SeedTagsAndUse(["Work", "network"]);

        Assert.Equal(["network"], f.Tasks.SuggestTags("ork", ["WORK"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Suggests_nothing_for_empty_typed_text(string typed)
    {
        f.SeedTagsAndUse(["Work"], ["home"]);

        Assert.Empty(f.Tasks.SuggestTags(typed, []));
    }

    [Fact]
    public void Trims_the_typed_text_before_matching()
    {
        f.SeedTagsAndUse(["Work"]);

        Assert.Equal(["work"], f.Tasks.SuggestTags("  wo ", []));
    }

    [Fact]
    public void Leaves_out_the_tags_the_task_already_has_in_any_spelling()
    {
        f.SeedTagsAndUse(["Work", "wonder", "my-work"]);

        Assert.Equal(["wonder"], f.Tasks.SuggestTags("wo", ["work", "My  Work"]));
    }

    [Fact]
    public void Suggests_in_the_order_of_the_tags_in_use()
    {
        f.SeedTagsAndUse(["wa", "wb"], ["wb"], ["wc"], ["wc"], ["wc"]);

        Assert.Equal(["wc", "wb", "wa"], f.Tasks.SuggestTags("w", []));
    }
}
