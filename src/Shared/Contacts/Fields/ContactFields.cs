using System.Globalization;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>How a field's value is written: plain text, a data type entry's id, a time HH:mm, or a date yyyy-MM-dd.</summary>
public enum ContactFieldKind
{
    Text,
    Key,
    Time,
    Date,
}

/// <summary>One known contact field: its name in the file, its kind, and how to read it from a contact and set it as text.</summary>
public sealed class ContactField
{
    private readonly Func<Contact, string?> _textOf;
    private readonly Func<Contact, string?, Contact> _with;

    internal ContactField(
        string name, ContactFieldKind kind, bool isTeamField, Func<Contact, string?> textOf, Func<Contact, string?, Contact> with)
    {
        Name = name;
        Kind = kind;
        IsTeamField = isTeamField;
        _textOf = textOf;
        _with = with;
    }

    /// <summary>The field's name in the file, such as "reportsTo".</summary>
    public string Name { get; }

    public ContactFieldKind Kind { get; }

    /// <summary>Whether the field table marks it Team, so the Contact list shows it only in team groups.</summary>
    public bool IsTeamField { get; }

    /// <summary>The value as the file writes it, or null when the field is missing.</summary>
    public string? TextOf(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return _textOf(contact);
    }

    /// <summary>
    /// The contact with this field set from text written as the file writes it. Null or empty makes the field missing;
    /// the ID and full name become empty, which Save rejects. Throws <see cref="FormatException"/> for a time or date written in another form.
    /// </summary>
    public Contact With(Contact contact, string? text)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return _with(contact, string.IsNullOrEmpty(text) ? null : text);
    }
}

/// <summary>The known contact fields, in the order the app writes them.</summary>
public static class ContactFields
{
    private const string DateFormat = "yyyy-MM-dd";

    public static IReadOnlyList<ContactField> All { get; } =
    [
        Text("id", false, c => c.Id, (c, v) => c with { Id = v ?? "" }),
        Text("alias", false, c => c.Alias, (c, v) => c with { Alias = v }),
        Text("fullName", false, c => c.FullName, (c, v) => c with { FullName = v ?? "" }),
        Text("firstName", false, c => c.FirstName, (c, v) => c with { FirstName = v }),
        Text("title", false, c => c.Title, (c, v) => c with { Title = v }),
        Text("location", true, c => c.Location, (c, v) => c with { Location = v }),
        Text("levelId", true, c => c.LevelId, (c, v) => c with { LevelId = v }),
        Text("workingType", true, c => c.WorkingType, (c, v) => c with { WorkingType = v }),
        Text("pronounsKey", false, c => c.PronounsKey, (c, v) => c with { PronounsKey = v }),
        Text("careerPathKey", false, c => c.CareerPathKey, (c, v) => c with { CareerPathKey = v }),
        Text("reportsTo", false, c => c.ReportsTo, (c, v) => c with { ReportsTo = v }),
        new("dayStartTime", ContactFieldKind.Time, true,
            c => c.DayStartTime is { } time ? ContactTime.TextOf(time) : null,
            (c, v) => c with { DayStartTime = v is null ? null : ParseTime(v) }),
        Date("levelStartDate", c => c.LevelStartDate, (c, v) => c with { LevelStartDate = v }),
        Date("teamMemberSince", c => c.TeamMemberSince, (c, v) => c with { TeamMemberSince = v }),
        Date("exitDate", c => c.ExitDate, (c, v) => c with { ExitDate = v }),
    ];

    private static readonly Dictionary<string, ContactField> ByName = All.ToDictionary(field => field.Name);

    internal static ContactField? Named(string name) => ByName.GetValueOrDefault(name);

    /// <summary>A text field, or a key field when <see cref="KeyFields"/> lists it.</summary>
    private static ContactField Text(string name, bool isTeamField, Func<Contact, string?> textOf, Func<Contact, string?, Contact> with) =>
        new(name, KeyFields.All.Any(key => key.Field == name) ? ContactFieldKind.Key : ContactFieldKind.Text, isTeamField, textOf, with);

    /// <summary>A date field; every date field is a team field.</summary>
    private static ContactField Date(string name, Func<Contact, DateOnly?> read, Func<Contact, DateOnly?, Contact> write) =>
        new(name, ContactFieldKind.Date, true,
            c => read(c)?.ToString(DateFormat, CultureInfo.InvariantCulture),
            (c, v) => write(c, v is null ? null : ParseDate(v)));

    private static TimeOnly ParseTime(string text) =>
        ContactTime.TryRead(text, out var time) ? time : throw new FormatException($"'{text}' isn't a time written HH:mm.");

    private static DateOnly ParseDate(string text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new FormatException($"'{text}' isn't a date written yyyy-MM-dd.");
}
