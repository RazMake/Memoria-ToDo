using System.Globalization;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The time range the From and To boxes hold, in the PC's local time. An empty box sets no bound; each bound includes its own
/// minute. A box that isn't a date and time, or a From later than To, is invalid, and an invalid range holds every time.
/// </summary>
internal readonly struct TimeRange
{
    private const string Layout = "yyyy-MM-dd HH:mm";

    private readonly DateTimeOffset? _start;
    private readonly DateTimeOffset? _endExclusive;

    private TimeRange(bool fromIsInvalid, bool toIsInvalid, DateTimeOffset? start, DateTimeOffset? endExclusive)
    {
        FromIsInvalid = fromIsInvalid;
        ToIsInvalid = toIsInvalid;
        _start = start;
        _endExclusive = endExclusive;
    }

    public bool FromIsInvalid { get; }

    public bool ToIsInvalid { get; }

    /// <summary>The text a box shows for this local time.</summary>
    public static string Format(DateTime local) => local.ToString(Layout, CultureInfo.InvariantCulture);

    /// <summary>The range the two boxes' texts hold, in the zone.</summary>
    public static TimeRange Of(string from, string to, TimeZoneInfo zone)
    {
        var fromIsInvalid = !TryParse(from, out var start);
        var toIsInvalid = !TryParse(to, out var end);
        if (!fromIsInvalid && !toIsInvalid && start > end)
        {
            return new TimeRange(true, true, null, null);
        }

        if (fromIsInvalid || toIsInvalid)
        {
            return new TimeRange(fromIsInvalid, toIsInvalid, null, null);
        }

        // The calendar's last minute has no minute after it: a To there has no upper limit.
        var endExclusive = end is { } last && last < DateTime.MaxValue.AddMinutes(-1) ? Instant(last.AddMinutes(1), zone) : null;
        return new TimeRange(false, false, Instant(start, zone), endExclusive);
    }

    /// <summary>Whether the time is inside the range. Every time is, while a box is invalid.</summary>
    public bool Contains(DateTimeOffset time) => (_start is not { } start || time >= start) && (_endExclusive is not { } end || time < end);

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
