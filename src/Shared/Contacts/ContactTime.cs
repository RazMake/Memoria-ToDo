using System.Globalization;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>
/// A contact's time of day as text, the way Contacts reads, writes, and searches dayStartTime.
/// The Contact list's time field reads the same forms.
/// </summary>
public static class ContactTime
{
    /// <summary>The time written HH:mm, such as "09:30".</summary>
    public static string TextOf(TimeOnly time) => time.ToString(WrittenFormat, CultureInfo.InvariantCulture);

    /// <summary>Reads H:mm or HH:mm, from 0:00 to 23:59. False for anything else, null and empty included.</summary>
    public static bool TryRead(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
            && TextOf(time) == text!.PadLeft(WrittenFormat.Length, '0'); // .NET reads 24:00 as 00:00

    private const string WrittenFormat = "HH:mm";
}
