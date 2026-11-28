namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Which contacts hold each name. IDs and aliases are one set of names, compared ignoring case.</summary>
internal sealed class NameIndex
{
    private readonly Dictionary<string, List<Contact>> holders = new(StringComparer.OrdinalIgnoreCase);

    public NameIndex(IEnumerable<Contact> contacts)
    {
        foreach (var contact in contacts)
        foreach (var name in NamesOf(contact))
        {
            if (!holders.TryGetValue(name, out var list))
                holders[name] = list = [];
            list.Add(contact);
        }
    }

    /// <summary>The contact's ID, then its alias when that's another name.</summary>
    public static IEnumerable<string> NamesOf(Contact contact)
    {
        yield return contact.Id;
        if (!string.IsNullOrEmpty(contact.Alias) && !contact.Alias.Equals(contact.Id, StringComparison.OrdinalIgnoreCase))
            yield return contact.Alias;
    }

    /// <summary>The one contact holding the name; null when none or several do.</summary>
    public Contact? Find(string name) =>
        holders.TryGetValue(name, out var list) && list.Count == 1 ? list[0] : null;

    /// <summary>Whether a contact other than this one (the same object) holds the name.</summary>
    public bool IsHeldByAnother(string name, Contact? contact) =>
        holders.TryGetValue(name, out var list) && list.Any(holder => !ReferenceEquals(holder, contact));

    /// <summary>The names two or more contacts hold, in alphabetical order.</summary>
    public IReadOnlyList<string> Shared() =>
        [.. holders.Where(pair => pair.Value.Count > 1).Select(pair => pair.Key).Order(StringComparer.OrdinalIgnoreCase)];
}
