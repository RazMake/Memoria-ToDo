namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// The shared fake clock. Time moves only when a test calls <see cref="Advance"/>, which fires every timer that
/// comes due on the way, in order, each at its own due time, on the calling thread. A timer with a zero due time
/// fires at once, as a real one does.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly object gate = new();
    private readonly List<FakeTimer> timers = [];
    private readonly List<(int Count, TaskCompletionSource Done)> timerWaiters = [];
    private readonly DateTimeOffset start;
    private DateTimeOffset now;
    private TimeSpan slept;

    /// <summary>Starts the clock at <paramref name="start"/>, or at 2026-01-01 UTC.</summary>
    public FakeTimeProvider(DateTimeOffset? start = null)
    {
        this.start = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        now = this.start;
    }

    /// <summary>How many timers are waiting for a due time.</summary>
    public int ActiveTimerCount
    {
        get { lock (gate) { return timers.Count(t => t.IsActive); } }
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (gate) { return now; }
    }

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (gate) { return (now - start - slept).Ticks; }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        lock (gate) { timers.Add(timer); }
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves time forward and fires the timers that come due.</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        DateTimeOffset target;
        lock (gate) { target = now + by; }
        RunDue(target);
    }

    /// <summary>
    /// Models the PC sleeping: the wall clock (<see cref="GetUtcNow"/>) jumps forward, but no timer fires and every
    /// timer keeps the same time left, because on Windows .NET timers and <see cref="GetTimestamp"/> measure awake
    /// time only. <see cref="GetTimestamp"/> does not move, so code that measures elapsed time with it, rather than
    /// with the wall clock, doesn't see the sleep.
    /// </summary>
    public void Sleep(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        lock (gate)
        {
            now += by;
            slept += by;
            foreach (var timer in timers.Where(t => t.IsActive))
            {
                timer.Postpone(by);
            }
        }
    }

    /// <summary>Completes once at least one timer is waiting for a due time. Use it before <see cref="Advance"/> when the code under test arms its timer in the background.</summary>
    public Task WaitForActiveTimerAsync()
    {
        TaskCompletionSource done;
        lock (gate)
        {
            if (timers.Any(t => t.IsActive))
            {
                return Task.CompletedTask;
            }

            done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            timerWaiters.Add((1, done));
        }

        return done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private void RunDue(DateTimeOffset target)
    {
        while (true)
        {
            FakeTimer? next;
            lock (gate)
            {
                next = timers
                    .Where(t => t.IsActive && t.Due <= target)
                    .OrderBy(t => t.Due)
                    .FirstOrDefault();
                if (next is null)
                {
                    if (target > now)
                    {
                        now = target;
                    }

                    return;
                }

                if (next.Due > now)
                {
                    now = next.Due;
                }

                next.Rearm(now);
            }

            next.Fire();
        }
    }

    private void TimersChanged()
    {
        List<TaskCompletionSource> ready = [];
        lock (gate)
        {
            if (timers.Any(t => t.IsActive))
            {
                ready.AddRange(timerWaiters.Select(w => w.Done));
                timerWaiters.Clear();
            }
        }

        foreach (var done in ready)
        {
            done.TrySetResult();
        }
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        public bool IsActive { get; private set; }

        public DateTimeOffset Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            var fireNow = false;
            lock (owner.gate)
            {
                if (!owner.timers.Contains(this))
                {
                    return false;
                }

                period = newPeriod;
                IsActive = dueTime != Timeout.InfiniteTimeSpan;
                if (IsActive)
                {
                    Due = owner.now + dueTime;
                    fireNow = dueTime == TimeSpan.Zero;
                }
            }

            owner.TimersChanged();
            if (fireNow)
            {
                owner.RunDue(owner.GetUtcNow());
            }

            return true;
        }

        // Called under the owner's lock when the timer fires: a periodic timer is due again one period later.
        public void Rearm(DateTimeOffset firedAt)
        {
            if (period > TimeSpan.Zero)
            {
                Due = firedAt + period;
            }
            else
            {
                IsActive = false;
            }
        }

        // Called under the owner's lock while the PC sleeps: the time left stays the same.
        public void Postpone(TimeSpan by) => Due += by;

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner.gate)
            {
                IsActive = false;
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
