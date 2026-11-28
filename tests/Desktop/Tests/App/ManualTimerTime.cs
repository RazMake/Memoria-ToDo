namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A clock whose timers never fire by themselves: the test moves the clock with <see cref="Move"/> and calls a timer's callback
/// by hand with <see cref="Fire"/>, at any moment, also when the timer isn't due, as a callback a real timer had already queued
/// can run late.
/// </summary>
internal sealed class ManualTimerTime : TimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private ManualTimer? timer;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
            return now;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().Ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (timer is not null)
            throw new InvalidOperationException("This clock makes one timer.");
        timer = new ManualTimer(callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock forward without firing anything.</summary>
    public void Move(TimeSpan by)
    {
        lock (gate)
            now += by;
    }

    /// <summary>Calls the timer's callback now, on the calling thread.</summary>
    public void Fire() => timer!.Fire();

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Fire() => callback(state);

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
