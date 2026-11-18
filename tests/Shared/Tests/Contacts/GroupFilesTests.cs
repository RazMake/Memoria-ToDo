using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Which files in the clone are groups.</summary>
[Unit]
public sealed class GroupFilesTests
{
    private readonly ContactsFixture f = new();

    private static string Ana => ContactFiles.Person("1", "Ana");

    [Fact]
    public void Loads_every_json_file_directly_in_the_folder_as_a_group_named_after_the_file()
    {
        f.SeedGroup("team", 1000, Ana);
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));

        f.Load();

        Assert.Equal(["team", "friends"], f.Contacts.Groups.Select(group => group.Name));
        Assert.Equal(["1"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(["2"], ContactsFixture.Ids(f.Group("friends").Members));
    }

    [Fact]
    public void Ignores_files_in_subfolders()
    {
        f.SeedDataTypes();
        f.SeedGroup("team", 1000, Ana);
        f.Seed($"{ContactsFixture.Folder}/dataTypes/other.json", ContactFiles.Group(5, ContactFiles.Person("9", "Zed")));
        f.Seed($"{ContactsFixture.Folder}/archive/old.json", ContactFiles.Group(6, ContactFiles.Person("8", "Yan")));

        f.Load();

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("team.json.bak")]
    [InlineData("friends.JSON")]
    [InlineData("friends.Json")]
    [InlineData("friends.jsonl")]
    public void Ignores_files_whose_extension_is_not_exactly_json(string fileName)
    {
        f.SeedGroup("team", 1000, Ana);
        f.Seed($"{ContactsFixture.Folder}/{fileName}", ContactFiles.Group(2000, ContactFiles.Person("2", "Bo")));

        f.Load();

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Ignores_files_elsewhere_in_the_clone()
    {
        f.SeedGroup("team", 1000, Ana);
        f.Seed("autocomplete/other.json", ContactFiles.Group(2000, ContactFiles.Person("2", "Bo")));
        f.Seed("contacts/friends.json", ContactFiles.Group(2000, ContactFiles.Person("2", "Bo")));

        f.Load();

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
    }
}
