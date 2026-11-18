using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Search: every typed word must appear, ignoring case, in some known field.</summary>
[Unit]
public sealed class SearchTests
{
    private readonly ContactsFixture f = new();

    private List<string> Found(string text) => ContactsFixture.Ids(f.Contacts.Search(text));

    private void Seed(params string[] members)
    {
        f.SeedGroup("team", 1000, members);
        f.Load();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void Matches_everyone_for_empty_or_blank_text(string text)
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo"));
        f.SeedMe(100, ContactFiles.Person("3", "Me"));
        f.Load();

        Assert.Equal(["1", "2", "3"], Found(text));
    }

    [Fact]
    public void Rejects_missing_text()
    {
        Seed(ContactFiles.Person("1", "Ana"));

        Assert.Throws<ArgumentNullException>(() => f.Contacts.Search(null!));
    }

    [Fact]
    public void Matches_a_word_inside_a_field_ignoring_case()
    {
        Seed(ContactFiles.Person("1", "Ana Popa"), ContactFiles.Person("2", "Bo Li"));

        Assert.Equal(["1"], Found("POP"));
        Assert.Equal(["2"], Found("bo li"));
    }

    [Fact]
    public void Needs_every_word_to_match()
    {
        Seed(ContactFiles.Person("1", "Ana Popa", ("title", "Lead")), ContactFiles.Person("2", "Ana Li", ("title", "Dev")));

        Assert.Equal(["1"], Found("ana lead"));
        Assert.Empty(Found("ana ghost"));
    }

    [Fact]
    public void Lets_the_words_match_in_different_fields()
    {
        Seed(ContactFiles.Person("1", "Ana", ("title", "Lead"), ("location", "Oslo")), ContactFiles.Person("2", "Bo", ("title", "Lead"), ("location", "Rome")));

        Assert.Equal(["1"], Found("lead oslo"));
        Assert.Equal(["2"], Found("rome lead"));
    }

    [Theory]
    [InlineData("alias", "zippy")]
    [InlineData("firstName", "Zippy")]
    [InlineData("title", "Zippy")]
    [InlineData("location", "Zippy")]
    [InlineData("levelId", "zippy")]
    [InlineData("workingType", "Zippy")]
    [InlineData("pronounsKey", "zippy")]
    [InlineData("careerPathKey", "zippy")]
    [InlineData("reportsTo", "zippy")]
    public void Searches_every_known_text_field(string field, string value)
    {
        Seed(ContactFiles.Person("1", "Ana", (field, value)), ContactFiles.Person("2", "Bo"));

        Assert.Equal(["1"], Found("zippy"));
    }

    [Fact]
    public void Searches_the_id_and_the_full_name()
    {
        Seed(ContactFiles.Person("77123", "Ana"), ContactFiles.Person("2", "Zoe"));

        Assert.Equal(["77123"], Found("7712"));
        Assert.Equal(["2"], Found("zoe"));
    }

    [Fact]
    public void Searches_a_time_as_hours_and_minutes_with_two_digits_even_when_the_file_holds_one()
    {
        Seed(ContactFiles.Person("1", "Ana", ("dayStartTime", "9:30")), ContactFiles.Person("2", "Bo", ("dayStartTime", "10:15")));

        Assert.Equal(["1"], Found("09:30"));
        Assert.Equal(["2"], Found("10:15"));
    }

    [Fact]
    public void Searches_a_time_written_with_two_digits_in_the_file()
    {
        Seed(ContactFiles.Person("1", "Ana", ("dayStartTime", "09:30")));

        Assert.Equal(["1"], Found("09:30"));
    }

    [Fact]
    public void Searches_each_date_as_year_month_day()
    {
        Seed(
            ContactFiles.Person("1", "Ana", ("levelStartDate", "2025-03-01")),
            ContactFiles.Person("2", "Bo", ("teamMemberSince", "2019-07-15")),
            ContactFiles.Person("3", "Cy", ("exitDate", "2024-12-31")));

        Assert.Equal(["1"], Found("2025-03-01"));
        Assert.Equal(["2"], Found("2019-07-15"));
        Assert.Equal(["3"], Found("2024-12-31"));
    }

    [Fact]
    public void Searches_the_other_team_fields_in_a_group_that_is_not_a_team()
    {
        f.SeedGroup("friends", 2000, ContactFiles.Person("1", "Ana", ("location", "Oslo"), ("levelId", "l62"), ("workingType", "Remote")));
        f.Load();

        Assert.Equal(["1"], Found("oslo"));
        Assert.Equal(["1"], Found("l62"));
        Assert.Equal(["1"], Found("remote"));
    }

    [Fact]
    public void Does_not_search_the_group_name()
    {
        f.SeedGroup("friends", 2000, ContactFiles.Person("1", "Ana"));
        f.Load();

        Assert.Empty(Found("friends"));
    }

    [Fact]
    public void Does_not_search_unknown_fields()
    {
        Seed("""{ "id": "1", "fullName": "Ana", "nickname": "Zippy", "notes": [ "zippy" ] }""");

        Assert.Empty(Found("zippy"));
    }

    [Fact]
    public void Sorts_matches_by_full_name_then_ID_ignoring_case()
    {
        Seed(
            ContactFiles.Person("5", "bob"),
            ContactFiles.Person("4", "Bob"),
            ContactFiles.Person("2", "Cy"),
            ContactFiles.Person("3", "Ann"),
            ContactFiles.Person("1", "ann"));

        Assert.Equal(["1", "3", "4", "5", "2"], Found(""));
    }

    [Fact]
    public void Searches_contacts_of_every_group()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("title", "Lead")));
        f.SeedGroup("friends", 2000, ContactFiles.Person("2", "Bo", ("title", "Lead")));
        f.Load();

        Assert.Equal(["1", "2"], Found("lead"));
    }
}
