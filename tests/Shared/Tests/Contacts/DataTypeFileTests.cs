using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A data type file that can't be read is always reported, and its key field takes no new choice.</summary>
[Unit]
public sealed class DataTypeFileTests
{
    private readonly ContactsFixture f = new();

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1, "pronouns": { "id": "he" } }""")]
    [InlineData("\"text\"")]
    [InlineData("[]")]
    [InlineData("""[ { "id": "he" } ]""")]
    [InlineData("""{ "pronouns": [] }""")]
    [InlineData("""{ "version": 0, "pronouns": [] }""")]
    [InlineData("""{ "version": "1", "pronouns": [] }""")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 1, "careerPaths": [] }""")]
    [InlineData("""{ "version": 1, "pronouns": [], "note": "x" }""")]
    [InlineData("""{ "version": 1, "pronouns": [ 1 ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "name": "no id" } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "" } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": 5 } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a" }, { "id": "a" } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a" }, { "id": "A" } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a", "n": 5 } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a", "n": true } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a", "n": { "x": "y" } } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a", "n": [ "x", 1 ] } ] }""")]
    [InlineData("""{ "version": 1, "pronouns": [ { "id": "a", "n": [ [ "x" ] ] } ] }""")]
    public void Reports_the_file_and_marks_its_data_type_not_loaded_even_when_the_clone_has_no_contacts(string content)
    {
        f.Seed(ContactsFixture.PronounsFile, content);

        f.Load();

        var type = f.Contacts.DataTypes.Single(type => type.KeyField == "pronounsKey");
        Assert.False(type.Loaded);
        Assert.Empty(type.Entries);
        f.AssertSkipped(ContactsFixture.PronounsFile);
    }

    [Fact]
    public void Reads_the_entries_of_the_list_named_after_the_file()
    {
        f.Seed(ContactsFixture.PronounsFile, ContactFiles.DataType("pronouns", ContactFiles.Object(("id", "he"), ("subject", "he"))));
        f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.DataType("careerPaths", ContactFiles.Object(("id", "em"), ("name", "EM"))));

        f.Load();

        Assert.Equal(["he"], f.Contacts.DataTypes.Single(type => type.KeyField == "pronounsKey").Entries.Select(entry => entry.Id));
        Assert.Equal(["em"], f.Contacts.DataTypes.Single(type => type.KeyField == "careerPathKey").Entries.Select(entry => entry.Id));
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Skips_a_file_with_a_newer_version_and_says_it_needs_an_update()
    {
        f.Seed(ContactsFixture.PronounsFile, """{ "version": 2, "pronouns": [] }""");

        f.Load();

        var skipped = Assert.Single(f.Contacts.SkippedFiles);
        Assert.Equal(ContactsFixture.PronounsFile, skipped.RelativePath);
        Assert.True(skipped.RequiresUpdate);
        Assert.False(f.Contacts.DataTypes.Single(type => type.KeyField == "pronounsKey").Loaded);
    }

    [Fact]
    public void Reports_a_file_with_a_lone_surrogate_and_still_loads_the_contacts_using_its_type()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "he")));
        f.Seed(ContactsFixture.PronounsFile,         """{ "version": 1, "pronouns": [ { "id": "he", "subject": "he\uD800" } ] }""");
                f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.DataType("careerPaths"));

        f.Load();

        f.AssertSkipped(ContactsFixture.PronounsFile);
        Assert.False(f.Contacts.DataTypes.Single(type => type.KeyField == "pronounsKey").Loaded);
        Assert.Equal("he", f.Member("team", "1").PronounsKey);
    }

    [Fact]
    public void Reports_a_file_that_can_not_be_read()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Disk.FailReadsUnder(f.PathOf(ContactsFixture.Folder + "/dataTypes"));

        f.Load();

        Assert.All(f.Contacts.DataTypes, type => Assert.False(type.Loaded));
        f.AssertSkipped(ContactsFixture.PronounsFile);
        f.AssertSkipped(ContactsFixture.CareerPathsFile);
    }

    [Fact]
    public void Still_loads_the_other_data_type()
    {
        f.Seed(ContactsFixture.PronounsFile, "{ not json");
        f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.DataType("careerPaths", ContactFiles.Object(("id", "em"), ("name", "EM"))));

        f.Load();

        Assert.True(f.Contacts.DataTypes.Single(type => type.KeyField == "careerPathKey").Loaded);
        Assert.Equal([ContactsFixture.PronounsFile], f.SkippedPaths);
    }

    [Fact]
    public void Reads_an_empty_list_as_a_loaded_data_type_with_no_entries()
    {
        f.Seed(ContactsFixture.PronounsFile, ContactFiles.DataType("pronouns"));

        f.Load();

        var type = f.Contacts.DataTypes.Single(type => type.KeyField == "pronounsKey");
        Assert.True(type.Loaded);
        Assert.Empty(type.Entries);
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Leaves_a_skipped_data_type_file_unchanged()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "he")));
        f.Seed(ContactsFixture.PronounsFile, "{ not json");
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("team", new Contact("1", "Ana", PronounsKey: "he", Title: "Lead"));

        Assert.Equal("{ not json", f.Text(ContactsFixture.PronounsFile));
    }
}
