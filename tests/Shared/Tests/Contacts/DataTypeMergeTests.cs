using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Three-way merge of a data type file, by entry ID and property.</summary>
[Unit]
public sealed class DataTypeMergeTests
{
    private const string Path = ContactsFixture.CareerPathsFile;

    private readonly ContactsFixture f = new();

    private static string Entry(string id, params (string Key, string Value)[] properties) =>
        ContactFiles.Object([("id", id), .. properties]);

    private static byte[] File(params string[] entries) => ContactFiles.Utf8(ContactFiles.ArrayOf(entries));

    private ContactFiles.Parsed Merge(byte[]? basis, byte[]? local, byte[]? remote) =>
        ContactFiles.Read(f.Rules.Merge(Path, basis, local, remote)!);

    [Fact]
    public void Takes_a_property_changed_on_one_side_only()
    {
        var basis = File(Entry("em", ("name", "EM"), ("short", "E")));
        var local = File(Entry("em", ("name", "Manager"), ("short", "E")));
        var remote = File(Entry("em", ("name", "EM"), ("short", "M")));

        var entry = Merge(basis, local, remote).WithId("em");

        Assert.Equal("Manager", entry["name"]);
        Assert.Equal("M", entry["short"]);
    }

    [Fact]
    public void Takes_the_remote_property_when_both_sides_changed_it()
    {
        var basis = File(Entry("em", ("name", "EM")));
        var local = File(Entry("em", ("name", "L")));
        var remote = File(Entry("em", ("name", "R")));

        Assert.Equal("R", Merge(basis, local, remote).WithId("em")["name"]);
    }

    [Fact]
    public void Treats_a_list_property_as_one_value()
    {
        var basis = File(Entry("em") .Replace("}", ", \"alternatives\": [ \"a\", \"b\" ] }"));
        var local = File(Entry("em") .Replace("}", ", \"alternatives\": [ \"a\", \"b\", \"c\" ] }"));
        var remote = File(Entry("em") .Replace("}", ", \"alternatives\": [ \"x\" ] }"));

        Assert.Equal("""[ "x" ]""", Normalized(Merge(basis, local, remote).WithId("em")["alternatives"]));
        Assert.Equal("""[ "a", "b", "c" ]""", Normalized(Merge(basis, local, basis).WithId("em")["alternatives"]));
    }

    private static string Normalized(string json) =>
        "[ " + string.Join(", ", System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateArray().Select(item => $"\"{item.GetString()}\"")) + " ]";

    [Fact]
    public void Keeps_an_entry_edited_on_one_side_and_deleted_on_the_other()
    {
        var basis = File(Entry("em", ("name", "EM")), Entry("se", ("name", "SE")));
        var edited = File(Entry("em", ("name", "EM")), Entry("se", ("name", "Engineer")));
        var deleted = File(Entry("em", ("name", "EM")));

        Assert.Equal("Engineer", Merge(basis, edited, deleted).WithId("se")["name"]);
        Assert.Equal("Engineer", Merge(basis, deleted, edited).WithId("se")["name"]);
    }

    [Fact]
    public void Deletes_an_entry_deleted_on_one_side_and_unchanged_on_the_other()
    {
        var basis = File(Entry("em", ("name", "EM")), Entry("se", ("name", "SE")));
        var deleted = File(Entry("em", ("name", "EM")));

        Assert.Equal(["em"], Merge(basis, deleted, basis).Ids);
    }

    [Fact]
    public void Orders_the_remote_entries_first_then_the_entries_only_the_local_side_has()
    {
        var basis = File(Entry("a"));
        var local = File(Entry("l2"), Entry("a"), Entry("l1"));
        var remote = File(Entry("r2"), Entry("a"), Entry("r1"));

        Assert.Equal(["r2", "a", "r1", "l2", "l1"], Merge(basis, local, remote).Ids);
    }

    [Fact]
    public void Joins_both_files_and_takes_remote_properties_when_there_is_no_basis()
    {
        var local = File(Entry("a", ("name", "L"), ("short", "l")), Entry("b"));
        var remote = File(Entry("a", ("name", "R")), Entry("c"));

        var merged = Merge(null, local, remote);

        Assert.Equal(["a", "c", "b"], merged.Ids);
        Assert.Equal("R", merged.WithId("a")["name"]);
        Assert.Equal("l", merged.WithId("a")["short"]);
    }

    [Fact]
    public void Deletes_a_file_deleted_on_one_side_and_unchanged_on_the_other()
    {
        var basis = File(Entry("a"));

        Assert.Null(f.Rules.Merge(Path, basis, null, basis));
    }

    [Fact]
    public void Keeps_a_file_changed_on_one_side_and_deleted_on_the_other()
    {
        var basis = File(Entry("a"));
        var changed = File(Entry("a", ("name", "N")));

        Assert.Equal("N", Merge(basis, null, changed).WithId("a")["name"]);
    }

    [Fact]
    public void Merges_the_pronouns_file_the_same_way()
    {
        var basis = File(Entry("he", ("subject", "he")));
        var local = File(Entry("he", ("subject", "he")), Entry("xe", ("subject", "xe")));

        var merged = ContactFiles.Read(f.Rules.Merge(ContactsFixture.PronounsFile, basis, local, basis)!);

        Assert.Equal(["he", "xe"], merged.Ids);
    }
}
