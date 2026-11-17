using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Shared.Tasks.Tests.TagsFile;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>What the tag order and tag colors do while tags.json can't be read, and when it is reloaded.</summary>
[Unit]
public sealed class TagOrderFileTests
{
    private readonly TasksFixture f = new();

    private ITagOrder Order => f.Store.TagOrder;

    // An unreadable file; each also holds a valid placed "apple", which a readable file would show after "zed".

    public static TheoryData<string> UnreadableFiles() => new()
    {
        "{ not json",
        Json(Placed("apple", "a0"), Placed(" padded ", "a1")),
        Json(Placed("apple", "a0"), Placed("zed", "!!")),
        Json(Placed("apple", "a0"), Placed("zed", "a0", "soon")),
        Json(Placed("apple", "a0"), new TagEntry("zed", null, null)),
        Json(Placed("apple", "a0"), new TagEntry("zed", new Stamp("", Earlier), null)),
        Json(Placed("apple", "a0"), Placed("Apple", "a1")),
        Json(3, Placed("apple", "a0")),
    };

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Shows_no_places_and_the_default_color_for_an_unreadable_file_and_still_loads_the_tasks(string content)
    {
        f.Seed(TasksFixture.Id(1), "Readable");
        f.WriteText(f.TagColorsFile, content);

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Readable"], f.Titles);
        Assert.Equal(["apple", "zed"], Order.Sort(["zed", "apple"]));
        Assert.Null(f.Store.TagColors.ColorOf("apple"));
        Assert.Equal("tags.json", Assert.Single(f.Store.Synchronization.SkippedFiles).RelativePath);
        Assert.Equal(content, f.ReadText(f.TagColorsFile));
    }

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Fails_to_move_a_tag_in_an_unreadable_file_and_leaves_it_untouched(string content)
    {
        f.WriteText(f.TagColorsFile, content);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (colors, order, saved) = f.CountNotices();

        Assert.Throws<InvalidDataException>(() => Order.Move("zed", null, "apple"));

        Assert.Equal(content, f.ReadText(f.TagColorsFile));
        Assert.Equal((0, 0, 0), (colors(), order(), saved()));
    }

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Fails_to_pick_a_color_in_an_unreadable_file_and_leaves_it_untouched(string content)
    {
        f.WriteText(f.TagColorsFile, content);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (colors, order, saved) = f.CountNotices();

        Assert.Throws<InvalidDataException>(() => f.Store.TagColors.SetColor("zed", "teal"));

        Assert.Equal(content, f.ReadText(f.TagColorsFile));
        Assert.Equal((0, 0, 0), (colors(), order(), saved()));
    }

    // Reloads

    [Fact]
    public void Raises_a_color_notice_and_an_order_notice_but_no_local_save_when_a_repository_is_selected()
    {
        f.SeedTags(Placed("a", "a0"));
        var (colors, order, saved) = f.CountNotices();

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal((1, 1, 0), (colors(), order(), saved()));
    }

    [Fact]
    public void Reloads_the_order_and_raises_both_notices_when_an_update_changes_the_file()
    {
        f.SeedTags(Placed("a", "a1"), Placed("b", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);
        Assert.Equal(["b", "a"], Order.Sort(["a", "b"]));
        var (colors, order, saved) = f.CountNotices();

        f.Store.Synchronization.UpdateRepository(f.Clone, () => f.SeedTags(Placed("a", "a0"), Placed("b", "a1")));

        Assert.Equal(["a", "b"], Order.Sort(["a", "b"]));
        Assert.Equal((1, 1, 0), (colors(), order(), saved()));
    }

    [Fact]
    public void Shows_the_order_of_the_newly_selected_repository()
    {
        f.SeedTags(Placed("a", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.UseRepositoryFolder(f.OtherClone);

        Assert.Equal(["a", "b"], Order.Sort(["a", "b"]));
    }
}
