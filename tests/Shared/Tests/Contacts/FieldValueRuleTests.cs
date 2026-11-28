using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>What a contact's known field values may hold: one line of text, a valid name, date, or time.</summary>
[Unit]
public sealed class FieldValueRuleTests
{
    private static readonly string[] TextFields =
        ["id", "alias", "fullName", "firstName", "nickname", "title", "location", "levelId", "workingType", "pronounsKey", "careerPathKey", "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate"];

    private static readonly string[] FreeTextFields =
        ["id", "alias", "fullName", "firstName", "nickname", "title", "location", "levelId", "workingType", "pronounsKey", "careerPathKey", "reportsTo"];

    private static readonly string[] DateFields = ["levelStartDate", "teamMemberSince", "exitDate"];

    private readonly ContactsFixture f = new();

    public static TheoryData<string, string> NonTextValues
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var name in TextFields)
                foreach (var value in new[] { "5", "true", "[]", "{}" })
                    data.Add(name, value);
            return data;
        }
    }

    public static TheoryData<string, string> BadDates
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var name in DateFields)
                foreach (var value in new[] { "2025-3-1", "2025/03/01", "03-01-2025", "2025-02-30", "2025-03-01T00:00:00", " 2025-03-01", "20250301", "soon" })
                    data.Add(name, value);
            return data;
        }
    }

    public static TheoryData<string> MultiLineFields
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in FreeTextFields)
                data.Add(name);
            return data;
        }
    }

    private void SeedWithRaw(string field, string rawValue)
    {
        var id = field == "id" ? rawValue : "\"1\"";
        var fullName = field == "fullName" ? rawValue : "\"Ana\"";
        var extra = field is "id" or "fullName" ? "" : $", \"{field}\": {rawValue}";
        f.Seed(ContactsFixture.TeamFile, $$"""{ "version": 1, "position": 1000, "members": [ { "id": {{id}}, "fullName": {{fullName}}{{extra}} } ] }""");
    }

    [Theory]
    [MemberData(nameof(NonTextValues))]
    public void Skips_a_file_whose_known_field_is_not_text(string field, string rawValue)
    {
        SeedWithRaw(field, rawValue);

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        Assert.Empty(f.Contacts.Groups);
    }

    [Theory]
    [MemberData(nameof(MultiLineFields))]
    public void Skips_a_file_whose_text_field_spans_more_than_one_line(string field)
    {
        SeedWithRaw(field, "\"first\\nsecond\"");

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a@b")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("é")]
    [InlineData("a,b")]
    public void Skips_a_file_whose_id_or_alias_has_a_character_other_than_letters_digits_dash_underscore_and_dot(string name)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person(name, "Ana"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo", ("alias", name)));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
        f.AssertSkipped($"{ContactsFixture.Folder}/friends.json");
        Assert.Empty(f.Contacts.Groups);
    }

    [Fact]
    public void Loads_an_id_and_an_alias_made_of_letters_digits_dash_underscore_and_dot()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("Ab-9_x.Y", "Ana", ("alias", "Z.z-1_")));

        f.Load();

        Assert.Equal([new Contact("Ab-9_x.Y", "Ana", Alias: "Z.z-1_")], f.Group("team").Members);
    }

    [Theory]
    [MemberData(nameof(BadDates))]
    public void Skips_a_file_whose_date_is_not_year_month_day_with_dashes(string field, string value)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", (field, value)));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }

    [Fact]
    public void Reads_each_date_field_as_year_month_day()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana",
            ("levelStartDate", "2025-03-01"), ("teamMemberSince", "2020-12-31"), ("exitDate", "2026-01-15")));

        f.Load();

        var ana = Assert.Single(f.Group("team").Members);
        Assert.Equal(new DateOnly(2025, 3, 1), ana.LevelStartDate);
        Assert.Equal(new DateOnly(2020, 12, 31), ana.TeamMemberSince);
        Assert.Equal(new DateOnly(2026, 1, 15), ana.ExitDate);
    }

    [Theory]
    [InlineData("9:30", 9, 30)]
    [InlineData("09:30", 9, 30)]
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    public void Reads_a_day_start_time_with_one_or_two_digit_hour(string text, int hour, int minute)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("dayStartTime", text)));

        f.Load();

        Assert.Equal(new TimeOnly(hour, minute), Assert.Single(f.Group("team").Members).DayStartTime);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("9:5")]
    [InlineData(" 9:30")]
    [InlineData("9:30 ")]
    [InlineData("12:60")]
    [InlineData("9.30")]
    [InlineData("noon")]
    public void Skips_a_file_whose_day_start_time_is_not_a_time(string text)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("dayStartTime", text)));

        f.Load();

        f.AssertSkipped(ContactsFixture.TeamFile);
    }
}
