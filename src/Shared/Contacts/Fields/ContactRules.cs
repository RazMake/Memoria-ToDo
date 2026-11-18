namespace Memoria.ToDo.Shared.Contacts;

/// <summary>The rules every contact follows, whether it's read from a file or saved.</summary>
internal static class ContactRules
{
    /// <summary>The first rule the contact breaks, or null when it follows them all.</summary>
    public static string? Problem(Contact contact)
    {
        if (string.IsNullOrEmpty(contact.Id))
            return "A contact needs an 'id'.";
        if (string.IsNullOrEmpty(contact.FullName))
            return "A contact needs a 'fullName'.";
        foreach (var field in ContactFields.All)
            if (field.Get(contact) is { } value && value.AsSpan().ContainsAny('\r', '\n'))
                return $"'{field.Name}' must be one line.";
        if (!IsName(contact.Id))
            return $"The id '{contact.Id}' may hold only letters, digits, '-', '_', and '.'.";
        if (contact.Alias is { Length: > 0 } alias && !IsName(alias))
            return $"The alias '{alias}' may hold only letters, digits, '-', '_', and '.'.";
        return null;
    }

    private static bool IsName(string text) => text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
