using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A file Contacts can't read is skipped: left unchanged, reported, and the other files still load.</summary>
[Unit]
public sealed class UnreadableFileTests
{
    private const string FriendsFile = ContactsFixture.Folder + "/friends.json";

    private readonly ContactsFixture f = new();

    public UnreadableFileTests() => f.SeedDataTypes();

    [Fact]
    public void Skips_a_group_file_with_the_same_id_twice_ignoring_case()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("ab1", "Ana"), ContactFiles.Person("AB1", "Bo"));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Empty(f.Contacts.Groups);
    }

    [Fact]
    public void Skips_a_file_that_is_not_valid_json()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void Skips_a_file_that_is_not_a_json_object(string json)
    {
        f.Seed(ContactsFixture.TeamFile, json);

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }

    [Fact]
    public void Skips_a_file_whose_members_are_not_a_list_of_objects()
    {
        f.Seed(ContactsFixture.TeamFile, """{ "version": 1, "position": 1000, "members": [ 5 ] }""");

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }

    [Fact]
    public void Skips_a_file_that_can_not_be_read()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Disk.FailReadsUnder(f.PathOf(ContactsFixture.Folder));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.True(f.Contacts.HasContactFiles);
    }

    [Fact]
    public void Loads_the_other_files_and_lists_the_skipped_one_with_a_reason()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));

        f.Load();

        Assert.Equal(["friends"], f.Contacts.Groups.Select(group => group.Name));
        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Equal([ContactsFixture.TeamFile], f.SkippedPaths);
    }

    [Fact]
    public void Never_writes_a_skipped_file_even_when_another_group_is_saved()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));
        var before = f.Bytes(ContactsFixture.TeamFile);
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("friends", new Contact("2", "Bo B"));

        Assert.Equal(before, f.Bytes(ContactsFixture.TeamFile));
        Assert.Equal([FriendsFile], f.Written);
    }

    [Fact]
    public void Skips_a_group_file_with_a_lone_surrogate_in_a_member_value_and_still_loads_the_other_groups()
    {
        f.Seed(ContactsFixture.TeamFile, ContactFiles.Group(1000, """{ "id": "1", "fullName": "Ana", "title": "Dev\uD800" }"""));
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Equal([ContactsFixture.TeamFile], f.SkippedPaths);
        Assert.Equal(["friends"], f.Contacts.Groups.Select(group => group.Name));
        Assert.Null(f.Contacts.Find("1"));
    }

    [Fact]
    public void Skips_a_group_file_with_a_lone_surrogate_only_in_an_unknown_field_name()
    {
        f.Seed(ContactsFixture.TeamFile, ContactFiles.Group(1000, """{ "id": "1", "fullName": "Ana", "x\uDC00y": "v" }"""));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Empty(f.Contacts.Groups);
    }

    [Fact]
    public void Writes_nothing_while_loading()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));
        f.ForgetWrites();

        f.Load();

        f.AssertNothingWritten();
    }
}
