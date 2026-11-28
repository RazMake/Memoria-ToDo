using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Deleting a contact removes it from its group's file.</summary>
[Unit]
public sealed class DeleteTests
{
    private const string FriendsFile = ContactsFixture.Folder + "/friends.json";

    private readonly ContactsFixture f = new();

    public DeleteTests()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("1", "Ana", ("alias", "anap")),
            ContactFiles.Person("2", "Bo", ("reportsTo", "anap")),
            ContactFiles.Person("3", "Cy", ("reportsTo", "1")));
        f.SeedGroup("friends", 2000, ContactFiles.Person("4", "Di"));
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        f.Load();
        f.ForgetWrites();
    }

    [Fact]
    public void Removes_the_contact_and_writes_only_its_groups_file()
    {
        var friendsBefore = f.Bytes(FriendsFile);

        f.Contacts.Delete("team", "2");

        Assert.Equal(["1", "3"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(["1", "3"], f.Parsed(ContactsFixture.TeamFile).Ids);
        Assert.Equal([ContactsFixture.TeamFile], f.Written);
        Assert.Equal(friendsBefore, f.Bytes(FriendsFile));
        Assert.Null(f.Contacts.Find("2"));
        Assert.Empty(f.Contacts.Search("Bo"));
    }

    [Fact]
    public void Deletes_only_from_the_file_named_exactly_when_another_group_differs_only_in_case()
    {
        const string teamUpper = ContactsFixture.Folder + "/Team.json";
        f.SeedGroup("Team", 3000, ContactFiles.Person("T1", "Tia"), ContactFiles.Person("T2", "Tom"));
        f.Load();
        f.ForgetWrites();
        var lower = f.Bytes(ContactsFixture.TeamFile);

        f.Contacts.Delete("Team", "T2");

        Assert.Equal([teamUpper], f.Written);
        Assert.Equal(lower, f.Bytes(ContactsFixture.TeamFile));
        Assert.Equal(["T1"], ContactsFixture.Ids(f.Group("Team").Members));
    }

    [Fact]
    public void Keeps_an_emptied_file_as_a_group_with_no_members()
    {
        f.Contacts.Delete("friends", "4");

        Assert.Empty(f.Group("friends").Members);
        Assert.Equal(2000, f.Group("friends").Position);
        Assert.True(f.Exists(FriendsFile));
        Assert.Empty(f.Parsed(FriendsFile).Objects);
        Assert.Equal(2000, f.Parsed(FriendsFile).Position);
    }

    [Fact]
    public void Leaves_a_reportsTo_that_named_it_finding_nobody()
    {
        f.Contacts.Delete("team", "1");

        Assert.Null(f.Contacts.ManagerOf(f.Member("team", "2")));
        Assert.Null(f.Contacts.ManagerOf(f.Member("team", "3")));
    }

    [Fact]
    public void Leaves_a_mention_of_it_without_live_text()
    {
        Assert.Equal("Ana", f.Contacts.TextOf(ContactAddress.Of("1"), "{fullName}"));

        f.Contacts.Delete("team", "1");

        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("1"), "{fullName}"));
    }

    [Fact]
    public void Raises_Changed_then_LocalSaved()
    {
        var before = f.Notices.Count;

        f.Contacts.Delete("team", "2");

        Assert.Equal(["Changed", "LocalSaved"], f.Notices.Skip(before));
    }

    [Theory]
    [InlineData("me", "42")]
    [InlineData("nobody", "1")]
    [InlineData("team", "99")]
    [InlineData("friends", "1")]
    public void Rejects_the_user_s_own_contact_an_unknown_group_or_an_unknown_ID_and_writes_nothing(string group, string id)
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        Assert.Throws<ArgumentException>(() => f.Contacts.Delete(group, id));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Rejects_a_group_whose_file_was_skipped_and_writes_nothing()
    {
        f.Seed($"{ContactsFixture.Folder}/broken.json", "{ not json");
        f.Load();
        f.ForgetWrites();

        Assert.Throws<SkippedFileException>(() => f.Contacts.Delete("broken", "1"));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Needs_a_clone()
    {
        f.Repository.Select(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Contacts.Delete("team", "1"));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Passes_on_the_file_system_s_exception_and_changes_nothing_when_the_write_fails()
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;
        f.Disk.FailWritesUnder(f.PathOf(ContactsFixture.Folder));

        Assert.Throws<IOException>(() => f.Contacts.Delete("team", "1"));

        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Rejects_a_delete_made_while_this_thread_holds_the_repository_lock_before_writing()
    {
        Assert.Throws<InvalidOperationException>(() => f.Repository.Locked(_ => { f.Contacts.Delete("team", "1"); }));

        f.AssertNothingWritten();
        Assert.Equal(["1", "2", "3"], ContactsFixture.Ids(f.Group("team").Members));
    }
}
