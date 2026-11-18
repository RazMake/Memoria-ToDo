using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>When a save can't happen: a skipped group, no clone, a failed write, the repository's lock, and missing arguments.</summary>
[Unit]
public sealed class SaveFailureTests
{
    private const string BrokenFile = ContactsFixture.Folder + "/broken.json";

    private readonly ContactsFixture f = new();

    private void LoadTeam()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();
        f.ForgetWrites();
    }

    [Fact]
    public void Rejects_a_save_into_a_group_whose_file_was_skipped_and_leaves_the_file_unchanged()
    {
        f.Seed(BrokenFile, "{ not json");
        f.Load();
        f.ForgetWrites();

        Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("broken", new Contact("1", "Ana")));

        f.AssertNothingWritten();
        Assert.Equal("{ not json", f.Text(BrokenFile));
    }

    [Fact]
    public void Rejects_a_save_of_the_user_s_own_contact_when_me_json_was_skipped()
    {
        f.Seed(ContactsFixture.MeFile, "{ not json");
        f.Load();
        f.ForgetWrites();

        Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("me", new Contact("42", "Me")));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Needs_a_clone()
    {
        f.Repository.Select(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Contacts.Save("team", new Contact("1", "Ana")));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Passes_on_the_file_system_s_exception_when_the_write_fails()
    {
        LoadTeam();
        f.Disk.FailWritesUnder(f.PathOf(ContactsFixture.Folder));

        Assert.Throws<IOException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));
    }

    [Fact]
    public void Changes_nothing_in_memory_and_raises_nothing_when_the_write_fails()
    {
        LoadTeam();
        var before = f.Snapshot();
        var notices = f.Notices.Count;
        f.Disk.FailWritesUnder(f.PathOf(ContactsFixture.Folder));

        Assert.Throws<IOException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));
        Assert.Throws<IOException>(() => f.Contacts.Save("team", new Contact("2", "New")));

        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
        Assert.Null(f.Contacts.Find("2"));
    }

    [Fact]
    public void Saves_again_once_the_write_works()
    {
        LoadTeam();
        f.Disk.FailWritesUnder(f.PathOf(ContactsFixture.Folder));
        Assert.Throws<IOException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));
        f.Disk.StopFailing();

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal("Ana Popa", f.Member("team", "1").FullName);
    }

    [Fact]
    public void Rejects_a_save_into_a_group_name_that_differs_only_in_case_from_a_loaded_one()
    {
        LoadTeam();

        Assert.Throws<ArgumentException>(() => f.Contacts.Save("TEAM", new Contact("1", "Ana Popa")));

        f.AssertNothingWritten();
        Assert.Equal("Ana", f.Member("team", "1").FullName);
    }

    [Fact]
    public void Saves_into_the_group_whose_file_loaded_and_rejects_the_one_whose_name_differs_only_in_case_and_was_skipped()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");
        f.SeedGroup("Team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("Team", new Contact("1", "Ana Popa"));
        Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));

        Assert.Equal([ContactsFixture.Folder + "/Team.json"], f.Written);
        Assert.Equal("{ not json", f.Text(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Rejects_a_save_made_while_this_thread_holds_the_repository_lock_before_writing()
    {
        LoadTeam();

        Assert.Throws<InvalidOperationException>(() =>
            f.Repository.Locked(_ => { f.Contacts.Save("team", new Contact("1", "Ana Popa")); }));

        f.AssertNothingWritten();
        Assert.Equal("Ana", f.Member("team", "1").FullName);
    }

    [Fact]
    public void Rejects_a_missing_contact()
    {
        LoadTeam();

        Assert.Throws<ArgumentNullException>(() => f.Contacts.Save("team", null!));
    }

    [Fact]
    public void Rejects_a_missing_group_name()
    {
        LoadTeam();

        Assert.Throws<ArgumentNullException>(() => f.Contacts.Save(null!, new Contact("1", "Ana")));
    }
}
