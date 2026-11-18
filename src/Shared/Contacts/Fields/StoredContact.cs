using System.Text.Json;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A valid contact with the JSON fields it was read from, so a save keeps the fields Contacts doesn't know.</summary>
internal sealed record StoredContact(Contact Contact, ObjectFields Fields)
{
    /// <summary>
    /// Reads a contact object. Known fields come out in the app's order, empty ones left out,
    /// then unknown fields in file order. Throws <see cref="InvalidDataException"/> when the contact breaks a rule.
    /// </summary>
    public static StoredContact FromFields(ObjectFields raw)
    {
        var contact = new Contact("", "");
        foreach (var field in ContactFields.All)
        {
            if (raw.Get(field.Name) is not { } element)
                continue;
            if (element.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"'{field.Name}' must be text.");
            if (element.GetString() is { Length: > 0 } text)
                contact = field.Set(contact, text);
        }

        if (ContactRules.Problem(contact) is { } problem)
            throw new InvalidDataException(problem);

        return new StoredContact(contact, ObjectFields.Of(KnownFields(contact).Concat(UnknownFields(raw))));
    }

    /// <summary>A new contact with no unknown fields. Throws <see cref="InvalidDataException"/> when it breaks a rule.</summary>
    public static StoredContact Of(Contact contact) => FromFields(ObjectFields.Of(KnownFields(contact)));

    /// <summary>The edited contact with this one's unknown fields. Throws <see cref="InvalidDataException"/> when it breaks a rule.</summary>
    public StoredContact With(Contact edited) => FromFields(ObjectFields.Of(KnownFields(edited).Concat(UnknownFields(Fields))));

    private static IEnumerable<KeyValuePair<string, JsonElement>> KnownFields(Contact contact) =>
        ContactFields.All.Select(field => (field.Name, Value: field.Get(contact)))
            .Where(field => !string.IsNullOrEmpty(field.Value))
            .Select(field => KeyValuePair.Create(field.Name, JsonValues.Text(field.Value!)));

    private static IEnumerable<KeyValuePair<string, JsonElement>> UnknownFields(ObjectFields fields) =>
        fields.Pairs.Where(pair => ContactFields.Named(pair.Key) is null);
}
