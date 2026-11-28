using System.Globalization;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The time range the From and To boxes hold, in the PC's local time. An empty box sets no bound; each bound includes its own
/// minute. A box that isn't a date and time, or an end (the minute after To) that as a moment isn't later than From, is invalid, and an invalid range has no bounds.
/// </summary>
internal readonly struct TimeRange
{
    private const string Layout = "yyyy-MM-dd HH:mm";

    private TimeRange(bool fromIsInvalid, bool toIsInvalid, DateTimeOffset? start, DateTimeOffset? endExclusive)
    {
        FromIsInvalid = fromIsInvalid;
        ToIsInvalid = toIsInvalid;
        Start = start;
        EndExclusive = endExclusive;
    }

    public bool FromIsInvalid { get; }

    public bool ToIsInvalid { get; }

    /// <summary>The moment the range starts, or null for no start.</summary>
    public DateTimeOffset? Start { get; }

    /// <summary>The moment the range ends, which it leaves out: the minute after the To box's. Null for no end.</summary>
    public DateTimeOffset? EndExclusive { get; }

    /// <summary>The text a box shows for this local time.</summary>
    public static string Format(DateTime local) => local.ToString(Layout, CultureInfo.InvariantCulture);

    /// <summary>The range the two boxes' texts hold, in the zone.</summary>
    public static TimeRange Of(string from, string to, TimeZoneInfo zone)
    {
        var fromIsInvalid = !TryParse(from, out var start);
        var toIsInvalid = !TryParse(to, out var end);
        if (fromIsInvalid || toIsInvalid)
        {
            return new TimeRange(fromIsInvalid, toIsInvalid, null, null);
        }

        // The calendar's last minute has no minute after it: a To there has no upper limit.
        var startMoment = Instant(start, zone);
        var endExclusive = end is { } last && last < DateTime.MaxValue.AddMinutes(-1) ? Instant(last.AddMinutes(1), zone) : null;

        // Checked as the moments the filter gets: a time the clock skips has no offset of its own, so the bounds can cross even
        // when the texts don't.
        if (endExclusive <= startMoment)
        {
            return new TimeRange(true, true, null, null);
        }

        return new TimeRange(false, false, startMoment, endExclusive);
    }

    // An empty text is a valid box with no bound.
    private static bool TryParse(string text, out DateTime? local)
    {
        local = null;
        if (text.Length == 0)
        {
            return true;
        }

        if (!DateTime.TryParseExact(text, Layout, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return false;
        }

        local = parsed;
        return true;
    }

    // A local time whose instant lies past the calendar's end is that end.
    private static DateTimeOffset? Instant(DateTime? local, TimeZoneInfo zone)
    {
        if (local is not { } value)
        {
            return null;
        }

        var offset = zone.GetUtcOffset(value);
        return value.Ticks - offset.Ticks > DateTimeOffset.MaxValue.Ticks
            ? DateTimeOffset.MaxValue
            : new DateTimeOffset(value, offset);
    }
}
