using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>What Contacts holds while no clone is selected.</summary>
[Unit]
public sealed class NoCloneTests
{
    private readonly ContactsFixture f = new();

    private void AssertEmpty()
    {
        Assert.Empty(f.Contacts.Groups);
        Assert.Empty(f.Contacts.SharedNames);
        Assert.Empty(f.Contacts.SkippedFiles);
        Assert.Empty(f.Contacts.DataTypes);
        Assert.Null(f.Contacts.Me);
        Assert.False(f.Contacts.HasContactFiles);
        Assert.Equal(["{fullName}"], f.Contacts.MentionTemplates);
        Assert.Empty(f.Contacts.Search(""));
        Assert.Empty(f.Contacts.Search("ana"));
    }

    [Fact]
    public void Is_empty_before_any_folder_is_selected() => AssertEmpty();

    [Fact]
    public void Is_empty_after_the_selection_is_cleared()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();
        Assert.NotEmpty(f.Contacts.Groups);

        f.Repository.Select(null);

        AssertEmpty();
    }

    [Fact]
    public void Reports_no_contact_files_and_no_skipped_files_for_a_clone_without_contacts()
    {
        f.Load();

        Assert.False(f.Contacts.HasContactFiles);
        Assert.Empty(f.Contacts.SkippedFiles);
    }
}
