using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>How a contact's fields are read: empty values, unknown fields, and team fields in any group.</summary>
[Unit]
public sealed class ReadingFieldsTests
{
    private static readonly string[] OptionalFields =
        ["alias", "firstName", "nickname", "title", "location", "levelId", "workingType", "pronounsKey", "careerPathKey", "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate"];

    private readonly ContactsFixture f = new();

    public ReadingFieldsTests() => f.SeedDataTypes();

    public static TheoryData<string> Optional
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in OptionalFields)
                data.Add(name);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Optional))]
    public void Counts_an_empty_value_as_missing(string field)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", (field, "")));

        f.Load();

        Assert.Equal([new Contact("1", "Ana")], f.Group("team").Members);
    }

    [Fact]
    public void Loads_every_known_field()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana Popa",
            ("alias", "anap"),             ("firstName", "Ana"), ("nickname", "Nia"), ("title", "Lead"), ("pronounsKey", "she"), ("careerPathKey", "em"),
            ("levelId", "l62"), ("workingType", "Remote"), ("location", "Oslo"), ("reportsTo", "boss"),
            ("dayStartTime", "9:30"), ("levelStartDate", "2025-03-01"), ("teamMemberSince", "2020-01-02"), ("exitDate", "2026-02-03")));

        f.Load();

        Assert.Equal(
            [new Contact("1", "Ana Popa", "anap", "Ana", "Lead", "she", "em", "l62", "Remote", "Oslo", "boss",
                            new TimeOnly(9, 30), new DateOnly(2025, 3, 1), new DateOnly(2020, 1, 2), new DateOnly(2026, 2, 3), "Nia")],
            f.Group("team").Members);
    }

                [Fact]
                public void Loads_a_contact_without_a_nickname_that_reads_as_no_stored_text_and_shows_the_first_name()
                {
                    f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana Popa", ("firstName", "Ana")));

                    f.Load();

                    var ana = Assert.Single(f.Group("team").Members);
                    Assert.Null(ContactFields.All.Single(field => field.Name == "nickname").TextOf(ana));
                    Assert.Equal("Ana", ana.Nickname);
                    Assert.Empty(f.Contacts.SkippedFiles);
                }

                [Fact]
                public void Skips_a_file_whose_nickname_is_null()
                {
                    f.Seed(ContactsFixture.TeamFile, """{ "version": 1, "position": 1000, "members": [ { "id": "1", "fullName": "Ana", "nickname": null } ] }""");

                    f.Load();

                    f.AssertSkipped(ContactsFixture.TeamFile);
                    Assert.Empty(f.Contacts.Groups);
                }

                [Fact]
                public void Loads_a_nickname_with_spaces()
                {
                    f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("nickname", "Ana Maria")));

                    f.Load();

                    Assert.Equal("Ana Maria", Assert.Single(f.Group("team").Members).Nickname);
                }

    [Fact]
    public void Loads_a_contact_with_unknown_fields_of_any_kind()
    {
        f.Seed(ContactsFixture.TeamFile,
            """{ "version": 1, "position": 1000, "extra": 5, "members": [ { "id": "1", "fullName": "Ana", "nick": "A", "score": 7, "tags": [ "x" ], "more": { "a": null } } ] }""");

        f.Load();

        Assert.Equal([new Contact("1", "Ana")], f.Group("team").Members);
        Assert.DoesNotContain(f.Contacts.SkippedFiles, file => file.RelativePath == ContactsFixture.TeamFile);
    }

    [Theory]
    [InlineData("team")]
    [InlineData("ex-team")]
    [InlineData("friends")]
    public void Loads_the_team_fields_in_any_group(string group)
    {
        f.SeedGroup(group, 1000, ContactFiles.Person("1", "Ana",
            ("location", "Oslo"), ("levelId", "l62"), ("workingType", "Remote"),
            ("dayStartTime", "09:30"), ("levelStartDate", "2025-03-01"), ("teamMemberSince", "2020-01-02"), ("exitDate", "2026-02-03")));

        f.Load();

        var ana = Assert.Single(f.Group(group).Members);
        Assert.Equal("Oslo", ana.Location);
        Assert.Equal("l62", ana.LevelId);
        Assert.Equal("Remote", ana.WorkingType);
        Assert.Equal(new TimeOnly(9, 30), ana.DayStartTime);
        Assert.Equal(new DateOnly(2025, 3, 1), ana.LevelStartDate);
        Assert.Equal(new DateOnly(2020, 1, 2), ana.TeamMemberSince);
        Assert.Equal(new DateOnly(2026, 2, 3), ana.ExitDate);
    }

    [Fact]
    public void Loads_team_fields_in_me_json()
    {
        f.SeedMe(100, ContactFiles.Person("1", "Ana", ("levelId", "l62"), ("dayStartTime", "8:00")));

        f.Load();

        Assert.Equal(new Contact("1", "Ana", LevelId: "l62", DayStartTime: new TimeOnly(8, 0)), f.Contacts.Me);
    }
}
