using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A contact's manager and direct reports, through reportsTo.</summary>
[Unit]
public sealed class ReportsToTests
{
    private readonly ContactsFixture f = new();

    private Contact Ana => f.Member("team", "1");

    [Fact]
    public void Finds_the_manager_by_alias()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("reportsTo", "boss")), ContactFiles.Person("9", "Big Boss", ("alias", "boss")));
        f.Load();

        Assert.Equal("9", f.Contacts.ManagerOf(Ana)?.Id);
    }

    [Fact]
    public void Finds_the_manager_by_ID()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("reportsTo", "9")), ContactFiles.Person("9", "Big Boss"));
        f.Load();

        Assert.Equal("Big Boss", f.Contacts.ManagerOf(Ana)?.FullName);
    }

    [Fact]
    public void Finds_a_manager_in_another_group_ignoring_case()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("reportsTo", "BOSS")));
        f.SeedGroup("friends", 2000, ContactFiles.Person("9", "Big Boss", ("alias", "boss")));
        f.Load();

        Assert.Equal("9", f.Contacts.ManagerOf(Ana)?.Id);
    }

    [Fact]
    public void Finds_no_manager_for_an_unknown_name()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("reportsTo", "ghost")));
        f.Load();

        Assert.Null(f.Contacts.ManagerOf(Ana));
    }

    [Fact]
    public void Finds_no_manager_for_a_shared_name()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("1", "Ana", ("reportsTo", "dup")),
            ContactFiles.Person("8", "Eight", ("alias", "dup")),
            ContactFiles.Person("9", "Nine", ("alias", "dup")));
        f.Load();

        Assert.Null(f.Contacts.ManagerOf(Ana));
    }

    [Fact]
    public void Finds_no_manager_when_reportsTo_is_missing()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();

        Assert.Null(f.Contacts.ManagerOf(Ana));
    }

    [Fact]
    public void Lists_the_direct_reports_that_name_the_contact_by_alias_or_ID_sorted_by_full_name_then_ID()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("9", "Big Boss", ("alias", "boss")),
            ContactFiles.Person("5", "Zoe", ("reportsTo", "boss")),
            ContactFiles.Person("4", "Ana", ("reportsTo", "9")),
            ContactFiles.Person("3", "ana", ("reportsTo", "BOSS")),
            ContactFiles.Person("2", "Mia", ("reportsTo", "someone")),
            ContactFiles.Person("1", "Lone"));
        f.Load();

        var reports = f.Contacts.DirectReportsOf(f.Member("team", "9"));

        Assert.Equal(["3", "4", "5"], ContactsFixture.Ids(reports));
    }

    [Fact]
    public void Lists_no_direct_reports_for_a_contact_nobody_reports_to()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bo", ("reportsTo", "1")));
        f.Load();

        Assert.Empty(f.Contacts.DirectReportsOf(f.Member("team", "2")));
    }

    [Fact]
    public void Lists_the_direct_reports_only_for_the_manager_the_name_finds_when_another_contact_shares_its_ID()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("ana", "Ana One", ("alias", "boss")),
            ContactFiles.Person("3", "Cy", ("reportsTo", "boss")));
        f.SeedGroup("friends", 2000, ContactFiles.Person("ana", "Ana Two"));
        f.Load();

        Assert.Equal(["3"], ContactsFixture.Ids(f.Contacts.DirectReportsOf(f.Member("team", "ana"))));
        Assert.Empty(f.Contacts.DirectReportsOf(f.Member("friends", "ana")));
    }

    [Fact]
    public void Lists_no_direct_reports_for_a_contact_whose_name_is_shared()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("1", "Ana", ("alias", "dup")),
            ContactFiles.Person("2", "Bo", ("alias", "dup")),
            ContactFiles.Person("3", "Cy", ("reportsTo", "dup")));
        f.Load();

        Assert.Empty(f.Contacts.DirectReportsOf(f.Member("team", "1")));
    }
}
