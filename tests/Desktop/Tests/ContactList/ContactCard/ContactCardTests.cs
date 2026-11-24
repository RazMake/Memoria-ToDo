using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

using Card = Memoria.ToDo.Desktop.ContactList.ContactCard;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactCard;

/// <summary>When the published <see cref="Card.For"/> gives no card, and what it rejects. No element is made in these.</summary>
[Unit]
public class ContactCardTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    [Fact]
    public void Gives_no_card_for_an_ID_no_contact_holds()
    {
        Assert.Null(Card.For(_services.Contacts, new FakeTheme(), ContactAddress.Of("ghost")));
    }

    [Fact]
    public void Gives_no_card_for_an_ID_two_contacts_hold()
    {
        var services = ContactServices.WithASharedID();

        Assert.Null(Card.For(services.Contacts, new FakeTheme(), ContactAddress.Of("sam")));
    }

    [Fact]
    public void Gives_no_card_for_an_address_that_is_not_a_contact_address()
    {
        Assert.Null(Card.For(_services.Contacts, new FakeTheme(), "task:ana"));
    }

    [Fact]
    public void Rejects_null_contacts()
    {
        Assert.Throws<ArgumentNullException>(() => Card.For(null!, new FakeTheme(), ContactAddress.Of("ana")));
    }

    [Fact]
    public void Rejects_a_null_theme()
    {
        Assert.Throws<ArgumentNullException>(() => Card.For(_services.Contacts, null!, ContactAddress.Of("ana")));
    }

    [Fact]
    public void Rejects_a_null_address()
    {
        Assert.Throws<ArgumentNullException>(() => Card.For(_services.Contacts, new FakeTheme(), null!));
    }
}
