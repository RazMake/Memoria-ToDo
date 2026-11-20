using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>How a saved group file is written: field order, sorted members, empty and unknown fields, and the platform's JSON style.</summary>
[Unit]
public sealed class SaveFileFormatTests
{
    private readonly ContactsFixture f = new();

    private void Load(string file, string json)
    {
        f.SeedDataTypes();
        f.Seed(file, json);
        f.Load();
        f.ForgetWrites();
    }

    [Fact]
    public void Writes_a_group_file_s_top_fields_as_version_position_members_then_unknown_fields_in_file_order()
    {
        Load(ContactsFixture.TeamFile,
            """{ "zzz": 1, "members": [ { "id": "1", "fullName": "Ana" } ], "position": 1000, "aaa": { "k": true }, "version": 1 }""");

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal(["version", "position", "members", "zzz", "aaa"], f.TopNames(ContactsFixture.TeamFile));
        Assert.StartsWith("{\n  \"version\": 1,\n  \"position\": 1000,", f.Text(ContactsFixture.TeamFile));
        Assert.Equal(1000, f.Parsed(ContactsFixture.TeamFile).Position);
    }

    [Fact]
    public void Writes_me_json_s_top_fields_as_version_position_details_then_unknown_fields()
    {
        Load(ContactsFixture.MeFile,
            """{ "extra": "x", "details": { "id": "42", "fullName": "Me" }, "position": 100, "version": 1 }""");

        f.Contacts.Save("me", new Contact("42", "Me Myself"));

        Assert.Equal(["version", "position", "details", "extra"], f.TopNames(ContactsFixture.MeFile));
        Assert.Equal("Me Myself", f.Parsed(ContactsFixture.MeFile).Objects.Single()["fullName"]);
    }

    [Fact]
    public void Writes_a_contact_s_known_fields_in_the_field_table_order()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));

        f.Contacts.Save("team", new Contact("1", "Ana Popa", "anap", "Ana", "Lead", "she", "em", "l62", "Remote", "Oslo", "boss",
            new TimeOnly(9, 30), new DateOnly(2025, 3, 1), new DateOnly(2020, 1, 2), new DateOnly(2026, 2, 3), "Nia"));

        Assert.Equal(
            ["id", "alias", "fullName", "firstName", "nickname", "title", "location", "levelId", "workingType", "pronounsKey", "careerPathKey", "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate"],
            f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Writes_the_nickname_right_after_the_first_name()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));

        f.Contacts.Save("team", new Contact("1", "Ana Popa", FirstName: "Ana", Title: "Lead", Nickname: "Nia"));

        Assert.Equal(["id", "fullName", "firstName", "nickname", "title"], f.FirstObjectNames(ContactsFixture.TeamFile));
        Assert.Equal("Nia", f.Parsed(ContactsFixture.TeamFile).WithId("1")["nickname"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Leaves_the_nickname_out_when_the_stored_value_is_missing_or_empty(string? nickname)
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", ("nickname", "Nia"))));

        f.Contacts.Save("team", new Contact("1", "Ana", Nickname: nickname));

        Assert.Equal(["id", "fullName"], f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Never_writes_the_first_name_as_the_nickname()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));

        f.Contacts.Save("team", new Contact("1", "Ana Popa", FirstName: "Ana"));

        Assert.Equal(["id", "fullName", "firstName"], f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Puts_a_hand_written_nickname_in_the_fixed_order_and_keeps_unknown_fields()
    {
        Load(ContactsFixture.TeamFile,
            """{ "version": 1, "position": 1000, "members": [ { "zeta": "z", "title": "Dev", "nickname": "Nia", "id": "1", "firstName": "Ana", "fullName": "Ana" } ] }""");

        f.Contacts.Save("team", new Contact("1", "Ana", FirstName: "Ana", Title: "Dev", Nickname: "Nia"));

        Assert.Equal(["id", "fullName", "firstName", "nickname", "title", "zeta"], f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Writes_unknown_fields_of_a_contact_after_the_known_ones_in_the_order_the_file_had_them()
    {
        Load(ContactsFixture.TeamFile,
            """{ "version": 1, "position": 1000, "members": [ { "zeta": "z", "title": "Dev", "alpha": 1, "fullName": "Ana", "id": "1", "mid": [ 1, 2 ] } ] }""");

        f.Contacts.Save("team", new Contact("1", "Ana", Title: "Lead", Location: "Oslo"));

        Assert.Equal(["id", "fullName", "title", "location", "zeta", "alpha", "mid"], f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Keeps_unknown_fields_and_their_values_as_they_are()
    {
        Load(ContactsFixture.TeamFile,
            """{ "version": 1, "position": 1000, "note": { "a": [ 1, 2 ] }, "members": [ { "id": "1", "fullName": "Ana", "score": 7, "tags": [ "x", "y" ], "more": { "a": null } }, { "id": "2", "fullName": "Bo", "other": "kept" } ] }""");

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        var written = f.Parsed(ContactsFixture.TeamFile);
        Assert.Equal("7", written.WithId("1")["score"]);
        Assert.Equal("[\"x\",\"y\"]", written.WithId("1")["tags"].Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        Assert.Equal("kept", written.WithId("2")["other"]);
        Assert.Contains("\"a\"", written.Top["note"]);
    }

    [Fact]
    public void Writes_the_members_sorted_by_full_name_then_ID_ignoring_case()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000,
            ContactFiles.Person("9", "zoe"), ContactFiles.Person("5", "bob"), ContactFiles.Person("3", "Ann")));

        f.Contacts.Save("team", new Contact("4", "Bob"));

        Assert.Equal(["3", "4", "5", "9"], f.Parsed(ContactsFixture.TeamFile).Ids);
    }

    [Fact]
    public void Leaves_empty_and_missing_fields_out()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", ("title", "Dev"))));

        f.Contacts.Save("team", new Contact("1", "Ana", FirstName: "", Title: null, Alias: ""));

        Assert.Equal(["id", "fullName"], f.FirstObjectNames(ContactsFixture.TeamFile));
    }

    [Fact]
    public void Writes_the_day_start_time_as_two_digit_hours_and_minutes_and_dates_as_year_month_day()
    {
        Load(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", ("dayStartTime", "9:30"))));

        f.Contacts.Save("team", new Contact("1", "Ana", DayStartTime: new TimeOnly(9, 5), LevelStartDate: new DateOnly(2025, 3, 1), TeamMemberSince: new DateOnly(2020, 1, 2), ExitDate: new DateOnly(2026, 12, 3)));

        var ana = f.Parsed(ContactsFixture.TeamFile).WithId("1");
        Assert.Equal("09:05", ana["dayStartTime"]);
        Assert.Equal("2025-03-01", ana["levelStartDate"]);
        Assert.Equal("2020-01-02", ana["teamMemberSince"]);
        Assert.Equal("2026-12-03", ana["exitDate"]);
    }

    [Fact]
    public void Writes_in_the_platform_s_JSON_style()
    {
        Load($"{ContactsFixture.Folder}/friends.json", """{"version":1,"position":2000,"members":[{"id":"1","fullName":"x"}]}""");

        f.Contacts.Save("friends", new Contact("1", "Ünï", Title: "Lead"));

        Assert.Equal(
            "{\n  \"version\": 1,\n  \"position\": 2000,\n  \"members\": [\n    {\n      \"id\": \"1\",\n      \"fullName\": \"Ünï\",\n      \"title\": \"Lead\"\n    }\n  ]\n}",
            f.Text($"{ContactsFixture.Folder}/friends.json"));
        Assert.False(f.Bytes($"{ContactsFixture.Folder}/friends.json").AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]));
    }

    [Theory]
    [InlineData("friends")]
    [InlineData("team")]
    [InlineData("me")]
    public void Saves_the_team_fields_in_any_group(string group)
    {
        f.SeedDataTypes();
        f.Seed($"{ContactsFixture.Folder}/{group}.json", group == "me"
            ? ContactFiles.Me(100, ContactFiles.Person("1", "Ana"))
            : ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));
        f.Load();

        f.Contacts.Save(group, new Contact("1", "Ana", Location: "Oslo", LevelId: "l62", WorkingType: "Remote",
            DayStartTime: new TimeOnly(8, 0), LevelStartDate: new DateOnly(2025, 3, 1), TeamMemberSince: new DateOnly(2020, 1, 2), ExitDate: new DateOnly(2026, 2, 3)));

        var saved = f.Parsed($"{ContactsFixture.Folder}/{group}.json").WithId("1");
        Assert.Equal("Oslo", saved["location"]);
        Assert.Equal("l62", saved["levelId"]);
        Assert.Equal("Remote", saved["workingType"]);
        Assert.Equal("08:00", saved["dayStartTime"]);
        Assert.Equal("2025-03-01", saved["levelStartDate"]);
        Assert.Equal("2020-01-02", saved["teamMemberSince"]);
        Assert.Equal("2026-02-03", saved["exitDate"]);
    }
}
