using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The contact a stored mention's address names, found by ID only.</summary>
[Unit]
public sealed class ContactOfTests
{
    private readonly ContactsFixture f = new();

    private void Load(params string[] members)
    {
        f.SeedGroup("team", 1000, members);
        f.Load();
    }

    [Fact]
    public void Gives_the_single_contact_holding_the_ID()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion"));

        Assert.Equal("Ana Pop", f.Contacts.ContactOf(ContactAddress.Of("12"))?.FullName);
    }

    [Fact]
    public void Gives_the_same_instance_its_group_holds()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Same(f.Member("team", "12"), f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Fact]
    public void Finds_a_contact_in_any_group()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("20", "Cat Dan"));
        f.Load();

        Assert.Same(f.Member("friends", "20"), f.Contacts.ContactOf(ContactAddress.Of("20")));
    }

    [Fact]
    public void Finds_the_contact_ignoring_case_of_the_ID()
    {
        Load(ContactFiles.Person("ab1", "Ana Pop"));

        Assert.Same(f.Member("team", "ab1"), f.Contacts.ContactOf(ContactAddress.Of("AB1")));
    }

    [Fact]
    public void Gives_null_for_an_unknown_ID()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("99")));
    }

    [Fact]
    public void Gives_null_for_an_ID_that_is_only_an_alias()
    {
        Load(ContactFiles.Person("12", "Ana Pop", ("alias", "apop")));

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("apop")));
    }

    [Fact]
    public void Gives_null_for_an_ID_another_contacts_alias_shares()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion", ("alias", "12")));

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Fact]
    public void Gives_null_for_an_ID_two_groups_hold()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("12", "Cat Dan"));
        f.Load();

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("contact:")]
    [InlineData("task:12")]
    [InlineData("https://example.com/12")]
    [InlineData("CONTACT:12")]
    [InlineData(" contact:12")]
    public void Gives_null_for_an_address_that_is_not_a_contact_address(string address)
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Null(f.Contacts.ContactOf(address));
    }

    [Fact]
    public void Rejects_a_null_address()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        Assert.Throws<ArgumentNullException>(() => f.Contacts.ContactOf(null!));
    }

    [Fact]
    public void Gives_null_without_a_clone()
    {
        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Fact]
    public void Finds_a_contact_a_reload_adds()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));
        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("13")));

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion")));

        Assert.Same(f.Member("team", "13"), f.Contacts.ContactOf(ContactAddress.Of("13")));
    }

    [Fact]
    public void Gives_null_for_a_contact_a_reload_removes()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion"));

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop")));

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("13")));
    }

    [Fact]
    public void Gives_the_contact_as_it_is_after_a_reload()
    {
        Load(ContactFiles.Person("12", "Ana Pop"));

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Popescu")));

        Assert.Equal("Ana Popescu", f.Contacts.ContactOf(ContactAddress.Of("12"))?.FullName);
    }

    [Fact]
    public void Gives_null_once_a_reload_makes_the_ID_shared()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion"));

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion", ("alias", "12"))));

        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Fact]
    public void Finds_the_contact_again_once_a_reload_ends_the_sharing()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion", ("alias", "12")));
        Assert.Null(f.Contacts.ContactOf(ContactAddress.Of("12")));

        f.Repository.Update(f.Clone, () => f.SeedGroup("team", 1000, ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion")));

        Assert.Same(f.Member("team", "12"), f.Contacts.ContactOf(ContactAddress.Of("12")));
    }

    [Fact]
    public void Agrees_with_the_live_text_of_the_same_address()
    {
        Load(ContactFiles.Person("12", "Ana Pop"), ContactFiles.Person("13", "Bob Ion", ("alias", "12")), ContactFiles.Person("14", "Cy Lee"));
        foreach (var address in new[] { "contact:12", "contact:14", "contact:CY", "contact:99", "task:14" })
        {
            Assert.Equal(f.Contacts.TextOf(address, "{fullName}") is not null, f.Contacts.ContactOf(address) is not null);
        }
    }
}
