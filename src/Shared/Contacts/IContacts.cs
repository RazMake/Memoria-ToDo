using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>The contacts in the selected clone. Safe to call from any thread; runs one call at a time.</summary>
public interface IContacts
{
    /// <summary>Whether the clone has at least one group file directly in autocomplete/contacts/, skipped or not. False with no clone.</summary>
    bool HasContactFiles { get; }

    /// <summary>Every loaded group, in display order; members by full name, then ID. Empty with no clone.</summary>
    IReadOnlyList<ContactGroup> Groups { get; }

    /// <summary>The user's own contact, or null when me.json is missing or skipped.</summary>
    Contact? Me { get; }

    /// <summary>Names two or more contacts hold, in alphabetical order.</summary>
    IReadOnlyList<string> SharedNames { get; }

    /// <summary>The templates, in file order, or the default list when the file is missing, empty, or skipped.</summary>
    IReadOnlyList<string> MentionTemplates { get; }

    /// <summary>Every key field's data type, in the order the form shows the fields, such as "careerPathKey".</summary>
    IReadOnlyList<DataType> DataTypes { get; }

    /// <summary>The one contact holding the alias, or else the ID; null when none or several do.</summary>
    Contact? Find(string name);

    /// <summary>The contact's manager, through reportsTo; null when it finds nobody.</summary>
    Contact? ManagerOf(Contact contact);

    /// <summary>The contacts whose reportsTo finds this contact, by full name.</summary>
    IReadOnlyList<Contact> DirectReportsOf(Contact contact);

    /// <summary>Contacts with every word of text in a known field, ignoring case, by full name, then ID.</summary>
    IReadOnlyList<Contact> Search(string text);

    /// <summary>
    /// The template's text for the contact, or its full name when it lacks a value the template names.
    /// Throws <see cref="ArgumentException"/> for an invalid template.
    /// </summary>
    string TextOf(Contact contact, string template);

    /// <summary>
    /// The live text for a stored mention; null when the address finds no single contact,
    /// or the template is invalid. The caller then shows the stored text.
    /// </summary>
    string? TextOf(string address, string template);

    /// <summary>
    /// The one contact a stored mention's address names, by ID only, ignoring case, as <see cref="TextOf(string, string)"/> finds it;
    /// null when the address isn't a contact address, or no contact or several hold the ID. The contact is the
    /// instance its group in <see cref="Groups"/> holds, so a caller finds its group there.
    /// Throws <see cref="ArgumentNullException"/> for a null address.
    /// </summary>
    Contact? ContactOf(string address);

    /// <summary>
    /// The whole Markdown link for a new mention: <c>[text](contact:&lt;id&gt; "&lt;template&gt;")</c>.
    /// Throws <see cref="ArgumentException"/> for an invalid template.
    /// </summary>
    string MentionOf(Contact contact, string template);

    /// <summary>
    /// Adds the contact to the group, or replaces the one with its ID, writes the group's file, then raises Changed.
    /// Throws, before writing: <see cref="NoRepositoryFolderException"/> with no clone; <see cref="ArgumentException"/> for an invalid
    /// contact, a name another contact holds, a key its data type doesn't list that the contact didn't hold,
    /// an unknown group, a new contact in "me", or a new ID for the user's own contact;
    /// <see cref="SkippedFileException"/> when the group's file was skipped.
    /// </summary>
    void Save(string groupName, Contact contact);

    /// <summary>
    /// Proposed, a decision to confirm: runs <paramref name="change"/> under the repository's lock with the group's contact holding
    /// the ID, ignoring case, or null when none does, then saves the contact it returns exactly as
    /// <see cref="Save(string, Contact)"/> does: the same checks and exceptions before writing, then Changed and the local-save report.
    /// So a caller that changes some fields keeps the latest value of the others, even when a sync or a window saves between.
    /// Throws as Save does for no clone, an unknown group, or a skipped group file, before calling change.
    /// An exception change throws reaches the caller, with nothing written; a null it returns is refused with <see cref="ArgumentException"/>. Because change runs under the lock,
    /// a Save, Delete, or MoveGroup inside it throws <see cref="InvalidOperationException"/>.
    /// </summary>
    void Save(string groupName, string id, Func<Contact?, Contact> change);

    /// <summary>
    /// Removes the contact from the group's file, then raises Changed. Throws, before writing:
    /// <see cref="NoRepositoryFolderException"/>; <see cref="ArgumentException"/> for "me", an unknown group, or an ID the group doesn't hold;
    /// <see cref="SkippedFileException"/> when the group's file was skipped.
    /// </summary>
    void Delete(string groupName, string id);

    /// <summary>
    /// Moves the group between two groups now next to each other, null at either end, rewriting positions
    /// as the design says, then raises Changed. Does nothing when the group is already there.
    /// Throws <see cref="SkippedFileException"/> first, for the moved group, then the previous, then the next, whose file was skipped;
    /// then <see cref="ArgumentException"/> when a group is unknown,
    /// or previous and next aren't neighbors once the group is taken out; <see cref="NoRepositoryFolderException"/>.
    /// A renumber that fails partway publishes the files it wrote, then throws the failure.
    /// </summary>
    void MoveGroup(string groupName, string? previousGroup, string? nextGroup);

    /// <summary>Every file skipped at the latest load, with its reason.</summary>
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// <summary>Raised after any change to what this interface returns, on the thread whose call made it, or after the repository's reload.</summary>
    event EventHandler? Changed;
}
