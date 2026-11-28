using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>
/// Saving through a change hands the change the group's latest contact under the repository's lock, then saves the contact it
/// returns exactly as Save does, so a caller that changes some fields keeps the latest value of the others.
/// </summary>
[Unit]
public sealed class SaveThroughChangeTests
{
    private readonly ContactsFixture f = new();

    public SaveThroughChangeTests()
    {
        f.SeedDataTypes();
        f.SeedGroup("team", 1000, ContactFiles.Person("a1", "Ana", ("title", "Dev"), ("alias", "anap")), ContactFiles.Person("2", "Bo"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("3", "Cy"));
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        f.Load();
        f.ForgetWrites();
    }

    private Contact? Handed(string group, string id)
    {
        Contact? handed = null;
        f.Contacts.Save(group, id, current =>
        {
            handed = current;
            return current ?? new Contact(id, "Someone");
        });
        return handed;
    }

    [Fact]
    public void Hands_the_change_the_group_s_contact_with_the_ID()
    {
        Assert.Equal(new Contact("a1", "Ana", Alias: "anap", Title: "Dev"), Handed("team", "a1"));
    }

    [Fact]
    public void Matches_the_ID_ignoring_case_and_replaces_that_contact()
    {
        var handed = Handed("team", "A1");

        Assert.Equal("a1", handed?.Id);
        Assert.Equal(["2", "a1"], ContactsFixture.Ids(f.Group("team").Members).Order(StringComparer.Ordinal));
        Assert.Equal(2, f.Group("team").Members.Count);
    }

    [Fact]
    public void Hands_the_change_nothing_when_the_group_holds_no_contact_with_the_ID_even_if_another_group_does()
    {
        Contact? handed = new("unset", "Unset");
        var stop = new InvalidOperationException();

        // The change stops the save: a new "3" in team would clash with friends' "3", which isn't this test's point.
        var thrown = Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", "3", current =>
        {
            handed = current;
            throw stop;
        }));

        Assert.Same(stop, thrown);
        Assert.Null(handed);
    }

    [Fact]
    public void Saves_the_contact_the_change_returns_over_the_one_it_was_handed()
    {
        f.Contacts.Save("team", "a1", current => current! with { FullName = "Ana Popa" });

        Assert.Equal(new Contact("a1", "Ana Popa", Alias: "anap", Title: "Dev"), f.Member("team", "a1"));
        Assert.Equal("Ana Popa", f.Parsed(ContactsFixture.TeamFile).WithId("a1")["fullName"]);
        Assert.Equal("Dev", f.Parsed(ContactsFixture.TeamFile).WithId("a1")["title"]);
    }

    [Fact]
    public void Adds_the_contact_the_change_returns_when_the_group_holds_none_with_the_ID()
    {
        f.Contacts.Save("team", "7", current => new Contact("7", "Zoe"));

        Assert.Equal(["2", "7", "a1"], ContactsFixture.Ids(f.Group("team").Members).Order(StringComparer.Ordinal));
        Assert.Equal("Zoe", f.Contacts.Find("7")?.FullName);
    }

    [Fact]
    public void Writes_only_that_groups_file()
    {
        f.Contacts.Save("team", "a1", current => current! with { FullName = "Ana Popa" });

        Assert.Equal([ContactsFixture.TeamFile], f.Written);
    }

    [Fact]
    public void Hands_the_change_the_latest_contact_after_an_earlier_save()
    {
        f.Contacts.Save("team", new Contact("a1", "Ana", Alias: "anap", Title: "Lead"));

        Assert.Equal("Lead", Handed("team", "a1")?.Title);
    }

    [Fact]
    public void Runs_the_change_under_the_repository_s_lock_so_a_save_inside_it_is_refused()
    {
        Exception? inside = null;
        Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", "a1", current =>
        {
            try
            {
                f.Contacts.Save("team", new Contact("2", "Bo Bell"));
            }
            catch (Exception e)
            {
                inside = e;
                throw;
            }

            return current!;
        }));

        Assert.IsType<InvalidOperationException>(inside);
        f.AssertNothingWritten();
        Assert.Equal("Bo", f.Member("team", "2").FullName);
    }

    [Fact]
    public void Refuses_a_returned_contact_that_Save_would_refuse_and_writes_nothing()
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        Assert.Throws<ArgumentException>(() => f.Contacts.Save("team", "a1", current => current! with { FullName = "" }));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Refuses_a_returned_contact_with_a_name_another_contact_holds()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Save("team", "2", current => current! with { Alias = "anap" }));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Refuses_a_returned_contact_with_a_key_its_data_type_does_not_list()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Save("team", "2", current => current! with { PronounsKey = "zz" }));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Refuses_a_new_contact_in_the_user_s_own_group()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Save("me", "43", current => new Contact("43", "Me Too")));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Refuses_a_new_ID_for_the_user_s_own_contact()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Save("me", "42", current => current! with { Id = "43" }));

        f.AssertNothingWritten();
        Assert.Equal("42", f.Contacts.Me?.Id);
    }

    [Fact]
    public void Saves_the_user_s_own_contact_when_its_ID_stays()
    {
        f.Contacts.Save("me", "42", current => current! with { Title = "Owner" });

        Assert.Equal(new Contact("42", "Me", Title: "Owner"), f.Contacts.Me);
        Assert.Equal([ContactsFixture.MeFile], f.Written);
    }

    [Fact]
    public void Passes_on_the_exception_the_change_throws_as_it_is_and_writes_nothing()
    {
        var failure = new InvalidOperationException("change");
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", "a1", _ => throw failure));

        Assert.Same(failure, thrown);
        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Refuses_a_change_that_returns_no_contact_and_writes_nothing()
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        Assert.Throws<ArgumentException>(() => f.Contacts.Save("team", "a1", _ => null!));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Rejects_an_unknown_group_without_calling_the_change()
    {
        var called = false;

        Assert.Throws<ArgumentException>(() => f.Contacts.Save("TEAM", "a1", current => { called = true; return current!; }));

        Assert.False(called);
        f.AssertNothingWritten();
    }

    [Fact]
    public void Rejects_a_group_whose_file_was_skipped_without_calling_the_change()
    {
        f.Seed(ContactsFixture.Folder + "/broken.json", "{ not json");
        f.Load();
        f.ForgetWrites();
        var called = false;

        Assert.Throws<SkippedFileException>(() => f.Contacts.Save("broken", "1", current => { called = true; return current!; }));

        Assert.False(called);
        f.AssertNothingWritten();
    }

    [Fact]
    public void Needs_a_clone_and_does_not_call_the_change()
    {
        f.Repository.Select(null);
        var called = false;

        Assert.Throws<NoRepositoryFolderException>(() => f.Contacts.Save("team", "a1", current => { called = true; return current!; }));

        Assert.False(called);
        f.AssertNothingWritten();
    }

    [Fact]
    public void Raises_Changed_then_the_repository_s_LocalSaved()
    {
        var before = f.Notices.Count;

        f.Contacts.Save("team", "a1", current => current! with { FullName = "Ana Popa" });

        Assert.Equal(["Changed", "LocalSaved"], [.. f.Notices.Skip(before)]);
    }

    [Fact]
    public void Logs_the_save_naming_its_id_name_and_group_file()
    {
        var before = f.Logs.Events.Count;

        f.Contacts.Save("team", "a1", current => current! with { FullName = "Ana Renamed" });

        f.Logs.After(before).SingleAt(LogEventLevel.Information, "a1", "Ana Renamed", "team.json");
    }
}
