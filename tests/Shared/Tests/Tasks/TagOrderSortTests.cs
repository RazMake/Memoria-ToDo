using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Shared.Tasks.Tests.TagsFile;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Tags sorted in the user's order: tags with no place first, then the placed tags by place.</summary>
[Unit]
public sealed class TagOrderSortTests
{
    private readonly TasksFixture f = new();

    private ITagOrder Order => f.Store.TagOrder;

    [Fact]
    public void Sorts_tags_with_no_place_alphabetically_ignoring_case()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Apple", "banana", "Cherry"], Order.Sort(["Cherry", "banana", "Apple"]));
    }

    [Fact]
    public void Puts_tags_with_no_place_before_the_placed_tags_and_sorts_the_placed_by_place()
    {
        f.SeedTags(Placed("pear", "a1"), Placed("fig", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Apple", "kiwi", "fig", "pear"], Order.Sort(["pear", "fig", "kiwi", "Apple"]));
    }

    [Fact]
    public void Sorts_tags_with_equal_places_alphabetically_ignoring_case()
    {
        f.SeedTags(Placed("b", "a0"), Placed("A", "a0"), Placed("c", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["A", "b", "c"], Order.Sort(["c", "b", "A"]));
    }

    [Fact]
    public void Matches_an_entry_ignoring_case_and_keeps_each_tags_spelling()
    {
        f.SeedTags(Placed("work", "a1"), Placed("home", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["New", "HOME", "Work"], Order.Sort(["Work", "HOME", "New"]));
    }

    [Fact]
    public void Treats_a_tag_with_only_a_color_as_having_no_place()
    {
        f.SeedTags(Colored("zed", "red"), Placed("beta", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["zed", "beta"], Order.Sort(["beta", "zed"]));
    }

    [Fact]
    public void Treats_every_tag_as_unplaced_for_a_version_1_file()
    {
        f.SeedTagColors(1, ("zed", "red", Earlier), ("apple", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["apple", "zed"], Order.Sort(["zed", "apple"]));
        Assert.Equal("red", f.Store.TagColors.ColorOf("zed"));
    }

    [Fact]
    public void Sorts_no_tags_to_no_tags()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(Order.Sort([]));
    }

    [Fact]
    public void Sorts_alphabetically_when_no_repository_is_selected()
    {
        Assert.Equal(["a", "b"], Order.Sort(["b", "a"]));
    }

    [Fact]
    public void Writes_nothing_when_sorting()
    {
        f.SeedTags(Placed("a", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Files.ReadFile(f.TagColorsFile);
        f.Files.FailWritesUnder(f.Clone);

        Order.Sort(["a", "b"]);

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded ")]
    [InlineData("two\nlines")]
    public void Refuses_to_sort_an_invalid_tag(string tag)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => Order.Sort(["ok", tag]));
    }

    [Fact]
    public void Refuses_to_sort_the_same_tag_twice_ignoring_case()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => Order.Sort(["Work", "work"]));
    }
}
