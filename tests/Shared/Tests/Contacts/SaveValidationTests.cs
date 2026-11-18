using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Save checks the contact first and writes nothing invalid.</summary>
[Unit]
public sealed class SaveValidationTests
{
    private readonly ContactsFixture f = new();

    public SaveValidationTests()
    {
        f.SeedDataTypes();
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana", ("alias", "anap")), ContactFiles.Person("2", "Bo"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("3", "Cy", ("alias", "cyc")));
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        f.Load();
        f.ForgetWrites();
    }

    private void AssertRejected(string group, Contact contact)
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        Assert.Throws<ArgumentException>(() => f.Contacts.Save(group, contact));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Rejects_a_contact_without_an_ID() => AssertRejected("team", new Contact("", "Ana"));

    [Fact]
    public void Rejects_a_contact_without_a_full_name() => AssertRejected("team", new Contact("1", ""));

    [Theory]
    [InlineData("a b")]
    [InlineData("a@b")]
    [InlineData("é")]
    [InlineData("a/b")]
    public void Rejects_an_ID_with_a_character_other_than_letters_digits_dash_underscore_and_dot(string id) =>
        AssertRejected("team", new Contact(id, "New"));

    [Theory]
    [InlineData("a b")]
    [InlineData("a@b")]
    [InlineData("é")]
    public void Rejects_an_alias_with_a_character_other_than_letters_digits_dash_underscore_and_dot(string alias) =>
        AssertRejected("team", new Contact("1", "Ana", Alias: alias));

    [Fact]
    public void Rejects_a_full_name_that_spans_more_than_one_line() => AssertRejected("team", new Contact("1", "Ana\nPopa"));

    [Fact]
    public void Rejects_a_first_name_that_spans_more_than_one_line() => AssertRejected("team", new Contact("1", "Ana", FirstName: "A\r\nna"));

    [Fact]
    public void Rejects_a_title_that_spans_more_than_one_line() => AssertRejected("team", new Contact("1", "Ana", Title: "Lead\nDev"));

    [Fact]
    public void Rejects_a_location_that_spans_more_than_one_line() => AssertRejected("team", new Contact("1", "Ana", Location: "Oslo\nNorway"));

    [Fact]
    public void Rejects_a_reportsTo_that_spans_more_than_one_line() => AssertRejected("team", new Contact("1", "Ana", ReportsTo: "a\nb"));

    [Fact]
    public void Rejects_a_new_ID_that_another_contact_holds_as_its_ID_in_another_group() => AssertRejected("team", new Contact("3", "Copy"));

    [Fact]
    public void Rejects_a_new_ID_that_another_contact_holds_as_its_alias() => AssertRejected("team", new Contact("cyc", "Copy"));

    [Fact]
    public void Rejects_an_alias_that_another_contact_holds_as_its_alias() => AssertRejected("team", new Contact("1", "Ana", Alias: "CYC"));

    [Fact]
    public void Rejects_an_alias_that_another_contact_holds_as_its_ID() => AssertRejected("team", new Contact("1", "Ana", Alias: "2"));

    [Fact]
    public void Rejects_a_pronouns_key_the_data_type_does_not_list() => AssertRejected("team", new Contact("1", "Ana", PronounsKey: "xe"));

    [Fact]
    public void Rejects_a_career_path_key_the_data_type_does_not_list() => AssertRejected("team", new Contact("1", "Ana", CareerPathKey: "ceo"));

    [Fact]
    public void Rejects_a_key_when_its_data_type_file_did_not_load()
    {
        f.Repository.Update(f.Clone, () => f.Seed(ContactsFixture.PronounsFile, "{ not json"));

        AssertRejected("team", new Contact("1", "Ana", PronounsKey: "he"));
    }

    [Fact]
    public void Rejects_a_key_when_its_data_type_file_is_missing()
    {
        f.Repository.Update(f.Clone, () => f.Disk.DeleteFile(f.PathOf(ContactsFixture.CareerPathsFile)));

        AssertRejected("team", new Contact("1", "Ana", CareerPathKey: "em"));
    }

    [Fact]
    public void Rejects_an_unknown_group() => AssertRejected("nobody", new Contact("1", "Ana"));

    [Fact]
    public void Rejects_a_new_contact_in_the_user_s_own_group() => AssertRejected("me", new Contact("77", "Other"));

    [Fact]
    public void Rejects_a_new_ID_for_the_user_s_own_contact() => AssertRejected("me", new Contact("43", "Me"));

    [Fact]
    public void Accepts_keys_the_data_types_list()
    {
        f.Contacts.Save("team", new Contact("1", "Ana", PronounsKey: "she", CareerPathKey: "se"));

        Assert.Equal("she", f.Member("team", "1").PronounsKey);
        Assert.Equal("se", f.Member("team", "1").CareerPathKey);
    }

    [Fact]
    public void Accepts_an_alias_equal_to_the_contact_s_own_ID_or_its_own_old_alias()
    {
        f.Contacts.Save("team", new Contact("1", "Ana", Alias: "ANAP"));
        f.Contacts.Save("team", new Contact("1", "Ana", Alias: "1"));

        Assert.Equal("1", f.Member("team", "1").Alias);
    }

    [Fact]
    public void Still_allows_a_name_the_contact_already_shared()
    {
        f.Seed($"{ContactsFixture.Folder}/dups.json", ContactFiles.Group(3000,
            ContactFiles.Person("8", "Dup One", ("alias", "dup")), ContactFiles.Person("9", "Dup Two", ("alias", "dup"))));
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("dups", new Contact("8", "Dup One Renamed", Alias: "dup"));

        Assert.Equal("Dup One Renamed", f.Member("dups", "8").FullName);
        Assert.Equal(["dup"], f.Contacts.SharedNames);
    }

    [Fact]
    public void Still_allows_a_key_the_contact_already_held_though_the_data_type_does_not_list_it()
    {
        f.Seed(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "xe"))));
        f.Load();
        f.ForgetWrites();

        f.Contacts.Save("team", new Contact("1", "Ana Popa", PronounsKey: "xe"));

        Assert.Equal("xe", f.Parsed(ContactsFixture.TeamFile).WithId("1")["pronounsKey"]);
    }

    [Fact]
    public void Still_rejects_changing_an_unlisted_key_to_another_unlisted_key()
    {
        f.Seed(ContactsFixture.TeamFile, ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", ("pronounsKey", "xe"))));
        f.Load();
        f.ForgetWrites();

        AssertRejected("team", new Contact("1", "Ana", PronounsKey: "ze"));
    }
}
