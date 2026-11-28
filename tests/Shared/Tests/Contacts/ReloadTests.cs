using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A reload after a sync updates everything Contacts returns.</summary>
[Unit]
public sealed class ReloadTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Updates_the_groups_and_Me()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();

        f.Repository.Update(f.Clone, () =>
        {
            f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bo"));
            f.SeedMe(100, ContactFiles.Person("9", "Me"));
        });

        Assert.Equal(["1", "2"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal("9", f.Contacts.Me?.Id);
    }

    [Fact]
    public void Updates_the_shared_names()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bo"));
        f.Load();
        Assert.Empty(f.Contacts.SharedNames);

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bo", ("alias", "1"))));

        Assert.Equal(["1"], f.Contacts.SharedNames);
    }

    [Fact]
    public void Updates_the_mention_templates()
    {
        f.SeedGroup("team", 1000);
        f.Load();

        f.Repository.Update(f.Clone, () => f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{firstName}", "{fullName}")));

        Assert.Equal(["{firstName}", "{fullName}"], f.Contacts.MentionTemplates);
    }

    [Fact]
    public void Updates_the_data_types()
    {
        f.SeedGroup("team", 1000);
        f.Load();
        Assert.All(f.Contacts.DataTypes, type => Assert.False(type.Loaded));

        f.Repository.Update(f.Clone, f.SeedDataTypes);

        Assert.All(f.Contacts.DataTypes, type => Assert.True(type.Loaded));
    }

    [Fact]
    public void Updates_the_skipped_files()
    {
        f.SeedDataTypes();
        f.Seed(ContactsFixture.TeamFile, "{ not json");
        f.Load();
        Assert.Equal([ContactsFixture.TeamFile], f.SkippedPaths);

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana")));

        Assert.Empty(f.Contacts.SkippedFiles);
        Assert.Equal(["1"], ContactsFixture.Ids(f.Group("team").Members));
    }

    [Fact]
    public void Drops_a_group_whose_file_a_sync_removed()
    {
        f.SeedGroup("team", 1000);
        f.SeedGroup("friends", 2000);
        f.Load();

        f.Repository.Update(f.Clone, () => f.Disk.DeleteFile(f.PathOf($"{ContactsFixture.Folder}/friends.json")));

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
    }
}
