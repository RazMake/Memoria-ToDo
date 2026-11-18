using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The data types key fields choose from: which file each reads, and what its entries show.</summary>
[Unit]
public sealed class DataTypeTests
{
    private const string DataTypes = ContactsFixture.Folder + "/dataTypes";

    private readonly ContactsFixture f = new();

    private DataType Type(string keyField) => f.Contacts.DataTypes.Single(type => type.KeyField == keyField);

    [Fact]
    public void Lists_pronouns_then_career_paths_with_the_file_each_reads()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();

        f.Load();

        Assert.Equal(["pronounsKey", "careerPathKey"], f.Contacts.DataTypes.Select(type => type.KeyField));
        Assert.Equal([$"{DataTypes}/pronouns.json", $"{DataTypes}/careerPaths.json"], f.Contacts.DataTypes.Select(type => type.RelativePath));
        Assert.All(f.Contacts.DataTypes, type => Assert.True(type.Loaded));
    }

    [Fact]
    public void Lists_the_entries_in_file_order_showing_the_name_or_else_the_ID()
    {
        f.SeedGroup("team", 1000);
        f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.ArrayOf(
            ContactFiles.Object(("id", "zz"), ("name", "Zebra Keeper")),
            ContactFiles.Object(("id", "aa"), ("short", "AA"))));
        f.Load();

        var entries = Type("careerPathKey").Entries;

        Assert.Equal(["zz", "aa"], entries.Select(entry => entry.Id));
        Assert.Equal(["Zebra Keeper", "aa"], entries.Select(entry => entry.Shown));
    }

    [Fact]
    public void Holds_every_text_property_except_the_ID_and_leaves_list_properties_out()
    {
        f.SeedGroup("team", 1000);
        f.Seed(ContactsFixture.CareerPathsFile,
            """[ { "id": "em", "name": "Engineering Manager", "short": "EM", "alternatives": [ "Lead", "Head" ] } ]""");
        f.Load();

        var entry = Assert.Single(Type("careerPathKey").Entries);

        Assert.Equal(new Dictionary<string, string> { ["name"] = "Engineering Manager", ["short"] = "EM" }, entry.Text);
    }

    [Fact]
    public void Reads_the_pronoun_properties_as_text()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Load();

        var he = Type("pronounsKey").Entries.Single(entry => entry.Id == "he");

        Assert.Equal("he", he.Shown);
        Assert.Equal(new Dictionary<string, string> { ["subject"] = "he", ["object"] = "him" }, he.Text);
    }

    [Fact]
    public void Marks_a_missing_file_not_loaded_and_reports_it_when_the_clone_has_contacts()
    {
        f.SeedGroup("team", 1000);
        f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.ArrayOf(ContactFiles.Object(("id", "em"))));

        f.Load();

        Assert.False(Type("pronounsKey").Loaded);
        Assert.Empty(Type("pronounsKey").Entries);
        Assert.True(Type("careerPathKey").Loaded);
        f.AssertSkipped(ContactsFixture.PronounsFile);
        Assert.Equal([ContactsFixture.PronounsFile], f.SkippedPaths);
    }

    [Fact]
    public void Marks_a_missing_file_not_loaded_without_reporting_it_when_the_clone_has_no_contacts()
    {
        f.Load();

        Assert.All(f.Contacts.DataTypes, type => Assert.False(type.Loaded));
        Assert.All(f.Contacts.DataTypes, type => Assert.Empty(type.Entries));
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Reports_a_missing_file_once_a_reload_finds_contacts()
    {
        f.Load();
        Assert.Empty(f.Contacts.SkippedFiles);

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000));

        Assert.Equal(
            new[] { ContactsFixture.PronounsFile, ContactsFixture.CareerPathsFile }.Order(StringComparer.Ordinal),
            f.SkippedPaths.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Loads_the_contacts_that_use_a_skipped_data_type()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "he"), ("careerPathKey", "em")));
        f.Seed(ContactsFixture.PronounsFile, "{ not json");
        f.Seed(ContactsFixture.CareerPathsFile, """[ { "name": "no id" } ]""");

        f.Load();

        Assert.Equal([new Contact("1", "Ana", PronounsKey: "he", CareerPathKey: "em")], f.Group("team").Members);
        Assert.All(f.Contacts.DataTypes, type => Assert.False(type.Loaded));
    }

    [Fact]
    public void Keeps_and_saves_a_key_the_file_does_not_list_unchanged()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "xe"), ("careerPathKey", "zz")));
        f.SeedDataTypes();
        f.Load();

        Assert.Equal("xe", f.Member("team", "1").PronounsKey);

        f.Contacts.Save("team", f.Member("team", "1") with { Title = "Lead" });

        var saved = f.Parsed(ContactsFixture.TeamFile).WithId("1");
        Assert.Equal("xe", saved["pronounsKey"]);
        Assert.Equal("zz", saved["careerPathKey"]);
        Assert.Equal("Lead", saved["title"]);
    }
}
