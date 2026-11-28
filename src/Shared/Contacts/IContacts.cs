using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>
/// The contacts in the selected clone. Safe to call from any thread; runs one call at a time.
/// Save, Delete, MoveGroup, and <see cref="MoveTemplateFirst"/> throw <see cref="NoticeFailedException{T}"/> of <see cref="Contact"/> when the change was
/// fully saved and then a Changed handler or the local save report failed, even for one failure. Its Saved is the contact
/// as saved by Save, and null for the others. A write that failed partway, and then a notice failed too, throws an
/// <see cref="AggregateException"/> with the write's failure first.
/// </summary>
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

    /// <summary>
    /// The user decided this member: moves the template to the top of the mention templates file, keeping the others' order,
    /// writes the file, then raises Changed and reports the local save. Re-reads the file under the lock first, so a change
    /// on disk is kept. Writes nothing and reports no local save when the template is already first, when the file is missing
    /// or empty, was skipped at the latest load, or no longer lists the template. Unless the file was skipped, the list then
    /// holds the file as read, or the default for a missing or empty file, and Changed is raised when that differs from before.
    /// Throws, before writing: <see cref="ArgumentNullException"/> for a null template; <see cref="NoRepositoryFolderException"/> with no clone;
    /// <see cref="InvalidDataException"/> when the file can't be read now or needs a newer app, keeping the list; <see cref="InvalidOperationException"/>
    /// inside a Save change, which holds the lock. A failed write throws the file system's exception and leaves the file whole.
    /// </summary>
    void MoveTemplateFirst(string template);

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
    /// The template's Markdown for the contact, fit for a link's text: the template's own text as written, each value
    /// with ASCII punctuation and symbols backslash-escaped. A contact lacking a value the template names gets its escaped full name.
    /// Throws <see cref="ArgumentException"/> for an invalid template.
    /// </summary>
    string TextOf(Contact contact, string template);

    /// <summary>
    /// The live Markdown text for a stored mention, as <see cref="TextOf(Contact, string)"/> writes it; null when the address finds no single contact,
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
    /// The whole Markdown link for a new mention: <c>[text](contact:&lt;id&gt; "&lt;template&gt;")</c>, with the text exactly as <see cref="TextOf(Contact, string)"/> gives it.
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
    /// a Save, Delete, MoveGroup, or MoveTemplateFirst inside it throws <see cref="InvalidOperationException"/>.
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
