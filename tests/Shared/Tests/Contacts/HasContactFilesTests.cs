using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Whether the clone has contacts: at least one group file, skipped or not.</summary>
[Unit]
public sealed class HasContactFilesTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Is_true_with_one_group_file()
    {
        f.SeedGroup("team", 1000);

        f.Load();

        Assert.True(f.Contacts.HasContactFiles);
    }

    [Fact]
    public void Is_true_when_the_only_group_file_is_skipped()
    {
        f.Seed(ContactsFixture.TeamFile, "{ not json");

        f.Load();

        Assert.True(f.Contacts.HasContactFiles);
        Assert.Empty(f.Contacts.Groups);
    }

    [Fact]
    public void Is_false_with_only_data_type_files()
    {
        f.SeedDataTypes();

        f.Load();

        Assert.False(f.Contacts.HasContactFiles);
    }

    [Fact]
    public void Is_false_with_only_the_templates_file()
    {
        f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{fullName}"));

        f.Load();

        Assert.False(f.Contacts.HasContactFiles);
    }

    [Fact]
    public void Is_false_when_the_folder_has_only_files_that_are_not_groups()
    {
        f.Seed($"{ContactsFixture.Folder}/notes.txt", "hello");

        f.Load();

        Assert.False(f.Contacts.HasContactFiles);
    }

    [Fact]
    public void Raises_changed_when_a_reload_finds_the_first_group_file()
    {
        f.Load();
        var before = f.Notices.Count;

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000));

        Assert.True(f.Contacts.HasContactFiles);
        Assert.Equal(before + 1, f.Notices.Count(notice => notice == "Changed"));
    }

    [Fact]
    public void Turns_false_again_when_a_reload_finds_the_last_group_file_gone()
    {
        f.SeedGroup("team", 1000);
        f.Load();

        f.Repository.Update(f.Clone, () => f.Disk.DeleteFile(f.PathOf(ContactsFixture.TeamFile)));

        Assert.False(f.Contacts.HasContactFiles);
    }
}
