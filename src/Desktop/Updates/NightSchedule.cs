namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// Waits for 3:00 local time. A timed wait doesn't count time asleep, so every wait looks at the clock at least once an hour:
/// a night that fell due during sleep ends within an hour after the PC wakes.
/// </summary>
internal sealed class NightSchedule(TimeProvider time)
{
    private static readonly TimeSpan NightOfDay = TimeSpan.FromHours(3);
    private static readonly TimeSpan MaxLook = TimeSpan.FromHours(1);

    /// <summary>Ends at the first 3:00 local time after now, or within an hour after the PC wakes when it slept through it. A change of time zone moves it to 3:00 in the new zone.</summary>
    public async Task WaitForNextNightAsync(CancellationToken cancellationToken)
    {
        var start = time.GetUtcNow();
        TimeSpan remaining;
        while ((remaining = NextNightAfter(start) - time.GetUtcNow()) > TimeSpan.Zero)
        {
            await Task.Delay(remaining < MaxLook ? remaining : MaxLook, time, cancellationToken);
        }
    }

    /// <summary>Ends once the condition holds, which it looks at now and then once an hour.</summary>
    public async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(MaxLook, time, cancellationToken);
        }
    }

    // The first 3:00 after the start in the zone the PC is in now, so that a change of time zone moves it with the zone.
    private DateTimeOffset NextNightAfter(DateTimeOffset start)
    {
        var zone = time.LocalTimeZone;
        var local = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var night = local.Date + NightOfDay;
        if (night <= local)
        {
            night = night.AddDays(1);
        }

        return new DateTimeOffset(night, zone.GetUtcOffset(night));
    }
}
