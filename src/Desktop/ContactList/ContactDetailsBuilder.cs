using System.Globalization;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>Builds what the right side shows for a selected contact.</summary>
internal static class ContactDetailsBuilder
{
    public static ContactDetails Of(IContacts contacts, ContactGroup group, Contact contact)
    {
        List<DetailField> fields = [];
        foreach (var field in ContactFields.All.Where(f => group.IsTeam || !f.IsTeamField))
        {
                    if (field.TextOf(contact) is { Length: > 0 } stored)
            {
                        fields.Add(new DetailField(FieldLabels.Of(field.Name), [ValueOf(contacts, field, stored, contact)]));
            }

            // Direct reports follow Reports to, though a contact may have them without a manager of its own.
            if (field.Name == "reportsTo" && contacts.DirectReportsOf(contact) is { Count: > 0 } reports)
            {
                fields.Add(new DetailField("Direct reports", [.. reports.Select(report => new DetailValue(report.FullName, RefOf(contacts, report)))]));
            }
        }

        return new ContactDetails(contact.FullName, group.Heading, fields);
    }

    private static DetailValue ValueOf(IContacts contacts, ContactField field, string stored, Contact contact) => field.Kind switch
    {
        ContactFieldKind.Key => new DetailValue(KeyText(contacts, field.Name, stored), null),
        ContactFieldKind.Date => new DetailValue(
            DateOnly.Parse(stored, CultureInfo.InvariantCulture).ToString("d", CultureInfo.CurrentCulture), null),
        _ when field.Name == "reportsTo" => contacts.ManagerOf(contact) is { } manager
            ? new DetailValue(manager.FullName, RefOf(contacts, manager))
            : new DetailValue(stored, null),
        _ => new DetailValue(stored, null),
    };

    private static string KeyText(IContacts contacts, string keyField, string key) =>
        contacts.DataTypes.FirstOrDefault(type => type.KeyField == keyField)?.Entries
            .FirstOrDefault(entry => string.Equals(entry.Id, key, StringComparison.OrdinalIgnoreCase))?.Shown ?? key;

    private static ContactRef? RefOf(IContacts contacts, Contact contact) =>
        contacts.Groups.FirstOrDefault(group => group.Members.Contains(contact)) is { } owner ? new ContactRef(owner.Name, contact.Id) : null;
}
