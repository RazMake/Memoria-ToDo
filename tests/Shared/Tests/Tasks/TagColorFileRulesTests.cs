using System.Text;
using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Tasks' tags.json path and the pure three-version merge of its entries.</summary>
[Unit]
public sealed class TagColorFileRulesTests
{
    private const string T1 = "2026-02-01T00:00:00.000Z";
    private const string T2 = "2026-03-01T00:00:00.000Z";
    private readonly TasksFixture f = new();

    private IRepositoryFileRules Rules => f.Store.Synchronization.Files;

    private static byte[] File(params (string Tag, string Color, string Updated)[] entries) =>
        Encoding.UTF8.GetBytes(TasksFixture.TagColorsJson(1, entries));

    private List<(string Tag, string Color, string Updated)> Merged(byte[]? basis, byte[]? local, byte[]? remote)
    {
        var merged = Rules.MergeTagColors(basis, local, remote);
        Assert.NotNull(merged);
        using var json = JsonDocument.Parse(merged);
        Assert.Equal(2, json.RootElement.GetProperty("version").GetInt32());
        return json.RootElement.GetProperty("tags").EnumerateArray()
            .Select(e => (e.GetProperty("tag").GetString()!, e.GetProperty("color").GetProperty("value").GetString()!, e.GetProperty("color").GetProperty("updated").GetString()!))
            .ToList();
    }

    // Path

    [Fact]
    public void Names_the_file_tags_json()
    {
        Assert.Equal("tags.json", Rules.TagColorsFileName);
    }

    [Theory]
    [InlineData("tags.json", RepositoryFileKind.TagColors)]
    [InlineData("notes/tags.json", RepositoryFileKind.Unmanaged)]
    [InlineData("Tags.json.bak", RepositoryFileKind.Unmanaged)]
    public void Classifies_a_repository_path(string path, RepositoryFileKind kind) => Assert.Equal(kind, Rules.Classify(path));

    // Merge

    [Fact]
    public void Keeps_an_entry_that_is_on_one_side_only()
    {
        var merged = Merged(null, File(("home", "red", T1)), File(("work", "teal", T1)));

        Assert.Equal([("home", "red", T1), ("work", "teal", T1)], merged);
    }

    [Fact]
    public void Takes_the_entry_with_the_later_time_and_writes_the_normalized_tag()
    {
        var local = File(("Work", "teal", T2));
        var remote = File(("work", "gold", T1));

        Assert.Equal([("work", "teal", T2)], Merged(null, local, remote));
        Assert.Equal([("work", "gold", T2)], Merged(null, File(("Work", "teal", T1)), File(("work", "gold", T2))));
    }

    [Fact]
    public void Takes_the_remote_entry_when_both_times_are_equal()
    {
        Assert.Equal([("work", "gold", T1)], Merged(null, File(("Work", "teal", T1)), File(("work", "gold", T1))));
    }

    [Fact]
    public void Never_removes_an_entry_the_other_side_lacks_even_when_the_basis_had_it()
    {
        var basis = File(("home", "red", T1), ("work", "teal", T1));

        Assert.Equal([("home", "red", T1), ("work", "teal", T1)], Merged(basis, File(("work", "teal", T1)), basis));
        Assert.Equal([("home", "red", T1), ("work", "teal", T1)], Merged(basis, basis, File(("home", "red", T1))));
    }

    [Fact]
    public void Lets_a_present_file_win_over_an_absent_side()
    {
        var present = File(("work", "teal", T1));

        Assert.Equal([("work", "teal", T1)], Merged(null, present, null));
        Assert.Equal([("work", "teal", T1)], Merged(present, null, present));
        Assert.Equal([("work", "teal", T1)], Merged(null, null, present));
    }

    [Fact]
    public void Merges_three_absent_files_to_none()
    {
        Assert.Null(Rules.MergeTagColors(null, null, null));
    }

    [Fact]
    public void Writes_the_merged_entries_sorted_by_normalized_tag_ordinally_and_the_same_bytes_every_time()
    {
        var local = File(("az", "red", T1), ("Cherry", "blue", T1));
        var remote = File(("a_z", "green", T1));

        Assert.Equal(["a_z", "az", "cherry"], Merged(null, local, remote).Select(e => e.Tag).ToList());
        Assert.Equal(Rules.MergeTagColors(null, local, remote), Rules.MergeTagColors(null, local, remote));
    }

    [Fact]
    public void Keeps_the_first_entry_when_one_file_names_the_same_normalized_tag_twice()
    {
        var twice = File(("X", "red", T1), ("x", "blue", T2));

        Assert.Equal([("x", "red", T1)], Merged(null, twice, null));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 3, "tags": [] }""")]
    [InlineData("""{ "version": 1, "tags": [ { "tag": "  ", "color": "red", "updated": "2026-01-01T00:00:00.000Z" } ] }""")]
    [InlineData("""{ "version": 1, "tags": [ { "tag": "x", "color": "", "updated": "2026-01-01T00:00:00.000Z" } ] }""")]
    [InlineData("""{ "version": 1, "tags": [ { "tag": "x", "color": "red", "updated": "soon" } ] }""")]
    public void Refuses_to_merge_an_unreadable_or_newer_file_and_leaves_the_inputs_unchanged(string bad)
    {
        var good = File(("work", "teal", T1));
        var badBytes = Encoding.UTF8.GetBytes(bad);
        var copy = badBytes.ToArray();
        var goodCopy = good.ToArray();

        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(good, badBytes, good));
        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(good, good, badBytes));
        Assert.Throws<InvalidDataException>(() => Rules.MergeTagColors(badBytes, good, good));
        Assert.Equal(copy, badBytes);
        Assert.Equal(goodCopy, good);
    }

    [Fact]
    public void Leaves_the_merged_files_unchanged()
    {
        var basis = File(("a", "red", T1));
        var local = File(("a", "blue", T2), ("b", "red", T1));
        var remote = File(("c", "gold", T1));
        var copies = new[] { basis.ToArray(), local.ToArray(), remote.ToArray() };

        Rules.MergeTagColors(basis, local, remote);

        Assert.Equal(copies[0], basis);
        Assert.Equal(copies[1], local);
        Assert.Equal(copies[2], remote);
    }
}
