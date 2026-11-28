using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A row of the "@" list: the contact's full name and its alias, or its ID when it has none, so every row names something the user can type.</summary>
internal sealed record MentionRow(string FullName, string Alias)
{
    /// <summary>The row as plain text: "Full Name (alias)".</summary>
    public string Text => $"{FullName} ({Alias})";

    public static MentionRow Of(Contact contact) =>
        new(contact.FullName, contact.Alias is { Length: > 0 } alias ? alias : contact.Id);
}
