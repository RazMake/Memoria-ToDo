using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Contacts' public field table: the known fields in the order the app writes them, their kinds, and reading and setting a field as text.</summary>
[Unit]
public sealed class ContactFieldsTests
{
    private static readonly Contact Empty = new("ana", "Ana Pop");

    private static ContactField Field(string name) => ContactFields.All.Single(field => field.Name == name);

    [Fact]
    public void Lists_the_fields_in_the_order_of_the_field_table()
    {
        Assert.Equal(
            [
                "id", "alias", "fullName", "firstName", "nickname", "title", "location", "levelId", "workingType", "pronounsKey",
                "careerPathKey", "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate",
            ],
            ContactFields.All.Select(field => field.Name));
    }

    [Fact]
    public void Gives_the_nickname_field_the_text_kind_and_no_team_field_mark()
    {
        Assert.Equal(ContactFieldKind.Text, Field("nickname").Kind);
        Assert.False(Field("nickname").IsTeamField);
    }

    [Fact]
    public void Reads_the_stored_nickname_and_not_the_first_name_as_text()
    {
        var field = Field("nickname");

        Assert.Null(field.TextOf(new Contact("ana", "Ana Pop", FirstName: "Ana")));
        Assert.Equal("Nia", field.TextOf(new Contact("ana", "Ana Pop", FirstName: "Ana", Nickname: "Nia")));
    }

    [Fact]
    public void Sets_and_clears_the_stored_nickname_from_text()
    {
        var field = Field("nickname");

        var set = field.With(new Contact("ana", "Ana Pop", FirstName: "Ana"), "Nia");
        Assert.Equal("Nia", set.Nickname);
        Assert.Equal("Nia", field.TextOf(set));

        Assert.Null(field.TextOf(field.With(set, null)));
        Assert.Null(field.TextOf(field.With(set, "")));
        Assert.Equal("Ana", field.With(set, "").Nickname);
    }

    [Fact]
    public void Marks_the_fields_of_the_Team_column_as_team_fields()
    {
        Assert.Equal(
            ["location", "levelId", "workingType", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate"],
            ContactFields.All.Where(field => field.IsTeamField).Select(field => field.Name));
    }

    [Fact]
    public void Marks_the_pronouns_and_career_path_fields_as_key_fields()
    {
        Assert.Equal(["pronounsKey", "careerPathKey"], ContactFields.All.Where(field => field.Kind == ContactFieldKind.Key).Select(field => field.Name));
    }

    [Fact]
    public void Gives_the_time_field_the_time_kind_and_the_date_fields_the_date_kind()
    {
        Assert.Equal(ContactFieldKind.Time, Field("dayStartTime").Kind);
        Assert.All(["levelStartDate", "teamMemberSince", "exitDate"], name => Assert.Equal(ContactFieldKind.Date, Field(name).Kind));
        Assert.Equal(ContactFieldKind.Text, Field("title").Kind);
    }

    [Fact]
    public void Writes_and_reads_a_date_as_yyyy_MM_dd()
    {
        var contact = Field("exitDate").With(Empty, "2025-01-31");

        Assert.Equal(new DateOnly(2025, 1, 31), contact.ExitDate);
        Assert.Equal("2025-01-31", Field("exitDate").TextOf(contact));
    }

    [Fact]
    public void Writes_and_reads_a_time_as_HH_mm()
    {
        var contact = Field("dayStartTime").With(Empty, "9:30");

        Assert.Equal(new TimeOnly(9, 30), contact.DayStartTime);
        Assert.Equal("09:30", Field("dayStartTime").TextOf(contact));
    }

    [Fact]
    public void A_missing_field_reads_as_no_text()
    {
        Assert.All(ContactFields.All.Where(field => field.Name is not ("id" or "fullName")), field => Assert.Null(field.TextOf(Empty)));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("dayStartTime")]
    [InlineData("exitDate")]
    public void Setting_a_field_from_null_or_empty_text_makes_it_missing(string name)
    {
        var field = Field(name);
        var filled = field.With(Empty, name switch { "dayStartTime" => "09:30", "exitDate" => "2025-01-31", _ => "Lead" });

        Assert.Null(field.TextOf(field.With(filled, null)));
        Assert.Null(field.TextOf(field.With(filled, "")));
    }

    [Theory]
    [InlineData("31/01/2025")]
    [InlineData("2025-1-31")]
    [InlineData("2025-02-30")]
    [InlineData("soon")]
    public void Rejects_a_date_not_written_yyyy_MM_dd(string text)
    {
        Assert.Throws<FormatException>(() => Field("levelStartDate").With(Empty, text));
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("9:5")]
    [InlineData("nine")]
    public void Rejects_a_time_not_written_as_a_time(string text)
    {
        Assert.Throws<FormatException>(() => Field("dayStartTime").With(Empty, text));
    }
}
