using System.Text;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Shared.Tasks.Tests.TagsFile;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The pure three-version merge of tags.json entries: version 1 and 2 files, colors and places merged apart.</summary>
[Unit]
public sealed class TagsFileMergeTests
{
    private readonly TasksFixture f = new();

    private IRepositoryFileRules Rules => f.Store.Synchronization.Files;

    private List<TagEntry> Merged(byte[]? basis, byte[]? local, byte[]? remote)
    {
        var merged = Rules.MergeTagColors(basis, local, remote);
        Assert.NotNull(merged);
        var (version, entries) = Parse(merged);
        Assert.Equal(2, version);
        return entries;
    }

    private static TagEntry Both(string tag, string color, string colorTime, string place, string placeTime) =>
        new(tag, new Stamp(color, colorTime), new Stamp(place, placeTime));

    // Reading versions 1 and 2

    [Fact]
    public void Reads_a_version_1_entry_as_a_color_with_no_place_and_writes_version_2()
    {
        var v1 = Encoding.UTF8.GetBytes(TasksFixture.TagColorsJson(1, ("work", "teal", T1)));

        Assert.Equal([Colored("work", "teal", T1)], Merged(null, v1, null));
    }

    [Fact]
    public void Merges_a_version_1_file_with_a_version_2_file()
    {
        var v1 = Encoding.UTF8.GetBytes(TasksFixture.TagColorsJson(1, ("work", "teal", T2), ("home", "red", T1)));
        var v2 = Bytes(Placed("work", "a0", T1));

        Assert.Equal([Colored("home", "red", T1), Both("work", "teal", T2, "a0", T1)], Merged(null, v1, v2));
    }

    [Fact]
    public void Keeps_a_color_only_and_a_place_only_entry_as_they_are()
    {
        var file = Bytes(Colored("a", "red", T1), Placed("b", "a1", T1));

        Assert.Equal([Colored("a", "red", T1), Placed("b", "a1", T1)], Merged(null, file, null));
    }

    // Merging entries

    [Fact]
    public void Keeps_an_entry_that_is_on_one_side_only()
    {
        var merged = Merged(null, Bytes(Placed("home", "a0", T1)), Bytes(Colored("work", "teal", T1)));

        Assert.Equal([Placed("home", "a0", T1), Colored("work", "teal", T1)], merged);
    }

    [Fact]
    public void Matches_tags_by_their_normalized_form_and_writes_it()
    {
        var merged = Merged(null, Bytes(Colored("Work", "teal", T1)), Bytes(Placed("  WORK ", "a0", T1)));

        Assert.Equal([Both("work", "teal", T1, "a0", T1)], merged);
    }

    [Fact]
    public void Matches_a_tag_with_inner_spaces_to_its_hyphenated_form()
    {
        var merged = Merged(null, Bytes(Colored("my  work", "teal", T1)), Bytes(Placed("my-work", "a0", T1)));

        Assert.Equal([Both("my-work", "teal", T1, "a0", T1)], merged);
    }

    [Fact]
    public void Keeps_the_first_entry_in_a_file_that_names_one_normalized_tag_twice()
    {
        var twice = Bytes(Both("Work", "teal", T1, "a0", T1), Both("work", "gold", T3, "a9", T3));

        Assert.Equal([Both("work", "teal", T1, "a0", T1)], Merged(null, twice, null));
    }

    [Fact]
    public void Merges_a_color_picked_on_one_side_with_a_move_made_on_the_other()
    {
        var local = Bytes(Both("work", "teal", T1, "a0", T1));
        var remote = Bytes(Both("work", "gold", T2, "a0", T1));
        var moved = Bytes(Both("work", "teal", T1, "a5", T3));

        Assert.Equal([Both("work", "gold", T2, "a0", T1)], Merged(null, local, remote));
        Assert.Equal([Both("work", "gold", T2, "a5", T3)], Merged(null, moved, remote));
    }

    [Fact]
    public void Takes_the_color_with_the_later_time_apart_from_the_place()
    {
        var local = Bytes(Both("work", "teal", T3, "a0", T1));
        var remote = Bytes(Both("work", "gold", T1, "a9", T2));

        Assert.Equal([Both("work", "teal", T3, "a9", T2)], Merged(null, local, remote));
    }

    [Fact]
    public void Takes_the_remote_color_and_the_remote_place_when_their_times_are_equal()
    {
        var local = Bytes(Both("work", "teal", T1, "a0", T1));
        var remote = Bytes(Both("work", "gold", T1, "a9", T1));

        Assert.Equal([Both("work", "gold", T1, "a9", T1)], Merged(null, local, remote));
    }

    [Fact]
    public void Keeps_a_color_or_a_place_that_the_other_side_has_as_null()
    {
        var local = Bytes(new TagEntry("work", new Stamp("teal", T1), null));
        var remote = Bytes(new TagEntry("work", null, new Stamp("a0", T2)));

        Assert.Equal([Both("work", "teal", T1, "a0", T2)], Merged(null, local, remote));
        Assert.Equal([Both("work", "teal", T1, "a0", T2)], Merged(null, remote, local));
    }

    [Fact]
    public void Never_removes_an_entry_the_other_side_lacks_even_when_the_basis_had_it()
    {
        var basis = Bytes(Placed("home", "a0", T1), Placed("work", "a1", T1));

        Assert.Equal([Placed("home", "a0", T1), Placed("work", "a1", T1)], Merged(basis, Bytes(Placed("work", "a1", T1)), basis));
    }

    [Fact]
    public void Lets_a_present_file_win_over_an_absent_side()
    {
        var present = Bytes(Placed("work", "a0", T1));

        Assert.Equal([Placed("work", "a0", T1)], Merged(null, present, null));
        Assert.Equal([Placed("work", "a0", T1)], Merged(present, null, present));
    }

    [Fact]
    public void Writes_the_entries_sorted_by_normalized_tag_ordinally_and_the_same_bytes_every_time()
    {
        var local = Bytes(Placed("az", "a0", T1), Placed("Cherry", "a1", T1));
        var remote = Bytes(Placed("a_z", "a2", T1));

        Assert.Equal(["a_z", "az", "cherry"], Merged(null, local, remote).Select(e => e.Tag));
        Assert.Equal(Rules.MergeTagColors(null, local, remote), Rules.MergeTagColors(null, local, remote));
    }

    // Unreadable files

    public static TheoryData<string> UnreadableFiles() => new()
    {
        "{ not json",
        """{ "version": 2 }""",
        """{ "version": 3, "tags": [] }""",
        Json(Placed("   ", "a0")),
        Json(Placed("", "a0")),
        Json(Placed(" \n ", "a0")),
        Json(new TagEntry("work", new Stamp("", T1), null)),
        Json(new TagEntry("work", new Stamp("   ", T1), null)),
        Json(new TagEntry("work", new Stamp("teal\ngold", T1), null)),
        Json(new TagEntry("work", new Stamp("teal", "yesterday"), null)),
        Json(Placed("work", "")),
        Json(Placed("work", "!!")),
        Json(Placed("work", "a")),
        Json(Placed("work", "a0", "soon")),
        Json(new TagEntry("work", null, null)),
    };

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Refuses_to_merge_an_unreadable_file_and_leaves_the_inputs_unchanged(string bad)
    {
        var good = Bytes(Placed("home", "a0", T1));
        var badBytes = Encoding.UTF8.GetBytes(bad);
        var badCopy = badBytes.ToArray();
        var goodCopy = good.ToArray();

        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(good, badBytes, good));
        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(good, good, badBytes));
        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(badBytes, good, good));
        Assert.Equal(badCopy, badBytes);
        Assert.Equal(goodCopy, good);
    }

    [Fact]
    public void Leaves_the_merged_files_unchanged()
    {
        var basis = Bytes(Placed("a", "a0", T1));
        var local = Bytes(Placed("a", "a1", T2), Colored("b", "red", T1));
        var remote = Bytes(Placed("c", "a2", T1));
        var copies = new[] { basis.ToArray(), local.ToArray(), remote.ToArray() };

        Rules.MergeTagColors(basis, local, remote);

        Assert.Equal(copies[0], basis);
        Assert.Equal(copies[1], local);
        Assert.Equal(copies[2], remote);
    }
}
