using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// A task with several tags, as a stored file: it loads with every tag in order, and search, the tag filter, and the tags
/// in use see every one of its tags, not only the first. Also what reading does to tags and to files with more than 15.
/// </summary>
[Unit]
public sealed class OlderTaskTagsTests
{
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(1);

    private TaskItem Load()
    {
        f.Seed(id, "Buy milk", "a0", "Oat milk", ["work", "home", "errand"]);
        f.SeedAndUse(TasksFixture.Id(2), "Other", "a1", ["solo"]);
        return f.Tasks.All.Single(task => task.Id == id);
    }

    [Fact]
    public void Reads_the_tags_normalized_and_drops_repeats_keeping_the_first_place()
    {
        f.SeedAndUse(id, tags: ["Work", "my  Home", "WORK", " my-home ", "errand"]);

        Assert.Equal(["work", "my-home", "errand"], f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Reads_a_tag_with_a_line_break_as_hyphenated()
    {
        f.SeedAndUse(id, tags: ["to\ndo"]);

        Assert.Equal(["to-do"], f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Never_rewrites_a_task_file_it_read_with_unnormalized_tags()
    {
        f.Seed(id, tags: ["Work", "work", "My  Home"]);
        var before = f.ReadText(f.TaskFile(id));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    [Fact]
    public void Writes_the_normalized_tags_at_the_next_save_of_the_task()
    {
        f.SeedAndUse(id, tags: ["Work", "work", "My  Home"]);

        f.Tasks.Resolve(id);

        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal(["work", "my-home"], json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public void Loads_every_tag_of_a_stored_file_with_more_than_fifteen()
    {
        var tags = Enumerable.Range(1, 20).Select(n => $"tag{n}").ToArray();

        f.SeedAndUse(id, tags: tags);

        Assert.Equal(tags, f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Keeps_every_tag_of_a_task_with_more_than_fifteen_when_another_part_of_the_task_changes()
    {
        var tags = Enumerable.Range(1, 20).Select(n => $"tag{n}").ToArray();
        f.SeedAndUse(id, tags: tags);

        f.Tasks.Resolve(id);

        Assert.Equal(tags, f.Tasks.All.Single().Tags);
        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal(20, json.RootElement.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void Gives_the_first_tag_as_the_group_tag()
    {
        Assert.Equal("work", Load().GroupTag);
        Assert.Equal("solo", f.Tasks.All.Single(t => t.Id == TasksFixture.Id(2)).GroupTag);
    }

    [Fact]
    public void Gives_no_group_tag_for_a_task_without_tags()
    {
        f.SeedAndUse(id);

        Assert.Null(f.Tasks.All.Single().GroupTag);
    }

    [Fact]
    public void Loads_with_every_tag_in_the_order_it_was_added()
    {
        var task = Load();

        Assert.Equal(["work", "home", "errand"], task.Tags);
    }

    [Fact]
    public void Loads_without_rewriting_its_file()
    {
        f.Seed(id, "Buy milk", "a0", "Oat milk", ["work", "home", "errand"]);
        var before = f.ReadText(f.TaskFile(id));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    [Fact]
    public void Keeps_every_tag_when_another_part_of_the_task_changes()
    {
        var task = Load();

        f.Tasks.Resolve(task.Id);

        Assert.Equal(["work", "home", "errand"], f.Tasks.All.Single(t => t.Id == id).Tags);
    }

    [Theory]
    [InlineData("work")]
    [InlineData("home")]
    [InlineData("errand")]
    public void Search_finds_the_task_by_each_of_its_tags(string tag)
    {
        Load();

        Assert.Equal(["Buy milk"], f.Tasks.Search(tag).Select(task => task.Title));
    }

    [Theory]
    [InlineData("work")]
    [InlineData("HOME")]
    [InlineData("errand")]
    public void The_tag_filter_finds_the_task_by_each_of_its_tags(string tag)
    {
        Load();

        Assert.Equal(["Buy milk"], f.Tasks.Search("", [tag]).Select(task => task.Title));
    }

    [Fact]
    public void Counts_every_tag_of_the_task_among_the_tags_in_use()
    {
        Load();

        var use = f.Tasks.TagsInUse();

        Assert.Equal(["errand", "home", "solo", "work"], use.Select(u => u.Tag).Order());
        Assert.All(use, u => Assert.Equal(1, u.Count));
    }

    [Theory]
    [InlineData("or", "work")]
    [InlineData("ome", "home")]
    [InlineData("rran", "errand")]
    public void Suggests_each_tag_of_the_task(string typed, string tag)
    {
        Load();

        Assert.Equal([tag], f.Tasks.SuggestTags(typed, []));
    }
}
