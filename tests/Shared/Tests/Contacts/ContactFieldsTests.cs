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
                "id", "alias", "fullName", "firstName", "title", "location", "levelId", "workingType", "pronounsKey",
                "careerPathKey", "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate",
            ],
            ContactFields.All.Select(field => field.Name));
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
