using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>
/// What Contacts logs: each save, delete, and group move, naming what changed. Entries are checked by level and the
/// facts their message names, never by wording.
/// </summary>
[Unit]
public sealed class ContactLoggingTests
{
    private readonly ContactsFixture f = new();

    public ContactLoggingTests()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana Popa"), ContactFiles.Person("2", "Bo Bell"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("4", "Di Dunn"));
        f.Load();
    }

    private IReadOnlyList<LogEvent> During(Action operation)
    {
        var before = f.Logs.Events.Count;
        operation();
        return f.Logs.After(before);
    }

    [Fact]
    public void Logs_saving_a_contact_naming_its_id_name_and_group_file()
    {
        var entries = During(() => f.Contacts.Save("team", new Contact("1", "Ana Renamed")));

        entries.SingleAt(LogEventLevel.Information, "1", "Ana Renamed", "team.json");
    }

    [Fact]
    public void Logs_deleting_a_contact_naming_its_id_name_and_group_file()
    {
        var entries = During(() => f.Contacts.Delete("team", "2"));

        entries.SingleAt(LogEventLevel.Information, "2", "Bo Bell", "team.json");
    }

    [Fact]
    public void Logs_moving_a_group_naming_the_group_file()
    {
        var entries = During(() => f.Contacts.MoveGroup("friends", null, "team"));

        entries.SingleAt(LogEventLevel.Information, "friends.json");
    }

    [Fact]
    public void Logs_nothing_for_a_save_that_is_refused()
    {
        var entries = During(() => Assert.Throws<ArgumentException>(() => f.Contacts.Save("TEAM", new Contact("1", "Ana"))));

        Assert.Empty(entries);
    }
}
