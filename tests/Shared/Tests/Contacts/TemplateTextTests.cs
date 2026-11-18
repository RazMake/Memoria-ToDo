using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The text a template makes for one contact.</summary>
[Unit]
public sealed class TemplateTextTests
{
    private readonly ContactsFixture f = new();

    public TemplateTextTests()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Load();
    }

    private static Contact Ana { get; } = new("1", "Ana Pop", FirstName: "Ana", Title: "Lead", Alias: "apop");

    [Fact]
    public void Fills_a_field_in_the_template()
    {
        Assert.Equal("Ana (Lead) - apop", f.Contacts.TextOf(Ana, "{firstName} ({title}) - {alias}"));
    }

    [Fact]
    public void Fills_the_ID_and_the_full_name()
    {
        Assert.Equal("1:Ana Pop", f.Contacts.TextOf(Ana, "{id}:{fullName}"));
    }

    [Fact]
    public void Keeps_doubled_braces_as_literal_braces()
    {
        Assert.Equal("{Ana} }x{", f.Contacts.TextOf(Ana, "{{{firstName}}} }}x{{"));
    }

    [Fact]
    public void Reaches_the_properties_of_the_career_path_and_pronoun_entries()
    {
        var contact = Ana with { CareerPathKey = "em", PronounsKey = "he" };

        Assert.Equal("EM / he / him", f.Contacts.TextOf(contact, "{careerPath.short} / {pronouns.subject} / {pronouns.object}"));
    }

    [Fact]
    public void Shows_the_time_as_HH_mm_and_dates_as_yyyy_MM_dd()
    {
        var contact = Ana with
        {
            DayStartTime = new TimeOnly(9, 5),
            LevelStartDate = new DateOnly(2025, 3, 1),
            TeamMemberSince = new DateOnly(2020, 12, 31),
            ExitDate = new DateOnly(2026, 1, 2),
        };

        Assert.Equal("09:05 2025-03-01 2020-12-31 2026-01-02", f.Contacts.TextOf(contact, "{dayStartTime} {levelStartDate} {teamMemberSince} {exitDate}"));
    }

    [Theory]
    [InlineData("{title}")]
    [InlineData("{location} and {title}")]
    [InlineData("{dayStartTime}")]
    [InlineData("{careerPath.short}")]
    [InlineData("{pronouns.subject}")]
    public void Falls_back_to_the_full_name_when_a_value_is_missing(string template)
    {
        var contact = new Contact("2", "Bob Ion");

        Assert.Equal("Bob Ion", f.Contacts.TextOf(contact, template));
    }

    [Fact]
    public void Falls_back_to_the_full_name_when_the_key_is_not_listed()
    {
        Assert.Equal("Ana Pop", f.Contacts.TextOf(Ana with { CareerPathKey = "zz" }, "{careerPath.short}"));
    }

    [Fact]
    public void Falls_back_to_the_full_name_when_the_entry_lacks_the_property()
    {
        Assert.Equal("Ana Pop", f.Contacts.TextOf(Ana with { CareerPathKey = "em" }, "{careerPath.nothing}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{nope}")]
    [InlineData("{nope.short}")]
    [InlineData("{careerPath.}")]
    [InlineData("{firstName")]
    [InlineData("firstName}")]
    [InlineData("{{{firstName")]
    public void Rejects_an_invalid_template(string template)
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.TextOf(Ana, template));
        Assert.Throws<ArgumentException>(() => f.Contacts.MentionOf(Ana, template));
    }
}

/// <summary>The live text of a stored mention, found by its address.</summary>
[Unit]
public sealed class AddressTextTests
{
    private readonly ContactsFixture f = new();

    private void Load(params string[] members)
    {
        f.SeedGroup("team", 1000, members);
        f.SeedDataTypes();
        f.Load();
    }

    [Fact]
    public void Gives_the_live_text_of_the_single_contact_holding_the_ID()
    {
        Load(ContactFiles.Person("12", "Ana Pop", ("firstName", "Ana"), ("alias", "apop")));

        Assert.Equal("Ana", f.Contacts.TextOf(ContactAddress.Of("12"), "{firstName}"));
    }

    [Fact]
    public void Finds_the_contact_ignoring_case_of_the_ID()
    {
        Load(ContactFiles.Person("ab1", "Ana Pop", ("firstName", "Ana")));

        Assert.Equal("Ana", f.Contacts.TextOf(ContactAddress.Of("AB1"), "{firstName}"));
    }

    [Fact]
    public void Gives_null_for_an_address_that_is_not_a_contact()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Null(f.Contacts.TextOf("https://example.com/12", "{fullName}"));
        Assert.Null(f.Contacts.TextOf("task:12", "{fullName}"));
        Assert.Null(f.Contacts.TextOf("", "{fullName}"));
    }

    [Fact]
    public void Gives_null_for_an_unknown_ID()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("99"), "{fullName}"));
    }

    [Fact]
    public void Gives_null_for_an_ID_that_two_contacts_hold()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion", ("alias", "12")));

        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("12"), "{fullName}"));
    }

    [Fact]
    public void Gives_null_for_an_ID_that_is_only_an_alias()
    {
        Load(ContactFiles.Person("12", "Ana Pop", ("alias", "apop")));

        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("apop"), "{fullName}"));
    }

    [Fact]
    public void Gives_null_for_an_invalid_template()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("12"), "{nope}"));
    }

    [Fact]
    public void Gives_null_without_a_clone()
    {
        Assert.Null(f.Contacts.TextOf(ContactAddress.Of("12"), "{fullName}"));
    }
}

/// <summary>The Markdown link a new mention is made of.</summary>
[Unit]
public sealed class MentionTests
{
    private readonly ContactsFixture f = new();

    public MentionTests()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Load();
    }

    [Fact]
    public void Makes_a_link_with_the_text_the_contact_address_and_the_template()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop", FirstName: "Ana"), "{firstName}");

        Assert.Equal("[Ana](contact:12 \"{firstName}\")", link);
    }

    [Fact]
    public void Uses_the_ID_and_never_the_alias()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop", Alias: "apop"), "{fullName}");

        Assert.Equal("[Ana Pop](contact:12 \"{fullName}\")", link);
    }

    [Fact]
    public void Escapes_the_text_for_a_Markdown_label()
    {
        var link = f.Contacts.MentionOf(new Contact("12", @"Ana [the] \ Pop"), "{fullName}");

        Assert.StartsWith(@"[Ana \[the\] \\ Pop](contact:12 ", link);
    }

    [Fact]
    public void Escapes_quotes_and_backslashes_in_the_title_part()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop"), "\"{fullName}\" \\");

        Assert.Equal("[\"Ana Pop\" \\\\](contact:12 \"\\\"{fullName}\\\" \\\\\")", link);
    }
}
