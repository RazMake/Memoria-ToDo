using System.Globalization;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>One choice of a key field's drop-down. <see cref="Mark"/> is "Not in the list" for a stored key the data type file doesn't list, else null.</summary>
internal sealed record FormChoice(string Key, string Text, string? Mark);

/// <summary>
/// The state of the form that edits or adds one contact, with the checks its own fields can make. Fields are named as in the
/// files: id, alias, fullName, firstName, title, location, levelId, workingType, pronounsKey, careerPathKey, reportsTo,
/// dayStartTime, levelStartDate, teamMemberSince, exitDate. It never calls Contacts' Save; <see cref="ToContact"/> says what to save.
/// </summary>
internal sealed class ContactForm
{
    private readonly IContacts _contacts;
    private readonly Contact _original;
    private readonly IReadOnlyList<ContactField> _shown;
    private readonly Dictionary<string, string> _initial = [];
    private readonly Dictionary<string, string> _current = [];

    private ContactForm(IContacts contacts, string groupName, Contact original, bool isNew)
    {
        var group = contacts.Groups.FirstOrDefault(g => g.Name == groupName)
            ?? throw new ArgumentException($"There is no group '{groupName}'.", nameof(groupName));
        _contacts = contacts;
        _original = original;
        GroupName = groupName;
        IsNew = isNew;
        _shown = [.. ContactFields.All.Where(field => group.IsTeam || !field.IsTeamField)];
        foreach (var field in _shown)
        {
            _initial[field.Name] = _current[field.Name] = field.TextOf(original) ?? "";
        }
    }

    /// <summary>A form on an existing contact of the group.</summary>
    public static ContactForm Edit(IContacts contacts, string groupName, Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(contact);
        return new(contacts, groupName, contact, isNew: false);
    }

    /// <summary>An empty form for a new contact of the group.</summary>
    public static ContactForm Add(IContacts contacts, string groupName)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        return new(contacts, groupName, new Contact("", ""), isNew: true);
    }

    public string GroupName { get; }

    public bool IsNew { get; }

    /// <summary>The fields the form shows; team fields only in a team group.</summary>
    public IReadOnlyList<string> FieldNames => [.. _shown.Select(f => f.Name)];

    /// <summary>The field's text: as typed, a stored time as HH:mm, empty for a missing value. Date fields are set with <see cref="SetDate"/>.</summary>
    public string this[string field]
    {
        get => _current.TryGetValue(field, out var text) ? text : throw NotShown(field);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Shown(field).Kind == ContactFieldKind.Date)
            {
                throw new ArgumentException($"Set the date field '{field}' with SetDate.", nameof(field));
            }

            if (IsReadOnly(field))
            {
                throw new InvalidOperationException($"The field '{field}' can't be typed in.");
            }

            _current[field] = value;
        }
    }

    // A date field's text is the file's yyyy-MM-dd, which is also the round-trip ("O") form of a DateOnly.
    public DateOnly? DateOf(string field) =>
        Shown(field).Kind == ContactFieldKind.Date && _current[field].Length > 0
            ? DateOnly.Parse(_current[field], CultureInfo.InvariantCulture)
            : null;

    public void SetDate(string field, DateOnly? date)
    {
        if (Shown(field).Kind != ContactFieldKind.Date)
        {
            throw new ArgumentException($"The field '{field}' isn't a date.", nameof(field));
        }

        _current[field] = date?.ToString("O", CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>Whether the user can't type in the field: the ID of an existing contact, and a key field whose data type isn't loaded.</summary>
    public bool IsReadOnly(string field)
    {
        var shown = Shown(field);
        return shown.Name == "id" ? !IsNew : shown.Kind == ContactFieldKind.Key && DataTypeOf(field) is not { Loaded: true };
    }

    /// <summary>A key field's choices: its data type's entries, with the stored key and the form's current key first and marked when the file doesn't list them.</summary>
    public IReadOnlyList<FormChoice> ChoicesOf(string keyField)
    {
        var entries = DataTypeOf(keyField)?.Entries ?? [];
        List<FormChoice> choices = [.. entries.Select(entry => new FormChoice(entry.Id, entry.Shown, null))];
        var name = Shown(keyField).Name;
        var orphans = new List<string>(2);
        foreach (var key in (string[])[_initial[name], _current[name]])
        {
            if (key.Length > 0
                && !entries.Any(entry => string.Equals(entry.Id, key, StringComparison.OrdinalIgnoreCase))
                && !orphans.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                orphans.Add(key);
            }
        }

        choices.InsertRange(0, orphans.Select(key => new FormChoice(key, key, "Not in the list")));
        return choices;
    }

    /// <summary>For a key field whose data type isn't loaded, text that says which file is wrong; else null.</summary>
    public string? DataTypeProblem(string keyField) =>
        DataTypeOf(keyField) is { Loaded: false } type
            ? $"The list of choices couldn't be read. Fix {type.RelativePath} in the repository."
            : null;

    /// <summary>The problem the form's own check finds in the field, shown under it; null when none.</summary>
    public string? ProblemOf(string field)
    {
        var text = this[field].Trim();
        return field switch
        {
            "id" when text.Length == 0 => "Write an ID.",
            "id" when IsNew && IsHeldByLoadedContact(text) => "Another contact has this ID.",
            "fullName" when text.Length == 0 => "Write the full name.",
            "dayStartTime" when text.Length > 0 && !ContactTime.TryRead(text, out _) => "Write the time as HH:mm, such as 09:30.",
            _ => null,
        };
    }

    public bool HasProblems => _shown.Any(f => ProblemOf(f.Name) is not null);

    /// <summary>Whether any field differs from what the form opened with.</summary>
    public bool HasChanges => _shown.Any(f => _current[f.Name] != _initial[f.Name]);

    /// <summary>The contact the form would save: empty fields missing, fields the form doesn't show kept as they were.</summary>
    public Contact ToContact()
    {
        if (HasProblems)
        {
            throw new InvalidOperationException("The form has problems.");
        }

        return _shown.Aggregate(_original, (contact, field) =>
        {
            return field.With(contact, _current[field.Name].Trim());
        });
    }

    internal ContactField FieldOf(string field) => Shown(field);

    private DataType? DataTypeOf(string keyField) => _contacts.DataTypes.FirstOrDefault(type => type.KeyField == keyField);

    // Contacts compares names ignoring case, and its Save replaces a contact of the same group that has the ID.
    private bool IsHeldByLoadedContact(string id) =>
        _contacts.Groups.Any(group => group.Members.Any(member => member.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));

    private ContactField Shown(string field) => _shown.FirstOrDefault(f => f.Name == field) ?? throw NotShown(field);

    private static ArgumentException NotShown(string field) => new($"The form doesn't show the field '{field}'.", nameof(field));
}
