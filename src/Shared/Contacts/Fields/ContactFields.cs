using System.Globalization;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One known contact field: its name in the file, how to read it from a contact, and how to set it.</summary>
internal sealed record ContactField(string Name, Func<Contact, string?> Get, Func<Contact, string, Contact> Set);

/// <summary>The known contact fields, in the order the app writes them.</summary>
internal static class ContactFields
{
    private const string TimeFormat = "HH:mm";
    private const string DateFormat = "yyyy-MM-dd";

    public static readonly IReadOnlyList<ContactField> All =
    [
        new("id", c => c.Id, (c, v) => c with { Id = v }),
        new("alias", c => c.Alias, (c, v) => c with { Alias = v }),
        new("fullName", c => c.FullName, (c, v) => c with { FullName = v }),
        new("firstName", c => c.FirstName, (c, v) => c with { FirstName = v }),
        new("title", c => c.Title, (c, v) => c with { Title = v }),
        new("location", c => c.Location, (c, v) => c with { Location = v }),
        new("levelId", c => c.LevelId, (c, v) => c with { LevelId = v }),
        new("workingType", c => c.WorkingType, (c, v) => c with { WorkingType = v }),
        new("pronounsKey", c => c.PronounsKey, (c, v) => c with { PronounsKey = v }),
        new("careerPathKey", c => c.CareerPathKey, (c, v) => c with { CareerPathKey = v }),
        new("reportsTo", c => c.ReportsTo, (c, v) => c with { ReportsTo = v }),
        new("dayStartTime", c => c.DayStartTime?.ToString(TimeFormat, CultureInfo.InvariantCulture),
            (c, v) => c with { DayStartTime = ParseTime(v) }),
        new("levelStartDate", c => Format(c.LevelStartDate), (c, v) => c with { LevelStartDate = ParseDate(v) }),
        new("teamMemberSince", c => Format(c.TeamMemberSince), (c, v) => c with { TeamMemberSince = ParseDate(v) }),
        new("exitDate", c => Format(c.ExitDate), (c, v) => c with { ExitDate = ParseDate(v) }),
    ];

    private static readonly Dictionary<string, ContactField> ByName = All.ToDictionary(field => field.Name);

    public static ContactField? Named(string name) => ByName.GetValueOrDefault(name);

    private static string? Format(DateOnly? date) => date?.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static TimeOnly ParseTime(string text) =>
        TimeOnly.TryParseExact(text, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            && time.ToString(TimeFormat, CultureInfo.InvariantCulture) == text.PadLeft(TimeFormat.Length, '0') // .NET reads 24:00 as 00:00
            ? time
            : throw new InvalidDataException($"'{text}' isn't a time written HH:mm.");

    private static DateOnly ParseDate(string text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new InvalidDataException($"'{text}' isn't a date written yyyy-MM-dd.");
}
