using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// A task saved with two or more tags before the one-tag rule: it loads unchanged, and search, the tag filter, and the tags
/// in use still see every one of its tags.
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

        Assert.Equal(["Buy milk"], f.Tasks.Search("", tag).Select(task => task.Title));
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
