using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Creating the contact store and joining it to the synced repository.</summary>
[Unit]
public sealed class ContactStoreCreateTests
{
    [Fact]
    public void Loads_the_groups_when_the_repository_selects_a_folder()
    {
        var f = new ContactsFixture();
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));

        f.Load();

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Gives_the_contacts_and_the_file_rules()
    {
        var f = new ContactsFixture();

        Assert.NotNull(f.Store.Contacts);
        Assert.NotNull(f.Store.Files);
    }

    [Fact]
    public void Rejects_a_missing_file_system()
    {
        Assert.Throws<ArgumentNullException>(() => ContactStore.Create(null!, new SyncedRepository(Logger.None), Logger.None));
    }

    [Fact]
    public void Rejects_a_missing_repository()
    {
        Assert.Throws<ArgumentNullException>(() => ContactStore.Create(new FakeFileSystem(), null!, Logger.None));
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        Assert.Throws<ArgumentNullException>(() => ContactStore.Create(new FakeFileSystem(), new SyncedRepository(Logger.None), null!));
    }

    [Fact]
    public void Rejects_creating_after_the_repository_selected_its_first_folder()
    {
        var repository = new SyncedRepository(Logger.None);
        repository.Select(null);

        Assert.Throws<InvalidOperationException>(() => ContactStore.Create(new FakeFileSystem(), repository, Logger.None));
    }
}
