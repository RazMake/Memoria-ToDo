namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Finds contacts by the words typed. The contacts must already be in the order results come in.</summary>
internal sealed class ContactSearch
{
    private readonly (Contact Contact, string[] Fields)[] entries;

    public ContactSearch(IEnumerable<Contact> sorted) =>
        entries = [.. sorted.Select(contact => (contact,
            ContactFields.All.Select(field => field.TextOf(contact)).OfType<string>().Where(value => value.Length > 0).ToArray()))];

    /// <summary>The contacts with every word of the text in some field, ignoring case; everyone for blank text.</summary>
    public IReadOnlyList<Contact> Find(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return [.. entries
            .Where(entry => words.All(word => entry.Fields.Any(field => field.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .Select(entry => entry.Contact)];
    }
}
