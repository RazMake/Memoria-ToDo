using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The messages for what a Contacts call can throw: the platform's, and the one for a group file skipped since the list was read.</summary>
internal static class ContactFailures
{
    public const string SkippedGroup = "This group's file was skipped. See the problems line.";

    /// <summary>
    /// The message for the failure, or null for one the window doesn't report. An <see cref="ArgumentException"/> or
    /// <see cref="InvalidOperationException"/> from a call that names a group the loaded groups no longer hold
    /// (its file was skipped) gets the skipped-group message; an <see cref="InvalidOperationException"/> otherwise isn't reported.
    /// </summary>
    public static string? MessageOf(Exception failure, IContacts contacts, IReadOnlyList<string> groups)
    {
        if (failure is NoRepositoryFolderException)
        {
            return StoreFailures.MessageOf(failure);
        }

        if (failure is ArgumentException or InvalidOperationException && groups.Any(group => contacts.Groups.All(loaded => loaded.Name != group)))
        {
            return SkippedGroup;
        }

        return StoreFailures.MessageOf(failure);
    }
}
