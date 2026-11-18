using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Finding a contact by alias or ID, and the names two contacts share.</summary>
[Unit]
public sealed class FindTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Finds_a_contact_by_its_alias()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("101", "Ana", ("alias", "anap")), ContactFiles.Person("102", "Bo"));
        f.Load();

        Assert.Equal("101", f.Contacts.Find("anap")?.Id);
    }

    [Fact]
    public void Finds_a_contact_by_its_ID()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("101", "Ana", ("alias", "anap")), ContactFiles.Person("102", "Bo"));
        f.Load();

        Assert.Equal("Bo", f.Contacts.Find("102")?.FullName);
        Assert.Equal("Ana", f.Contacts.Find("101")?.FullName);
    }

    [Fact]
    public void Finds_a_contact_in_any_group_including_me()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("101", "Ana"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("102", "Bo"));
        f.SeedMe(100, ContactFiles.Person("103", "Me"));
        f.Load();

        Assert.Equal("Bo", f.Contacts.Find("102")?.FullName);
        Assert.Equal("Me", f.Contacts.Find("103")?.FullName);
    }

    [Fact]
    public void Compares_names_ignoring_case()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("AB1", "Ana", ("alias", "AnaP")));
        f.Load();

        Assert.Equal("Ana", f.Contacts.Find("anap")?.FullName);
        Assert.Equal("Ana", f.Contacts.Find("ANAP")?.FullName);
        Assert.Equal("Ana", f.Contacts.Find("ab1")?.FullName);
    }

    [Fact]
    public void Finds_nobody_for_an_unknown_name()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("101", "Ana"));
        f.Load();

        Assert.Null(f.Contacts.Find("nobody"));
        Assert.Null(f.Contacts.Find(""));
    }

    [Fact]
    public void Keeps_both_contacts_that_share_an_alias_in_one_file_and_finds_neither()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("alias", "dup")), ContactFiles.Person("2", "Bo", ("alias", "DUP")));

        f.Load();

        Assert.Equal(["1", "2"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Null(f.Contacts.Find("dup"));
        Assert.Equal("Ana", f.Contacts.Find("1")?.FullName);
    }

    [Fact]
    public void Keeps_both_contacts_that_share_an_ID_in_two_files_and_finds_neither()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("same", "Ana"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("same", "Bo"));

        f.Load();

        Assert.Equal(["same"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(["same"], ContactsFixture.Ids(f.Group("friends").Members));
        Assert.Null(f.Contacts.Find("same"));
        Assert.DoesNotContain(f.Contacts.SkippedFiles, file => file.RelativePath.EndsWith("team.json") || file.RelativePath.EndsWith("friends.json"));
    }

    [Fact]
    public void Lists_each_shared_name_once_in_alphabetical_order()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("1", "Ana", ("alias", "zed")),
            ContactFiles.Person("2", "Bo", ("alias", "zed")),
            ContactFiles.Person("3", "Cy", ("alias", "zed")),
            ContactFiles.Person("4", "Di", ("alias", "amy")),
            ContactFiles.Person("5", "Ed", ("alias", "amy")),
            ContactFiles.Person("6", "Flo"));

        f.Load();

        Assert.Equal(["amy", "zed"], f.Contacts.SharedNames);
    }

    [Fact]
    public void Has_no_shared_names_when_every_name_is_held_once()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("alias", "a")), ContactFiles.Person("2", "Bo", ("alias", "b")));

        f.Load();

        Assert.Empty(f.Contacts.SharedNames);
    }

    [Fact]
    public void Counts_one_contacts_alias_equal_to_another_contacts_ID_as_a_shared_name()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("alias", "2")), ContactFiles.Person("2", "Bo"));

        f.Load();

        Assert.Equal(["2"], f.Contacts.SharedNames);
        Assert.Null(f.Contacts.Find("2"));
        Assert.Equal("Ana", f.Contacts.Find("1")?.FullName);
    }

    [Fact]
    public void Does_not_count_a_contacts_alias_equal_to_its_own_ID_as_shared()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("same", "Ana", ("alias", "SAME")));

        f.Load();

        Assert.Empty(f.Contacts.SharedNames);
        Assert.Equal("Ana", f.Contacts.Find("same")?.FullName);
    }
}
