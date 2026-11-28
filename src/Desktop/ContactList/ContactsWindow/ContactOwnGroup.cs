namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>Which group is the user's own: the one in "me.json", whatever the case of the file's name, as Contacts reads it.</summary>
internal static class ContactOwnGroup
{
    public static bool Is(string groupName) => groupName.Equals("me", StringComparison.OrdinalIgnoreCase);
}
