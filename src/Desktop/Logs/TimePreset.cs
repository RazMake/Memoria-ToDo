namespace Memoria.ToDo.Desktop.Logs;

/// <summary>The choices of the time range list. Custom is what the list shows once the user edits a box.</summary>
internal enum TimePreset
{
    LastHour,
    Today,
    Last24Hours,
    Last7Days,
    All,
    Custom,
}

/// <summary>What each preset says to the user, and the From box's text it fills.</summary>
internal static class TimePresets
{
    public static string Label(TimePreset preset) => preset switch
    {
        TimePreset.LastHour => "Last hour",
        TimePreset.Today => "Today",
        TimePreset.Last24Hours => "Last 24 hours",
        TimePreset.Last7Days => "Last 7 days",
        TimePreset.All => "All",
        TimePreset.Custom => "Custom",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown time preset."),
    };

    /// <summary>The text of the From box for the preset at this moment in the zone, or an empty text for All, which has no start, and for Custom.</summary>
    public static string Start(TimePreset preset, DateTimeOffset now, TimeZoneInfo zone)
    {
        DateTime Local(TimeSpan ago) => TimeZoneInfo.ConvertTime(now - ago, zone).DateTime;
        return preset switch
        {
            TimePreset.LastHour => TimeRange.Format(Local(TimeSpan.FromHours(1))),
            TimePreset.Last24Hours => TimeRange.Format(Local(TimeSpan.FromHours(24))),
            TimePreset.Last7Days => TimeRange.Format(Local(TimeSpan.FromDays(7))),
            TimePreset.Today => TimeRange.Format(Local(TimeSpan.Zero).Date),
            _ => "",
        };
    }
}
