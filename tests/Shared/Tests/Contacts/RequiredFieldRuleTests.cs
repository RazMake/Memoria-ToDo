using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The fields every file and contact must have: position, id, and fullName.</summary>
[Unit]
public sealed class RequiredFieldRuleTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Skips_a_group_file_without_a_position()
    {
        f.Seed(ContactsFixture.TeamFile, """{ "version": 1, "members": [ { "id": "1", "fullName": "Ana" } ] }""");

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Empty(f.Contacts.Groups);
    }

    [Fact]
    public void Skips_me_json_without_a_position()
    {
        f.Seed(ContactsFixture.MeFile, """{ "version": 1, "details": { "id": "1", "fullName": "Ana" } }""");

        f.Load();

        f.AssertSkipped(ContactsFixture.MeFile);
        Assert.Null(f.Contacts.Me);
    }

    [Theory]
    [InlineData("""{ "fullName": "Ana" }""")]
    [InlineData("""{ "id": "", "fullName": "Ana" }""")]
    [InlineData("""{ "id": "1" }""")]
    [InlineData("""{ "id": "1", "fullName": "" }""")]
    public void Skips_a_group_file_with_a_contact_missing_its_id_or_full_name(string member)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("2", "Bo"), member);

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Empty(f.Contacts.Groups);
    }

    [Theory]
    [InlineData("""{ "fullName": "Ana" }""")]
    [InlineData("""{ "id": "", "fullName": "Ana" }""")]
    [InlineData("""{ "id": "1" }""")]
    [InlineData("""{ "id": "1", "fullName": "" }""")]
    public void Skips_me_json_whose_details_miss_the_id_or_full_name(string details)
    {
        f.SeedMe(100, details);

        f.Load();

        f.AssertSkipped(ContactsFixture.MeFile);
        Assert.Null(f.Contacts.Me);
    }

    [Fact]
    public void Skips_me_json_without_a_details_object()
    {
        f.Seed(ContactsFixture.MeFile, """{ "version": 1, "position": 100 }""");

        f.Load();

        f.AssertSkipped(ContactsFixture.MeFile);
        Assert.Null(f.Contacts.Me);
    }

    [Fact]
    public void Skips_me_json_whose_details_are_not_an_object()
    {
        f.SeedMe(100, "[ 1 ]");

        f.Load();

        f.AssertSkipped(ContactsFixture.MeFile);
    }

    [Fact]
    public void Skips_a_group_file_whose_position_is_not_a_whole_number()
    {
        f.Seed(ContactsFixture.TeamFile, """{ "version": 1, "position": "1000", "members": [] }""");

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }
}
