using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Three-way merge of a group file, by contact ID and field.</summary>
[Unit]
public sealed class GroupMergeTests
{
    private const string Path = ContactsFixture.TeamFile;

    private readonly ContactsFixture f = new();

    private static string Person(string id, string name, params (string Key, string Value)[] fields) =>
        ContactFiles.Person(id, name, fields);

    private static byte[] Group(params string[] members) => ContactFiles.Utf8(ContactFiles.Group(1000, members));

    private ContactFiles.Parsed Merge(byte[]? basis, byte[]? local, byte[]? remote) =>
        ContactFiles.Read(f.Rules.Merge(Path, basis, local, remote)!);

    [Fact]
    public void Takes_a_change_made_on_one_side_only()
    {
        var basis = Group(Person("1", "Ana", ("title", "Dev")), Person("2", "Bob", ("title", "QA")));
        var local = Group(Person("1", "Ana", ("title", "Lead")), Person("2", "Bob", ("title", "QA")));
        var remote = Group(Person("1", "Ana", ("title", "Dev")), Person("2", "Bob", ("title", "PM")));

        var merged = Merge(basis, local, remote);

        Assert.Equal("Lead", merged.WithId("1")["title"]);
        Assert.Equal("PM", merged.WithId("2")["title"]);
    }

    [Fact]
    public void Takes_the_remote_value_when_both_sides_changed_a_field()
    {
        var basis = Group(Person("1", "Ana", ("title", "Dev")));
        var local = Group(Person("1", "Ana", ("title", "Lead")));
        var remote = Group(Person("1", "Ana", ("title", "PM")));

        Assert.Equal("PM", Merge(basis, local, remote).WithId("1")["title"]);
    }

    [Fact]
    public void Merges_different_fields_of_one_contact_from_both_sides()
    {
        var basis = Group(Person("1", "Ana", ("title", "Dev")));
        var local = Group(Person("1", "Ana", ("title", "Lead")));
        var remote = Group(Person("1", "Ana", ("title", "Dev"), ("location", "Cluj")));

        var contact = Merge(basis, local, remote).WithId("1");

        Assert.Equal("Lead", contact["title"]);
        Assert.Equal("Cluj", contact["location"]);
    }

    [Fact]
    public void Takes_the_remote_removal_of_a_field_when_both_sides_changed_it()
    {
        var basis = Group(Person("1", "Ana", ("title", "Dev")));
        var local = Group(Person("1", "Ana", ("title", "Lead")));
        var remote = Group(Person("1", "Ana"));

        Assert.False(Merge(basis, local, remote).WithId("1").ContainsKey("title"));
    }

    [Fact]
    public void Removes_a_field_removed_on_one_side_only()
    {
        var basis = Group(Person("1", "Ana", ("title", "Dev")));
        var local = Group(Person("1", "Ana"));
        var remote = Group(Person("1", "Ana", ("title", "Dev")));

        Assert.False(Merge(basis, local, remote).WithId("1").ContainsKey("title"));
    }

    [Fact]
    public void Keeps_a_contact_added_on_either_side()
    {
        var basis = Group(Person("1", "Ana"));
        var local = Group(Person("1", "Ana"), Person("2", "Bob"));
        var remote = Group(Person("1", "Ana"), Person("3", "Cy"));

        Assert.Equal(["1", "2", "3"], Merge(basis, local, remote).Ids.Order());
    }

    [Fact]
    public void Deletes_a_contact_deleted_on_one_side_when_the_other_did_not_edit_it()
    {
        var basis = Group(Person("1", "Ana"), Person("2", "Bob"));
        var local = Group(Person("1", "Ana"));
        var remote = Group(Person("1", "Ana"), Person("2", "Bob"));

        Assert.Equal(["1"], Merge(basis, local, remote).Ids);
        Assert.Equal(["1"], Merge(basis, remote, local).Ids);
    }

    [Fact]
    public void Keeps_a_contact_deleted_on_one_side_when_the_other_edited_it()
    {
        var basis = Group(Person("1", "Ana"), Person("2", "Bob"));
        var local = Group(Person("1", "Ana"));
        var remote = Group(Person("1", "Ana"), Person("2", "Bob", ("title", "PM")));

        var merged = Merge(basis, local, remote);

        Assert.Equal("PM", merged.WithId("2")["title"]);
        Assert.Equal("PM", Merge(basis, remote, local).WithId("2")["title"]);
    }

    [Fact]
    public void Deletes_a_contact_deleted_on_both_sides()
    {
        var basis = Group(Person("1", "Ana"), Person("2", "Bob"));
        var both = Group(Person("1", "Ana"));

        Assert.Equal(["1"], Merge(basis, both, both).Ids);
    }

    [Fact]
    public void Merges_the_position_and_unknown_top_level_fields_one_by_one()
    {
        var basis = ContactFiles.Utf8("""{ "version": 1, "position": 1000, "members": [], "a": "x", "b": "x" }""");
        var local = ContactFiles.Utf8("""{ "version": 1, "position": 2000, "members": [], "a": "x", "b": "L" }""");
        var remote = ContactFiles.Utf8("""{ "version": 1, "position": 1000, "members": [], "a": "R", "b": "R" }""");

        var merged = Merge(basis, local, remote);

        Assert.Equal(2000, merged.Position);
        Assert.Equal("R", merged.Top["a"]);
        Assert.Equal("R", merged.Top["b"]);
    }

    [Fact]
    public void Merges_the_details_of_the_me_file_field_by_field()
    {
        string Me(string title, string location) =>
            ContactFiles.Me(100, ContactFiles.Person("9", "Me", ("title", title), ("location", location)));
        var basis = ContactFiles.Utf8(Me("Dev", "Cluj"));
        var local = ContactFiles.Utf8(Me("Lead", "Cluj"));
        var remote = ContactFiles.Utf8(Me("Dev", "Iasi"));

        var merged = ContactFiles.Read(f.Rules.Merge(ContactsFixture.MeFile, basis, local, remote)!);

        Assert.Equal("Lead", merged.Objects.Single()["title"]);
        Assert.Equal("Iasi", merged.Objects.Single()["location"]);
    }

    [Fact]
    public void Keeps_every_contact_and_takes_remote_fields_when_there_is_no_basis()
    {
        var local = Group(Person("1", "Ana", ("title", "Lead"), ("location", "Cluj")), Person("2", "Bob"));
        var remote = Group(Person("1", "Ana", ("title", "PM")), Person("3", "Cy"));

        var merged = Merge(null, local, remote);

        Assert.Equal(["1", "2", "3"], merged.Ids.Order());
        Assert.Equal("PM", merged.WithId("1")["title"]);
        Assert.Equal("Cluj", merged.WithId("1")["location"]);
    }

    [Fact]
    public void Keeps_a_file_changed_on_one_side_after_the_other_side_deleted_it()
    {
        var basis = Group(Person("1", "Ana"));
        var changed = Group(Person("1", "Ana", ("title", "Lead")));

        Assert.Equal("Lead", Merge(basis, changed, null).WithId("1")["title"]);
        Assert.Equal("Lead", Merge(basis, null, changed).WithId("1")["title"]);
    }

    [Fact]
    public void Deletes_a_file_deleted_on_one_side_when_the_other_did_not_change_it()
    {
        var basis = Group(Person("1", "Ana"));

        Assert.Null(f.Rules.Merge(Path, basis, null, basis));
        Assert.Null(f.Rules.Merge(Path, basis, basis, null));
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("local")]
    [InlineData("remote")]
    public void Rejects_a_side_holding_a_lone_surrogate_as_unreadable(string side)
    {
        var good = Group(Person("1", "Ana"));
        var bad = ContactFiles.Utf8(ContactFiles.Group(1000, """{ "id": "1", "fullName": "Ana\uD800" }"""));

        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(Path,
            side == "basis" ? bad : good, side == "local" ? bad : good, side == "remote" ? bad : good));
    }

    [Fact]
    public void Gives_null_when_every_side_is_absent()
    {
        Assert.Null(f.Rules.Merge(Path, null, null, null));
    }

    [Fact]
    public void Writes_the_merged_file_the_way_a_save_does()
    {
        var local = Group(Person("2", "Zed", ("title", "Dev")), Person("1", "Ana", ("location", "Cluj"), ("id2", "kept")));
        var remote = Group(Person("1", "Ana", ("location", "Cluj"), ("id2", "kept")));

        var text = System.Text.Encoding.UTF8.GetString(f.Rules.Merge(Path, remote, local, remote)!);

        Assert.Equal(
            "{\n  \"version\": 1,\n  \"position\": 1000,\n  \"members\": [\n"
            + "    {\n      \"id\": \"1\",\n      \"fullName\": \"Ana\",\n      \"location\": \"Cluj\",\n      \"id2\": \"kept\"\n    },\n"
            + "    {\n      \"id\": \"2\",\n      \"fullName\": \"Zed\",\n      \"title\": \"Dev\"\n    }\n  ]\n}",
            text);
    }

    [Fact]
    public void Writes_the_members_sorted_by_full_name_then_ID()
    {
        var basis = Group();
        var local = Group(Person("5", "Bob"), Person("2", "bob"), Person("9", "Ana"));

        Assert.Equal(["9", "2", "5"], Merge(basis, local, basis).Ids);
    }
}
