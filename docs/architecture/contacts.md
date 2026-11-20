# Contacts: contact files, relations, search, merge, and mentions

Status: Decided

Contacts keeps the people the user works with, in files in the synced repository, so every device has the same list. A task mentions a contact by a link that shows the contact's current details each time it's displayed. This page decides the files, how contacts relate, how they're searched, saved, and merged, and how a mention is stored and shown. The desktop [Contact list](desktop/contact-list.md) is the window that shows and edits them.

## Behavior

### Contacts is a shared module of its own

The user decided that contacts are a module of their own, `Memoria.ToDo.Shared.Contacts` in `src/Shared/Contacts/`, not a part of Tasks. It uses no other module. It owns the contact files, their reading, writing, and merge rules, search, relations, the mention format, the mention templates, and the data types key fields choose from.

It shares the clone with Tasks through the shared platform's [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload): the same selected folder, the same lock, the same reload after a sync, and the same list of skipped files. So neither store knows the other, and a sync updates both under one lock. It also writes and skips files by the platform's [JSON style and file rules](modules.md#stores-share-one-json-style-and-one-set-of-file-rules), as Tasks does.

These modules use it:

| Module | What it uses |
| --- | --- |
| [Sync](sync.md#sync-manages-contact-files-through-contacts-rules) | Which files are contact files, and how two versions of one merge |
| Desktop [Contact list](desktop/contact-list.md) | Everything: groups, details, search, saving, adding, deleting, and moving groups |
| Desktop [Task editor](desktop/task-editor.md#typing-an-at-sign-mentions-a-contact) and [Android Task editor](android/task-editor.md#typing-an-at-sign-mentions-a-contact) | Search, the templates, and the mention to insert, for the "@" list; and the live text, for the mention chips |
| Desktop [Task list](desktop/task-list.md#mentions-show-each-contacts-current-details) and [Android Task list](android/task-list.md#mentions-show-each-contacts-current-details) | The live text of each mention they show, and the editors' contacts |
| Each app's composition root | Creating the store |

### The clone keeps contacts in group files under `autocomplete/contacts/`

| Path | What it holds |
| --- | --- |
| `autocomplete/contacts/<group>.json` | One group of contacts: `{ "version": 1, "position": 1000, "members": [ ... ] }` |
| `autocomplete/contacts/me.json` | The user's own contact: `{ "version": 1, "position": 100, "details": { ... } }` |
| `autocomplete/contacts/dataTypes/careerPaths.json`, `pronouns.json` | One [data type](#key-fields-choose-from-the-users-data-type-files): the entries a key field chooses from, `{ "version": 1, "pronouns": [ ... ] }` with the list named after the file |
| `autocomplete/contacts/dataTypes/mentionTemplates.json` | The [mention templates](#mention-templates-come-from-a-synced-file): `{ "version": 1, "templates": [ ... ] }` |

- **Every `.json` file directly in `autocomplete/contacts/` is a group**, named by its file name without `.json`. Files in its subfolders, such as `dataTypes/`, aren't groups. `me.json` is the user's own group, named "me", with one contact.
- **The user decided a group's name is its file name exactly, case included.** On a file system that tells case apart, `team.json` and `Team.json` load as two groups, though their headings may look alike. Every call that names a group, such as Save, Delete, and MoveGroup, finds only the group with exactly that name. Only the [team check](#team-groups-show-more-fields) and the check for the user's own group ignore case. The user decided `Me.json` is the user's own group too, named "Me". Proposed, a decision to confirm: when more than one file is the user's own group, such as `me.json` and `Me.json`, each is skipped and reported as another file that differs only in case, so the user has no own contact until one is renamed.
- **Group files, data type files, and the templates file hold a required `version`, starting at 1**, the user's choice, as task files do. A newer app reads every older version. A file with a newer version is skipped, left unchanged, and reported as needing a newer app. A file with no version, or one that isn't a whole number from 1 up, is skipped. The platform's [version rule](modules.md#stores-share-one-json-style-and-one-set-of-file-rules) checks it.
- **The clone has contacts when at least one group file exists**, the user's choice: the desktop app [offers Contacts](desktop/shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) only then. Proposed, a decision to confirm: a group file that's skipped still counts, so the window can say why it's skipped; data type and templates files alone don't count. Contacts checks again whenever it loads, and raises Changed when the answer changes.
- **A contact is a JSON object of text fields.** The table lists the known fields, in the order the app writes them. The For column marks the [team fields](#team-groups-show-more-fields), which the Contact list shows only in team groups:

  | Field | Holds | Required | For |
  | --- | --- | --- | --- |
  | `id` | The contact's ID, as [below](#an-id-and-an-optional-alias-name-each-contact) | Yes | Every contact |
  | `alias` | A short name, such as a sign-in name | No | Every contact |
  | `fullName` | The name shown everywhere | Yes | Every contact |
  | `firstName`, `title` | Text | No | Every contact |
  | `location`, `levelId`, `workingType` | Text | No | Team |
  | `pronounsKey`, `careerPathKey` | The `id` of an entry in a [data type file](#key-fields-choose-from-the-users-data-type-files) | No | Every contact |
  | `reportsTo` | The manager's alias or ID, as [below](#reportsto-names-the-manager) | No | Every contact |
  | `dayStartTime` | A time of day, written `HH:mm`, such as `09:30`. The user decided an hour of one digit, such as `9:30`, reads too. | No | Team |
  | `levelStartDate`, `teamMemberSince`, `exitDate` | A date, `yyyy-MM-dd` | No | Team |

- **The user decided every field is allowed in every group**, including the user's own, team fields too.
- **`position` is a whole number** that orders the groups, as [below](#groups-keep-the-order-of-their-positions).
- **Every text value is one line.** Empty values count as missing.
- Proposed, each a decision to confirm:
  - `id` and `fullName` are required, and `position` is required in each group file.
  - A file that breaks any rule on this page, such as a missing `fullName`, a date that isn't `yyyy-MM-dd`, or one ID twice in the same file, is skipped and reported, never changed, and its contacts don't load. The rest load.

The user confirmed how the app writes a group file, so a saved file's diff shows only what changed:

- **In the platform's [JSON style](modules.md#stores-share-one-json-style-and-one-set-of-file-rules)**, with the group's members sorted by `fullName`, then `id`, ignoring case.
- **Empty fields are left out, and fields Contacts doesn't know are kept as they are**, in a contact or at a file's top.
- **Fields come in one fixed order**, the user's choice. At a file's top: `version`, `position`, then `members` or `details`, then unknown fields, with `version` first as in `settings.json` and `tags.json`. In a contact: the table's order, then unknown fields in the order the file had them. So the first save of a hand-written file may reorder its fields once.

### Team groups show more fields

The user decided there are two kinds of contacts: team contacts, and everyone else. Team contacts have more fields: the ones the [field table](#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts) marks Team.

- **A group is a team group by its file name**: `team.json` and `ex-team.json`, the groups `team` and `ex-team`. Every other group, `me` included, isn't.
- **Team fields are allowed everywhere**, the user's choice. Contacts loads, keeps, saves, searches, and merges them in any group alike. Only the [Contact list's details](desktop/contact-list.md#the-details-show-every-field-and-edit-in-place) show them for team groups alone.
- **Contacts tells screens a group's kind**, through `ContactGroup.IsTeam`, so the team groups' names have one home.
- **Contacts tells screens the fields too**, through its public `ContactFields`, the user's choice: each field's name, its kind, whether it's a team field, and its value as the file writes it. So the field table has one home, and a screen keeps only its own labels.

The user decided the team check ignores case, so `Team.json` and `EX-TEAM.json` are team groups too, though group names otherwise [compare exactly](#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts).

### An ID and an optional alias name each contact

The user decided that a contact may have an alias, a real field the user adds to the files, and that `reportsTo` and the rest of the app look a name up by alias first, then by ID. IDs and aliases are one set of names: no name may be another contact's ID or alias.

Proposed, each a decision to confirm:

- **The alias is optional**, so today's files load before the user adds aliases.
- **IDs and aliases compare ignoring case.** `RazvanP` and `razvanp` are the same name.
- **An ID or alias holds only letters, digits, `-`, `_`, and `.`**, so a mention's address needs no escaping. A file with any other name is skipped and reported.
- **Letters and digits are ASCII only**, so `é` is not allowed in an ID or alias; such a file is skipped and reported. Proposed, a decision to confirm. The `.json` of a group file and the `contact:` of an address compare exactly, not ignoring case.
- **A field that is `null` or not text makes the file unreadable**, rather than counting as missing.
- **An ID never changes once saved.** The [Contact list](desktop/contact-list.md#add-and-delete-change-the-contacts) lets the user type it only when adding a contact.

### A name two contacts share points to nobody

The user decided that when two contacts share a name, in one file or two, both still load, the shared name is reported, and it resolves to nobody: a `reportsTo` that names it finds no manager, and a mention of it shows its stored text. Each contact still shows and can be edited and searched. The Contact list and Sync's status list the shared names, so the user can fix the files.

Proposed, a decision to confirm: one ID twice in the same file still makes that file unreadable, since the [merge](#sync-merges-contact-files-contact-by-contact-and-field-by-field) matches contacts in a file by ID.

### `reportsTo` names the manager

A contact's `reportsTo` holds its manager's alias or ID. It finds the contact with that alias, or else the one with that ID. A name no contact holds, or a shared one, finds nobody, and the name shows as written. A contact's direct reports are the contacts whose `reportsTo` finds it, sorted by `fullName`.

### Search matches every typed word in any field

The user decided that search, in the Contacts window and in the "@" list alike, matches a contact when every word typed appears, ignoring case, somewhere in its known fields. Empty search text matches everyone. One search serves both, so they always agree. The user decided a time is searched as `HH:mm`, such as `09:30`, as the app writes it, even when the file holds `9:30`.

Proposed, each a decision to confirm:

- A date is searched as `yyyy-MM-dd`, as the file holds it.
- The text is split on whitespace, and each word is matched as a substring. A key field is searched by its stored key, not its entry's name.
- The group name and unknown fields aren't searched.
- Matches come sorted by `fullName`, then `id`, ignoring case.

### Saving, adding, and deleting change one group file

- **Saving a contact writes only its group's file**, then announces the change. A contact whose ID the group doesn't hold yet is added to it.
- **Save checks the contact first and writes nothing invalid**: a missing ID or full name, a name with other characters, a multi-line value, a bad date or time, an ID or alias another contact holds, or a key its data type doesn't list. A name the saved contact already shared, or a key it already held, stays allowed, so the user can still fix its other fields.
- **The user's own contact can be saved but never added or deleted**, and its ID never changes.
- **Deleting a contact removes it from its group's file.** An emptied group file stays, as a group with no members.
- **After a delete**, a `reportsTo` that named the contact shows its name as written, and a mention of it shows its stored text.
- **A group file that was skipped can't be saved into**, so the app never overwrites a file it couldn't read.

### Groups keep the order of their positions

- **Groups show in ascending `position`**, with ties sorted by group name, ignoring case. The user's own group takes part like any other. The user decided names that differ only in case then sort exactly, by ordinal comparison, so `Team` comes before `team` on every device.
- **The user decided a group's heading is its file name with each hyphen-separated word capitalized**, hyphens kept: `ex-team.json` shows "Ex-Team", and `team.json` "Team". So the heading has one home in Contacts.
- **The user decided that dragging a group's heading reorders the groups**, and the app rewrites the positions.

Proposed, each a decision to confirm:

- **A move writes only the moved group's file when it can.** The moved group takes the whole number halfway between its new neighbors' positions, rounded down; at the top, the first position minus 1000; at the bottom, the last plus 1000.
- **When no whole number lies strictly between the neighbors**, such as two groups with equal positions, every loaded group is renumbered 1000, 2000, 3000, and so on, in the new order, and every file whose position changed is written.
- **A move to where the group already is writes nothing and raises nothing.**
- **A position that would leave the range of a whole number (`int`) renumbers every group**, as above.
- **A renumber that fails partway publishes what it wrote**: Contacts raises Changed and reports the local save when at least one file was written, then throws the failure.
- **Two devices that reorder at once merge each file's position on its own**, with the remote winning, so the result may be an order neither device chose. This is an accepted risk: the user drags again.

### Key fields choose from the user's data type files

The user decided that a key field's choices come from synced files the user writes by hand, with no editor for now, and the user already made them in `autocomplete/contacts/dataTypes/`. Each file a key field reads, listed below, is one data type; the folder also holds the [templates file](#mention-templates-come-from-a-synced-file), which isn't. A data type file is an object with a `version` and one list named after the file without `.json`, such as `{ "version": 1, "careerPaths": [ ... ] }`; the user chose this shape. The list holds objects, each with a unique `id` and any other properties that describe it, such as a career path's `name` and `short`, or a pronoun's `subject` and `object`. A key field stores an entry's `id`.

Today two fields are key fields: `careerPathKey` chooses from `careerPaths.json`, and `pronounsKey` from `pronouns.json`. `levelId` and `workingType` have no data type file, so they're plain text.

The user decided that **a missing data type file is reported only when the clone [has contacts](#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts)**, so users who don't keep contacts see no problem in Sync's status. A data type file that's there but can't be read is always reported.

Proposed, each a decision to confirm:

- **Contacts lists which field reads which file**, rather than deriving the file name from the field's, since no one rule fits both: `careerPathKey` to `careerPaths` adds an "s", and `pronounsKey` to `pronouns` doesn't. A new key field needs a code change anyway, so the form knows it.
- **An entry shows as its `name`, or its `id` when it has none**, so pronouns show "he/him" and career paths "Software Engineer".
- **An entry's properties are text, or lists of text such as `alternatives`.** Contacts reads every text property, so a [mention template](#mention-templates-come-from-a-synced-file) can show any of them, and keeps the rest as they are.
- **Entry IDs are unique ignoring case, and a stored key finds its entry ignoring case.** With no clone, `DataTypes` is empty and `MentionTemplates` is the default list. A missing data type file is reported with the reason "The file is missing."
- **A key field takes only an entry from its file**, so every stored key finds its entry. The user adds a new choice to the file first.
- **A contact's key that its file doesn't list is kept and shown as written**, and Save keeps it until the user picks an entry. It's never dropped or changed.
- **A data type file can't be read when its version is missing or invalid, its list is missing or isn't a list of objects, it has any other top-level property, or it has an entry with no `id`, a repeated `id`, or a property of another kind.** Another top-level property is refused rather than ignored, since a merge writes only the version and the list and would drop it. Missing or unreadable, it's listed in the skipped files as above, and its field takes no new choice: the form shows the stored key as written and says which file is wrong, as the [Contact list](desktop/contact-list.md#the-details-show-every-field-and-edit-in-place) decides. Contacts that use it still load.

### A mention is a link that names its contact and its template

The user decided that a mention is a Markdown link that also names a display template, which says which of the contact's fields show, such as `{fullName} ({title})` or `{firstName}`. Its text is rebuilt from the contact's current fields each time it shows; the text stored in the link is shown only when the contact is unknown, such as after a delete.

The user confirmed that **a mention is stored as `[text](contact:<id> "<template>")`**, with the address from the platform's [contact address](modules.md#a-contact-address-has-one-home). The template sits in the link's title, a standard Markdown part, so any Markdown reader still sees a link with its text. A mention always stores the ID, never the alias, since the ID never changes.

Proposed, each a decision to confirm:

- **The mention keeps the template's text itself, not its name or place in the list**, so editing, reordering, or removing templates never changes an existing mention.
- **The stored text** escapes `[`, `]`, and `\` as a task link's text does, through the platform's [Markdown label](../../src/Shared/Platform/MarkdownLabel.cs). In the title, `"` and `\` take a backslash.
- **A mention shows on a soft accent background**, one per theme, as the user decided and [Rendering](rendering.md#a-mention-shows-on-a-colored-background) owns.
- **Clicking a mention does nothing.**
- **Task search matches a mention's stored text**, since Tasks searches the task as saved.

### Mention templates come from a synced file

The user decided that the templates are a list in a synced file the user writes by hand, with no editor for now, and that Tab in the "@" list cycles through them.

- **The file is `autocomplete/contacts/dataTypes/mentionTemplates.json`**, the user's choice: `{ "version": 1, "templates": ["{fullName}", "{fullName} ({careerPath.short})", "{firstName}"] }`. Its order is the order Tab cycles them in. It isn't a [data type](#key-fields-choose-from-the-users-data-type-files): no key field chooses from it.
- **With no file, the list is `["{fullName}"]`**, and nothing is reported. The user decided an empty file, of zero bytes, counts as no file.
- **A template shows a time as `HH:mm`**, as [search](#search-matches-every-typed-word-in-any-field) does, the user's choice. Proposed, a decision to confirm: a date shows as `yyyy-MM-dd`.

Proposed, each a decision to confirm:

- **A placeholder is a known field's name in braces**, such as `{title}`. `{{` and `}}` write a literal brace.
- **A placeholder can also reach a key field's entry**, as `{<field without Key>.<property>}`: `{careerPath.short}` shows "EM" for `careerPathKey: "em"`, so `{fullName} ({careerPath.short})` shows "Razvan Popov (EM)", and `{pronouns.subject}` shows "he". The property is any text property of the entry, since entries differ.
- **A templates file that lists no template is unreadable**, the same as one with an invalid template; only a file of zero bytes means the default.
- **A template is invalid when it's empty, names an unknown field or key field, or has an unmatched brace.** A file with an invalid template, or that can't be read, is skipped and reported, and the default list is used.
- **When the contact lacks any value the template names, the mention shows the contact's full name**: a missing field, a key its data type doesn't list, or an entry without the property. So a mention never shows half a text such as "Ana ()".

### A mention's text is rebuilt from the contact each time it shows

Each screen that shows a mention, in a title, a description, or an editor, asks Contacts for its live text, from the address and template the link holds. Contacts answers nothing when:

- the address isn't a contact's, or names an ID no contact or more than one contact holds;
- the stored template is invalid.

Proposed, a decision to confirm: an address resolves by ID only, never by alias, so a mention never changes meaning when an alias is added. Contacts also raises Changed after every reload, even one that changed nothing.

The screen then shows the stored text, with the mention look. Screens show the new text when contacts change.

### Sync merges contact files contact by contact and field by field

Sync commits and merges the contact files, the data type files, and the templates file like task files, through [Contacts' merge rules](#public-interface). The user decided how two versions of a group file merge:

- **Three-way, by contact ID and field.** A field only one side changed takes that side's value. When both sides changed it, the remote wins.
- **A contact added on either side is kept.**
- **A deletion on one side deletes**, unless the other side changed that contact; then the edit is kept.

The user also decided that when one side deletes the templates file and the other edits it, the edit wins, so a merge never drops templates the user wrote.

The user decided that a merged group or data type file is always written in this app's layout, as task files are, even when only one side changed it. So a hand-written file another device pushed is reformatted when it merges.

Proposed, each a decision to confirm:

- `position`, `me.json`'s `details`, and unknown fields merge the same way, field by field.
- A merged group or data type file is written as Save writes a group file, with this app's `version`.
- A merge applies the load rules to every present side and to the result, so an unreadable side stops it with `InvalidDataException`. A merged data type entry keeps the remote's property order, then the properties only the local side has.
- A whole file deleted on one side and changed on the other is kept, with the changes, as a contact edit beats a deletion.
- With no common version, such as a file both sides added, every contact is kept, and a field that differs takes the remote's value.
- A data type file merges the same way, by entry `id` and property, with the remote winning a property both changed, and an edit beating a deletion. A list property, such as `alternatives`, counts as one value. The merged entries keep the remote's order, with entries only the local side added after them, in its order.
- The templates file merges as a whole file, since a template's place in the list is its meaning: the changed side wins, and the remote when both changed, except the edit over a deletion above. The merge returns the winning side's bytes unchanged. An empty file is a file with its own bytes here, not a missing one.
- A file that can't be read on one side follows [Sync's rule](sync.md#sync-manages-contact-files-through-contacts-rules) for contact files: it's kept and reported.

### Contacts stays out of the screens

Contacts decides data, never look or layout. The [Contact list](desktop/contact-list.md) decides the Contacts window; the [desktop](desktop/task-editor.md#typing-an-at-sign-mentions-a-contact) and [Android](android/task-editor.md#typing-an-at-sign-mentions-a-contact) Task editors decide the "@" list and the mention chips; [Rendering](rendering.md#a-mention-shows-on-a-colored-background) decides the mention's look; and [Editing](editing.md#typing-an-at-sign-opens-the-mention-list) decides where "@" opens the list. The Android app has no Contacts screen for now.

## Public interface

The project is `Memoria.ToDo.Shared.Contacts`. It uses the shared platform's [`IFileSystem`](modules.md#the-file-system-interface-reads-and-writes-whole-files), [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), [JSON style and file rules](modules.md#stores-share-one-json-style-and-one-set-of-file-rules), [`ContactAddress`](modules.md#a-contact-address-has-one-home), and [`MarkdownLabel`](../../src/Shared/Platform/MarkdownLabel.cs). The user confirmed these declarations, with `IsTeam` added for [team groups](#team-groups-show-more-fields), and Contacts implements them in [`src/Shared/Contacts/`](../../src/Shared/Contacts/IContacts.cs). The desktop composition root creates the store right after Tasks' store, before the first Select, and hands its `Files` to [Sync](sync.md#sync-manages-contact-files-through-contacts-rules), from slice (c) of [step 15](desktop/README.md#delivery-backlog). The Android composition root does the same when the Android app is built:

```csharp
namespace Memoria.ToDo.Shared.Contacts;

public static class ContactStore
{
    /// Creates the store and adds it to the repository's parts, so it loads on every Select and Update.
    /// Must be called before the first Select.
    public static IContactStore Create(IFileSystem files, IRepositoryAccess repository);
}

public interface IContactStore
{
    IContacts Contacts { get; }

    /// Sync's file rules; they never read or write files.
    IContactFileRules Files { get; }
}

/// One contact's known fields. Null means the field is missing.
public sealed record Contact(
    string Id, string FullName, string? Alias = null, string? FirstName = null, string? Title = null,
    string? PronounsKey = null, string? CareerPathKey = null, string? LevelId = null, string? WorkingType = null,
    string? Location = null, string? ReportsTo = null, TimeOnly? DayStartTime = null,
    DateOnly? LevelStartDate = null, DateOnly? TeamMemberSince = null, DateOnly? ExitDate = null);

/// A contact's time of day as text, the way Contacts reads, writes, and searches dayStartTime.
/// The user decided it's public, so the Contact list's time field reads the same forms (slice (e) of step 15).
public static class ContactTime
{
    /// The time written HH:mm, such as "09:30".
    public static string TextOf(TimeOnly time);

    /// Reads H:mm or HH:mm, from 0:00 to 23:59. False for anything else, null and empty included.
    public static bool TryRead(string? text, out TimeOnly time);
}

/// How a field's value is written: plain text, a data type entry's id, a time HH:mm, or a date yyyy-MM-dd.
public enum ContactFieldKind { Text, Key, Time, Date }

/// One known contact field. The user decided it's public, so the Contact list reads the field table
/// from here instead of keeping its own copy (slice (e) of step 15).
public sealed class ContactField
{
    /// The field's name in the file, such as "reportsTo".
    public string Name { get; }

    public ContactFieldKind Kind { get; }

    /// Whether the field table marks it Team, so the Contact list shows it only in team groups.
    public bool IsTeamField { get; }

    /// The value as the file writes it, or null when the field is missing.
    public string? TextOf(Contact contact);

    /// The contact with this field set from text written as the file writes it. Null or empty makes
    /// the field missing; the ID and full name become empty, which Save rejects.
    /// Throws FormatException for a time or date written in another form.
    public Contact With(Contact contact, string? text);
}

/// The known contact fields.
public static class ContactFields
{
    /// Every known field, in the field table's order.
    public static IReadOnlyList<ContactField> All { get; }
}

/// One group file. The user's own group is named "me". IsTeam is true for the team and ex-team groups.
public sealed record ContactGroup(string Name, string Heading, int Position, bool IsTeam, IReadOnlyList<Contact> Members);

/// One entry of a data type file. Text holds every text property but id, by name.
public sealed record DataTypeEntry(string Id, string Shown, IReadOnlyDictionary<string, string> Text);

/// A key field's data type. Loaded is false when its file is missing or skipped; Entries is then empty.
public sealed record DataType(string KeyField, string RelativePath, bool Loaded, IReadOnlyList<DataTypeEntry> Entries);

/// The contacts in the selected clone. Safe to call from any thread; runs one call at a time.
public interface IContacts
{
    /// Whether the clone has at least one group file directly in autocomplete/contacts/, skipped or not. False with no clone.
    bool HasContactFiles { get; }

    /// Every loaded group, in display order; members by full name, then ID. Empty with no clone.
    IReadOnlyList<ContactGroup> Groups { get; }

    /// The user's own contact, or null when me.json is missing or skipped.
    Contact? Me { get; }

    /// Names two or more contacts hold, in alphabetical order.
    IReadOnlyList<string> SharedNames { get; }

    /// The templates, in file order, or the default list when the file is missing, empty, or skipped.
    IReadOnlyList<string> MentionTemplates { get; }

    /// Every key field's data type, in the order the form shows the fields, such as "careerPathKey".
    IReadOnlyList<DataType> DataTypes { get; }

    /// The one contact holding the alias, or else the ID; null when none or several do.
    Contact? Find(string name);

    /// The contact's manager, through reportsTo; null when it finds nobody.
    Contact? ManagerOf(Contact contact);

    /// The contacts whose reportsTo finds this contact, by full name.
    IReadOnlyList<Contact> DirectReportsOf(Contact contact);

    /// Contacts with every word of text in a known field, ignoring case, by full name, then ID.
    IReadOnlyList<Contact> Search(string text);

    /// The template's text for the contact, or its full name when it lacks a value the template names.
    /// Throws ArgumentException for an invalid template.
    string TextOf(Contact contact, string template);

    /// The live text for a stored mention; null when the address finds no single contact,
    /// or the template is invalid. The caller then shows the stored text.
    string? TextOf(string address, string template);

    /// The whole Markdown link for a new mention: [text](contact:<id> "<template>").
    /// Throws ArgumentException for an invalid template.
    string MentionOf(Contact contact, string template);

    /// Adds the contact to the group, or replaces the one with its ID, writes the group's file, then raises Changed.
    /// Throws, before writing: NoRepositoryFolderException with no clone; ArgumentException for an invalid
    /// contact, a name another contact holds, a key its data type doesn't list that the contact didn't hold,
    /// an unknown group, a new contact in "me", or a new ID for the
    /// user's own contact; InvalidOperationException when the group's file was skipped.
    void Save(string groupName, Contact contact);

    /// Removes the contact from the group's file, then raises Changed. Throws, before writing:
    /// NoRepositoryFolderException; ArgumentException for "me", an unknown group, or an ID the group doesn't hold;
    /// InvalidOperationException when the group's file was skipped.
    void Delete(string groupName, string id);

    /// Moves the group between two groups now next to each other, null at either end, rewriting positions
    /// as the design says, then raises Changed. Throws ArgumentException when a group is unknown,
    /// or previous and next aren't neighbors once the group is taken out; NoRepositoryFolderException.
    void MoveGroup(string groupName, string? previousGroup, string? nextGroup);

    /// Every file skipped at the latest load, with its reason.
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// Raised after any change to what this interface returns, on the thread whose call made it,
    /// or after the repository's reload.
    event EventHandler? Changed;
}

/// Sync's rules for the files Contacts owns.
public interface IContactFileRules
{
    /// Whether the path, relative to the clone, is a group file, a data type file, or the templates file.
    /// Throws ArgumentException for a rooted path or one with "." or ".." parts.
    bool Holds(string relativePath);

    /// The merged bytes, or null when the merged file is deleted. Null means the side has no file.
    /// Throws InvalidDataException when a present side can't be read or needs a newer app;
    /// ArgumentException when not Holds. Never changes its inputs.
    byte[]? Merge(string relativePath, byte[]? basis, byte[]? local, byte[]? remote);
}
```

## Failure modes

- **A group file, `me.json`, or the templates file can't be read, breaks a rule, or needs a newer app**: it's skipped, never changed, and listed in the skipped files, which Sync's status shows; one that needs a newer app says so, as a [task file](tasks.md#each-task-file-holds-nine-fields) does. The rest load. A skipped templates file means the default template; an empty one means the default with nothing listed.
- **A data type file can't be read**: it's skipped and listed in the skipped files, and its key field takes no new choice while keeping every stored key, as [above](#key-fields-choose-from-the-users-data-type-files). A missing one is treated the same, but listed only when the clone has contacts.
- **A contact's key isn't in its data type file**: the key is kept and shown as written, and a template that reads its entry shows the full name.
- **Two contacts share a name**: both load, the name finds nobody, and it's listed in the shared names.
- **No clone is selected**: every list is empty, and Save, Delete, and MoveGroup fail with `NoRepositoryFolderException` before writing.
- **A write fails**: the call throws the file system's exception; the file stays whole, as the [file system interface](modules.md#the-file-system-interface-reads-and-writes-whole-files) promises, and the screen that asked says why.
- **A Changed handler or the local save report fails**: after a write, Contacts still raises Changed and reports the local save, each even when the other fails. The user decided this follows the [synced repository's rule](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload): one failure is thrown as it is, and several make an `AggregateException`, the write's own failure first, then Changed's, then the report's.
- **A sync deletes a contact being edited in the Contact list**: Save adds it back. This is an accepted risk, decided with the [Contact list](desktop/contact-list.md#failure-modes).

## Ruled out

- **Contacts as a capability of Tasks**, through `ITaskStore.Contacts`: the user chose a module of its own, so tasks and contacts change apart.
- **A lock or a reload of its own**: a sync could change the clone between the two stores' locks. The platform's [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload) gives both one lock.
- **`reportsTo` by ID only, with the files changed to hold IDs**: the user keeps aliases there, and added the alias field instead.
- **A required alias**: today's files would fail to load until every contact has one.
- **When two contacts share a name, skipping both, or keeping the first**: the user chose to keep both and resolve the name to nobody, so no data hides and no guess is made.
- **Merging whole files, or letting the local side win**: the user chose a three-way merge by contact and field, with the remote winning a field both changed, and an edit beating a deletion.
- **Fields allowed only in some groups**: the user allowed every field everywhere. Allowing [team fields](#team-groups-show-more-fields) only in team groups was ruled out too: a file moved between groups, or written by hand, would be skipped or lose data. Only the Contact list's form hides them.
- **A mention that stores only its text, or only the contact's ID with a fixed look**: the user chose live text from a chosen template, with the stored text as the fallback.
- **A mention that names its template by name or by place in the list**: changing the templates file would change or break old mentions.
- **A template editor, or templates kept in the app's settings**: the user chose a hand-written synced file, with no editor for now.
- **Search over names, the alias, and the ID only**: the user chose every field.
- **A new choices file, or one file for every field's choices**: the user already keeps one file for each data type in `dataTypes/`, with details that templates can show.
- **Deriving a key field's file from its name, such as `<name>Key` to `<name>s.json`**: it fits `careerPathKey` but not `pronounsKey`.
- **Free typing in a key field**: a typed value would find no entry, so templates couldn't show its details. The user adds the entry to the file first.
- **Dropping or clearing a key its file doesn't list**: the user's data would change without the user asking.
- **Templates that show only the contact's own fields**: a short form such as a career path's "EM" lives only in the data type file.
- **Merging a data type file as a whole file**: one device's new entry would be lost when the other device changed any entry.
- **Keeping each contact's own field order when writing, adding new fields at its end**: the merge would have to pick one side's order, and the code would remember every contact's order. The user chose [one fixed order](#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts).
- **Always reporting a missing data type file**: every user without contacts would see two problems in Sync's status. **Never reporting a missing one**: Sync's status would never say the file is missing.
- **Contact files with no `version`, so none ever needs a newer app, or with an optional one**: the user chose a required version, as task files have, so a newer format is skipped unchanged rather than misread.
- **Data type files as plain arrays with no version**: the first design, since the user writes them by hand. The user later chose a versioned object, as the other contact files have, and the app no longer reads the plain array.
- **Reading only `H:mm` for `dayStartTime`, or writing `H:mm`**: a hand-written `09:30` would be skipped, and the two forms would mix. The user chose to read both and write `HH:mm`.
- **Letting a templates file deletion beat an edit**, as "the changed side wins" alone would: a merge would drop templates the user wrote.
- **Keeping a one-side change's bytes unchanged in a group or data type file merge**, in Contacts or in Sync for every file kind: the user chose to write every merged file in this app's layout, as task files are.

## Open questions

None.
