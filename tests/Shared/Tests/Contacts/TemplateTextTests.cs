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
    public void Fills_the_nickname_in_the_template()
    {
        Assert.Equal("Nia", f.Contacts.TextOf(Ana with { Nickname = "Nia" }, "{nickname}"));
    }

    [Fact]
    public void Shows_the_first_name_for_a_missing_or_empty_nickname()
    {
        Assert.Equal("Ana", f.Contacts.TextOf(Ana, "{nickname}"));
        Assert.Equal("Ana", f.Contacts.TextOf(Ana with { Nickname = "" }, "{nickname}"));
    }

    [Fact]
    public void Falls_back_to_the_full_name_when_the_nickname_and_the_first_name_are_missing()
    {
        Assert.Equal("Bob Ion", f.Contacts.TextOf(new Contact("2", "Bob Ion"), "{nickname}"));
        Assert.Equal("Bob Ion", f.Contacts.TextOf(new Contact("2", "Bob Ion", Nickname: ""), "{nickname}"));
    }

    [Fact]
    public void Falls_back_to_the_full_name_when_a_template_with_the_nickname_lacks_another_value()
    {
        Assert.Equal("Ana Pop", f.Contacts.TextOf(Ana with { Title = null }, "{nickname} ({title})"));
    }

    [Fact]
    public void Shows_only_the_first_name_for_the_first_name_placeholder_when_the_contact_has_a_nickname()
    {
        Assert.Equal("Ana", f.Contacts.TextOf(Ana with { Nickname = "Nia" }, "{firstName}"));
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

        Assert.Equal(@"09\:05 2025\-03\-01 2020\-12\-31 2026\-01\-02", f.Contacts.TextOf(contact, "{dayStartTime} {levelStartDate} {teamMemberSince} {exitDate}"));
    }

    [Fact]
    public void Keeps_the_Markdown_of_the_templates_own_text_as_written()
    {
        var contact = Ana with { Alias = "anap" };

        Assert.Equal("Ana Pop (_anap_)", f.Contacts.TextOf(contact, "{fullName} (_{alias}_)"));
        Assert.Equal("**Ana**", f.Contacts.TextOf(contact, "**{nickname}**"));
        Assert.Equal(@"~~x~~ `y` :red[anap] \* \[Ana\]", f.Contacts.TextOf(contact, @"~~x~~ `y` :red[{alias}] \* \[{firstName}\]"));
    }

    [Fact]
    public void Puts_a_backslash_before_each_punctuation_character_of_a_field_value()
    {
        Assert.Equal(@"ana\_pop", f.Contacts.TextOf(Ana with { Alias = "ana_pop" }, "{alias}"));
        Assert.Equal(@"Ana \*QA\*", f.Contacts.TextOf(Ana with { FullName = "Ana *QA*" }, "{fullName}"));
        Assert.Equal(@"Ana Pop (_ana\_pop_)", f.Contacts.TextOf(Ana with { Alias = "ana_pop" }, "{fullName} (_{alias}_)"));
    }

    [Fact]
    public void Escapes_every_ASCII_punctuation_and_symbol_character_in_a_value()
    {
        const string punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
        const string escaped = @"\!\""\#\$\%\&\'\(\)\*\+\,\-\.\/\:\;\<\=\>\?\@\[\\\]\^\_\`\{\|\}\~";

        Assert.Equal(escaped, f.Contacts.TextOf(Ana with { Title = punctuation }, "{title}"));
    }

    [Fact]
    public void Leaves_letters_digits_spaces_and_non_ASCII_characters_in_a_value_unescaped()
    {
        Assert.Equal("Ana Pop 42 ă é – ñ", f.Contacts.TextOf(Ana with { Title = "Ana Pop 42 ă é – ñ" }, "{title}"));
    }

    [Fact]
    public void Does_not_read_braces_or_placeholders_inside_a_value_as_template_text()
    {
        Assert.Equal(@"\{title\} x", f.Contacts.TextOf(Ana with { Alias = "{title}" }, "{alias} x"));
    }

    [Fact]
    public void Escapes_the_full_name_it_falls_back_to()
    {
        var contact = new Contact("2", "Ana *QA*");

        Assert.Equal(@"Ana \*QA\*", f.Contacts.TextOf(contact, "{title}"));
        Assert.Equal(@"Ana \*QA\*", f.Contacts.TextOf(contact, "{nickname}"));
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
    [InlineData("[{alias}")]
    [InlineData("{alias}]")]
    [InlineData("][{alias}")]
    [InlineData("[x]({alias})")]
    [InlineData("![{alias}]")]
    [InlineData("{alias}\\")]
    [InlineData("{alias}\\\\\\")]
    [InlineData(@"\{alias}")]
    [InlineData(@"a \{fullName} b")]
    [InlineData(@"\\\{alias}")]
    [InlineData("{alias}\n{title}")]
    [InlineData("{alias}\r{title}")]
    public void Rejects_an_invalid_template(string template)
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.TextOf(Ana, template));
        Assert.Throws<ArgumentException>(() => f.Contacts.MentionOf(Ana, template));
    }

    [Theory]
    [InlineData("[{alias}]")]
    [InlineData(@"\[{alias}\]")]
    [InlineData(@"\[{alias}")]
    [InlineData(@"{alias}\\")]
    [InlineData(@"[\]{alias}]")]
    [InlineData("[{alias}] [{title}]")]
    [InlineData("!{alias}")]
    public void Accepts_a_template_whose_brackets_pair_and_that_makes_no_link_image_or_trailing_backslash(string template)
    {
        Assert.NotEmpty(f.Contacts.TextOf(Ana, template));
        Assert.StartsWith("[", f.Contacts.MentionOf(Ana, template));
    }

    [Theory]
    [InlineData(@"\\{alias}")]
    [InlineData(@"\{{alias}}")]
    public void Accepts_an_escaped_backslash_or_a_literal_brace_right_after_a_backslash(string template)
    {
        Assert.NotEmpty(f.Contacts.TextOf(Ana, template));
        Assert.StartsWith("[", f.Contacts.MentionOf(Ana, template));
    }

    [Fact]
    public void Gives_null_for_a_stored_address_whose_template_breaks_a_new_rule()
    {
        var loaded = new ContactsFixture();
        loaded.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop", ("alias", "apop")));
        loaded.SeedDataTypes();
        loaded.Load();

        Assert.Null(loaded.Contacts.TextOf(ContactAddress.Of("12"), "[{alias}"));
        Assert.Null(loaded.Contacts.TextOf(ContactAddress.Of("12"), "{alias}\\"));
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
    public void Gives_the_live_nickname_of_the_contact_holding_the_ID_and_the_first_name_when_it_has_none()
    {
        Load(ContactFiles.Person("12", "Ana Pop", ("firstName", "Ana"), ("nickname", "Nia")), ContactFiles.Person("13", "Bob Ion", ("firstName", "Bob")));

        Assert.Equal("Nia", f.Contacts.TextOf(ContactAddress.Of("12"), "{nickname}"));
        Assert.Equal("Bob", f.Contacts.TextOf(ContactAddress.Of("13"), "{nickname}"));
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
    public void Makes_a_link_with_the_nickname_as_its_text_and_the_first_name_or_full_name_as_fallbacks()
    {
        Assert.Equal("[Nia](contact:12 \"{nickname}\")", f.Contacts.MentionOf(new Contact("12", "Ana Pop", FirstName: "Ana", Nickname: "Nia"), "{nickname}"));
        Assert.Equal("[Ana](contact:12 \"{nickname}\")", f.Contacts.MentionOf(new Contact("12", "Ana Pop", FirstName: "Ana"), "{nickname}"));
        Assert.Equal("[Ana Pop](contact:12 \"{nickname}\")", f.Contacts.MentionOf(new Contact("12", "Ana Pop"), "{nickname}"));
    }

    [Fact]
    public void Uses_the_ID_and_never_the_alias()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop", Alias: "apop"), "{fullName}");

        Assert.Equal("[Ana Pop](contact:12 \"{fullName}\")", link);
    }

    [Fact]
    public void Stores_the_text_as_the_Markdown_the_template_makes_with_one_backslash_per_escape()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop", Alias: "ana_pop"), "{alias}");

        Assert.Equal(@"[ana\_pop](contact:12 ""{alias}"")", link);
    }

    [Fact]
    public void Stores_a_full_name_with_brackets_and_a_backslash_escaped_once()
    {
        var link = f.Contacts.MentionOf(new Contact("12", @"Ana [the] \ Pop"), "{fullName}");

        Assert.StartsWith(@"[Ana \[the\] \\ Pop](contact:12 ", link);
    }

    [Fact]
    public void Stores_the_templates_own_Markdown_and_the_escaped_values_in_the_text()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana *QA*", Alias: "ana_pop"), "{fullName} (_{alias}_)");

        Assert.Equal(@"[Ana \*QA\* (_ana\_pop_)](contact:12 ""{fullName} (_{alias}_)"")", link);
    }

    [Fact]
    public void Stores_the_escaped_full_name_when_the_template_falls_back()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana *QA*"), "{title}");

        Assert.Equal(@"[Ana \*QA\*](contact:12 ""{title}"")", link);
    }

    [Fact]
    public void Escapes_quotes_and_backslashes_in_the_title_part()
    {
        var link = f.Contacts.MentionOf(new Contact("12", "Ana Pop"), "\"{fullName}\" \\\\");

        Assert.Equal(@"[""Ana Pop"" \\](contact:12 ""\""{fullName}\"" \\\\"")", link);
    }
}

/// <summary>A key field's entry property shows in a mention as Markdown.</summary>
[Unit]
public sealed class KeyFieldTextTests
{
    [Fact]
    public void Puts_a_backslash_before_each_punctuation_character_of_an_entry_property()
    {
        var f = new ContactsFixture();
        f.Seed(ContactsFixture.CareerPathsFile, ContactFiles.DataType("careerPaths", ContactFiles.Object(("id", "em"), ("short", "E.M. *lead*"))));
        f.SeedGroup("team", 1000);
        f.Load();
        var contact = new Contact("1", "Ana Pop", CareerPathKey: "em");

        Assert.Equal(@"E\.M\. \*lead\*", f.Contacts.TextOf(contact, "{careerPath.short}"));
        Assert.Equal(@"[Ana Pop (E\.M\. \*lead\*)](contact:1 ""{fullName} ({careerPath.short})"")", f.Contacts.MentionOf(contact, "{fullName} ({careerPath.short})"));
    }
}
