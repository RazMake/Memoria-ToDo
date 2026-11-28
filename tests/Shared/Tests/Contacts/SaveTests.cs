using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Saving a contact replaces or adds it in its group's file and shows in everything Contacts returns.</summary>
[Unit]
public sealed class SaveTests
{
    private const string FriendsFile = ContactsFixture.Folder + "/friends.json";

    private readonly ContactsFixture f = new();

    public SaveTests()
    {
        f.SeedDataTypes();
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("title", "Dev")), ContactFiles.Person("2", "Bo"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("3", "Cy"));
        f.Load();
        f.ForgetWrites();
    }

    [Fact]
    public void Writes_only_the_file_named_exactly_when_another_group_differs_only_in_case()
    {
        const string teamUpper = ContactsFixture.Folder + "/Team.json";
        f.SeedGroup("Team", 3000, ContactFiles.Person("T1", "Tia"), ContactFiles.Person("T2", "Tom"));
        f.Load();
        f.ForgetWrites();
        var lower = f.Bytes(ContactsFixture.TeamFile);

        f.Contacts.Save("Team", new Contact("T1", "Tia Z"));

        Assert.Equal([teamUpper], f.Written);
        Assert.Equal(lower, f.Bytes(ContactsFixture.TeamFile));
        Assert.Equal("Tia Z", f.Member("Team", "T1").FullName);
    }

    [Fact]
    public void Replaces_the_contact_with_the_same_ID()
    {
        f.Contacts.Save("team", new Contact("1", "Ana Popa", Title: "Lead"));

        Assert.Equal(["1", "2"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(new Contact("1", "Ana Popa", Title: "Lead"), f.Member("team", "1"));
        Assert.Equal("Ana Popa", f.Parsed(ContactsFixture.TeamFile).WithId("1")["fullName"]);
        Assert.Equal("Lead", f.Parsed(ContactsFixture.TeamFile).WithId("1")["title"]);
    }

    [Fact]
    public void Adds_a_contact_whose_ID_the_group_does_not_hold()
    {
        f.Contacts.Save("team", new Contact("7", "Zoe", Alias: "zz"));

        Assert.Equal(["1", "2", "7"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(["1", "2", "7"], f.Parsed(ContactsFixture.TeamFile).Ids);
    }

    [Fact]
    public void Writes_only_that_groups_file()
    {
        var friendsBefore = f.Bytes(FriendsFile);

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal([ContactsFixture.TeamFile], f.Written);
        Assert.Equal(friendsBefore, f.Bytes(FriendsFile));
    }

    [Fact]
    public void Shows_the_saved_contact_in_Find_and_Search()
    {
        f.Contacts.Save("friends", new Contact("3", "Cyrus", Alias: "cy", Title: "Painter"));

        Assert.Equal("Cyrus", f.Contacts.Find("cy")?.FullName);
        Assert.Equal("Cyrus", f.Contacts.Find("3")?.FullName);
        Assert.Equal(["3"], ContactsFixture.Ids(f.Contacts.Search("painter")));
    }

    [Fact]
    public void Keeps_the_group_members_sorted_after_the_save()
    {
        f.Contacts.Save("team", new Contact("1", "Zed"));

        Assert.Equal(["2", "1"], ContactsFixture.Ids(f.Group("team").Members));
    }

    [Fact]
    public void Saves_into_a_group_with_no_members_yet()
    {
        f.Seed($"{ContactsFixture.Folder}/empty.json", ContactFiles.Group(3000));
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("empty", new Contact("8", "Hal"));

        Assert.Equal(["8"], ContactsFixture.Ids(f.Group("empty").Members));
    }

    [Fact]
    public void Saves_the_user_s_own_contact_when_its_ID_stays()
    {
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("me", new Contact("42", "Me Myself", Title: "Owner"));

        Assert.Equal(new Contact("42", "Me Myself", Title: "Owner"), f.Contacts.Me);
        Assert.Equal([ContactsFixture.MeFile], f.Written);
    }

    [Fact]
    public void Saves_a_contact_that_reports_to_another_contact()
    {
        f.Contacts.Save("team", new Contact("1", "Ana", ReportsTo: "2"));

        Assert.Equal("Bo", f.Contacts.ManagerOf(f.Member("team", "1"))?.FullName);
    }
}
