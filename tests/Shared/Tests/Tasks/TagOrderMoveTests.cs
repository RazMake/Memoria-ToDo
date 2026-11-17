using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Shared.Tasks.Tests.TagsFile;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Moving a tag to a place between two neighbors, in tags.json.</summary>
[Unit]
public sealed class TagOrderMoveTests
{
    private readonly TasksFixture f = new();

    private ITagOrder Order => f.Store.TagOrder;

    private void UseThreePlaced()
    {
        f.SeedTags(new TagEntry("a", new Stamp("red", Earlier), new Stamp("a0", Earlier)), Placed("b", "a1"), Placed("c", "a2"));
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    private IReadOnlyList<string> Shown(params string[] tags) => Order.Sort(tags);

    // Placing

    [Fact]
    public void Puts_a_tag_between_its_two_neighbors()
    {
        UseThreePlaced();

        Order.Move("c", "a", "b");

        Assert.Equal(["a", "c", "b"], Shown("a", "b", "c"));
    }

    [Fact]
    public void Puts_a_tag_before_the_first_one()
    {
        UseThreePlaced();

        Order.Move("c", null, "a");

        Assert.Equal(["c", "a", "b"], Shown("a", "b", "c"));
    }

    [Fact]
    public void Puts_a_tag_after_the_last_one()
    {
        UseThreePlaced();

        Order.Move("a", "c", null);

        Assert.Equal(["b", "c", "a"], Shown("a", "b", "c"));
    }

    [Fact]
    public void Matches_the_tag_and_its_neighbors_ignoring_case()
    {
        UseThreePlaced();

        Order.Move("C", "A", "B");

        Assert.Equal(["a", "c", "b"], Shown("a", "b", "c"));
    }

    [Fact]
    public void Changes_only_the_moved_tags_entry_with_the_current_time_and_keeps_its_color()
    {
        UseThreePlaced();

        Order.Move("a", "c", null);

        var entries = f.ReadTags();
        Assert.Equal(["a", "b", "c"], entries.Select(e => e.Tag));
        Assert.Equal(Placed("b", "a1"), entries[1]);
        Assert.Equal(Placed("c", "a2"), entries[2]);
        Assert.Equal(new Stamp("red", Earlier), entries[0].Color);
        Assert.Equal(Now, entries[0].Place!.Updated);
        Assert.NotEqual("a0", entries[0].Place!.Value);
    }

    [Fact]
    public void Writes_the_tag_as_spelled_in_the_call()
    {
        f.SeedTags(Placed("Work", "a0"), Placed("home", "a1"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("work", "home", null);

        Assert.Equal(["home", "work"], f.ReadTags().Select(e => e.Tag));
    }

    [Fact]
    public void Keeps_the_places_after_a_restart()
    {
        UseThreePlaced();
        Order.Move("c", null, "a");

        var restarted = Assert.IsAssignableFrom<ITaskStore>(f.Restarted());
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Equal(["c", "a", "b"], restarted.TagOrder.Sort(["a", "b", "c"]));
    }

    [Fact]
    public void Keeps_the_place_when_a_color_is_picked()
    {
        f.SeedTags(new TagEntry("work", new Stamp("teal", Earlier), new Stamp("a0", T1)));
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Store.TagColors.SetColor("work", "gold");

        Assert.Equal([new TagEntry("work", new Stamp("gold", Now), new Stamp("a0", T1))], f.ReadTags());
    }

    // Tags with no place yet

    [Fact]
    public void Gives_every_tag_in_use_a_place_first_when_a_neighbor_has_none()
    {
        f.Seed(TasksFixture.Id(1), tags: ["x", "y", "z"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("z", null, "x");

        Assert.Equal(["z", "x", "y"], Shown("x", "y", "z"));
        var entries = f.ReadTags();
        Assert.Equal(["x", "y", "z"], entries.Select(e => e.Tag));
        Assert.All(entries, e => Assert.Equal(Now, e.Place!.Updated));
    }

    [Fact]
    public void Gives_a_neighbor_that_is_not_in_use_a_place_too()
    {
        f.Seed(TasksFixture.Id(1), tags: ["m"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("m", "zed", null);

        Assert.Equal(["zed", "m"], Shown("m", "zed"));
        Assert.Contains(f.ReadTags(), e => e.Tag == "zed" && e.Place is not null);
    }

    [Fact]
    public void Gives_every_tag_in_use_a_place_first_when_both_neighbors_share_one()
    {
        f.SeedTags(Placed("a", "a0"), Placed("b", "a0"), Placed("c", "a0"), Placed("d", "a0"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("d", "a", "b");

        Assert.Equal(["a", "d", "b", "c"], Shown("a", "b", "c", "d"));
    }

    [Fact]
    public void Gives_a_tag_placed_in_the_file_but_not_in_use_a_new_time_and_keeps_its_order_among_the_placed_ones()
    {
        f.SeedTags(Placed("u", "a0"), Placed("w", "a1"));
        f.Seed(TasksFixture.Id(1), tags: ["x", "y"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("y", null, "x");

        Assert.Equal(["y", "x", "u", "w"], Shown("u", "w", "x", "y"));
        var entries = f.ReadTags();
        Assert.Equal(Now, entries.Single(e => e.Tag == "u").Place!.Updated);
        Assert.Equal(Now, entries.Single(e => e.Tag == "w").Place!.Updated);
    }

    [Fact]
    public void Places_a_tag_exactly_where_it_was_dropped_among_unplaced_tags()
    {
        f.Seed(TasksFixture.Id(1), tags: ["a", "b", "c"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Order.Move("a", "c", null);

        Assert.Equal(["b", "c", "a"], Shown("a", "b", "c"));
    }

    // Nothing to change

    [Fact]
    public void Writes_nothing_and_raises_no_notice_when_the_tag_already_shows_there()
    {
        UseThreePlaced();
        var before = f.Files.ReadFile(f.TagColorsFile);
        var (colors, order, saved) = f.CountNotices();
        f.Files.FailWritesUnder(f.Clone);

        Order.Move("b", "a", "c");

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
        Assert.Equal((0, 0, 0), (colors(), order(), saved()));
    }

    [Fact]
    public void Changes_nothing_when_both_neighbors_are_open()
    {
        UseThreePlaced();
        var before = f.Files.ReadFile(f.TagColorsFile);
        var (_, order, saved) = f.CountNotices();
        f.Files.FailWritesUnder(f.Clone);

        Order.Move("b", null, null);

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
        Assert.Equal((0, 0), (order(), saved()));
    }

    [Fact]
    public void Writes_nothing_when_an_unplaced_tag_already_shows_first()
    {
        f.Seed(TasksFixture.Id(1), tags: ["a", "b"]);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (_, order, saved) = f.CountNotices();
        f.Files.FailWritesUnder(f.Clone);

        Order.Move("a", null, "b");

        Assert.False(f.Files.FileExists(f.TagColorsFile));
        Assert.Equal((0, 0), (order(), saved()));
    }

    // Refusals

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded ")]
    [InlineData("two\nlines")]
    public void Refuses_an_invalid_tag_and_writes_nothing(string tag)
    {
        UseThreePlaced();
        var before = f.Files.ReadFile(f.TagColorsFile);

        Assert.ThrowsAny<ArgumentException>(() => Order.Move(tag, "a", "b"));

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
    }

    [Theory]
    [InlineData("b", "b", null)]
    [InlineData("b", null, "B")]
    [InlineData("b", "b", "b")]
    public void Refuses_a_neighbor_that_is_the_tag_itself_and_writes_nothing(string tag, string? previous, string? next)
    {
        UseThreePlaced();
        var before = f.Files.ReadFile(f.TagColorsFile);

        Assert.ThrowsAny<ArgumentException>(() => Order.Move(tag, previous, next));

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
    }

    [Fact]
    public void Refuses_a_previous_tag_that_does_not_show_before_the_next_tag_and_writes_nothing()
    {
        f.SeedTags(Placed("a", "a0"), Placed("b", "a1"), Placed("c", "a2"), Placed("d", "a3"));
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Files.ReadFile(f.TagColorsFile);

        Assert.ThrowsAny<ArgumentException>(() => Order.Move("d", "c", "a"));

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
    }

    [Fact]
    public void Refuses_a_move_when_no_repository_is_selected()
    {
        Assert.Throws<NoRepositoryFolderException>(() => Order.Move("a", null, "b"));
    }

    // Notices

    [Fact]
    public void Raises_one_order_notice_and_one_local_save_but_no_color_notice_for_a_move()
    {
        UseThreePlaced();
        var (colors, order, saved) = f.CountNotices();

        Order.Move("c", null, "a");

        Assert.Equal((0, 1, 1), (colors(), order(), saved()));
    }

    [Fact]
    public void Raises_only_a_color_notice_and_a_local_save_when_a_color_is_picked()
    {
        UseThreePlaced();
        var (colors, order, saved) = f.CountNotices();

        f.Store.TagColors.SetColor("b", "teal");

        Assert.Equal((1, 0, 1), (colors(), order(), saved()));
    }
}
