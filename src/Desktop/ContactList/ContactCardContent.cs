using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// What a contact card says: the contact's name and group, the fields to show, and the help line. Taken once, when the card opens, from
/// the details the Contact list shows; the fields are those details' fields, so a value reads as it does there. Ctrl switches
/// <see cref="AllDetails"/> from the common fields to every field, without reading the contact again.
/// </summary>
internal sealed class ContactCardContent
{
    // The fields the card shows without Ctrl, by file name; they show in the details' order.
    private static readonly HashSet<string> CommonLabels =
        [.. new[] { "alias", "title", "pronounsKey", "careerPathKey", "reportsTo" }.Select(FieldLabels.Of)];

    private readonly IReadOnlyList<DetailField> _every;
    private readonly IReadOnlyList<DetailField> _common;

    private ContactCardContent(ContactDetails details, bool allDetails)
    {
        Heading = details.Heading;
        GroupHeading = details.GroupHeading;
        var fullName = FieldLabels.Of("fullName");
        _every = [.. details.Fields.Where(field => field.Label != fullName)];
        _common = [.. _every.Where(field => CommonLabels.Contains(field.Label))];
        AllDetails = allDetails;
    }

    /// <summary>The contact's full name.</summary>
    public string Heading { get; }

    /// <summary>The heading of the contact's group.</summary>
    public string GroupHeading { get; }

    /// <summary>The fields to show now, in the details' order.</summary>
    public IReadOnlyList<DetailField> Fields => AllDetails ? _every : _common;

    /// <summary>The line that says what Ctrl does now.</summary>
    public string Help => AllDetails ? "Release Ctrl: fewer details" : "Ctrl: all details";

    /// <summary>Whether every field shows, as while Ctrl is held. The fields come from the contact as it was when the card opened.</summary>
    public bool AllDetails { get; set; }

    /// <summary>The content for the contact the address names, or null when <see cref="IContacts.ContactOf"/> finds nobody.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ContactCardContent? Of(IContacts contacts, string address, bool allDetails)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(address);
        if (contacts.ContactOf(address) is not { } contact
            || contacts.Groups.FirstOrDefault(group => group.Members.Contains(contact)) is not { } owner)
        {
            return null;
        }

        return new ContactCardContent(ContactDetailsBuilder.Of(contacts, owner, contact), allDetails);
    }
}
