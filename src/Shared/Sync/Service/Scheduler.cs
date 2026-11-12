namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Decides when the next automatic pass starts, with one timer: a 5 second debounce after edits, a retry backoff
/// after a failure, or the periodic interval, in that order of priority.
/// </summary>
internal sealed class Scheduler(TimeProvider time, TimeSpan? periodicInterval, Action requestPass) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(15);

    private readonly object gate = new();
    private ITimer? timer;
    private bool started;
    private bool stopped;
    private bool passRunning;
    private bool dirty;
    private int failures;

    /// <summary>Starts the automatic triggers and requests a pass; a pass already running gets one follow-up.</summary>
    public void Start()
    {
        lock (gate)
        {
            if (stopped)
            {
                return;
            }

            started = true;
        }

        requestPass();
    }

    /// <summary>An edit was saved: wait the full debounce again, and run a follow-up if a pass is running.</summary>
    public void LocalSaved()
    {
        lock (gate)
        {
            // Before Start nothing is automatic, except the follow-up for an edit made during a manual pass.
            // An idle edit is left for the initial pass to pick up.
            if (stopped || (!started && !passRunning))
            {
                return;
            }

            dirty = true;
            Arm(Debounce);
        }
    }

    public void PassStarting()
    {
        lock (gate)
        {
            passRunning = true;
            dirty = false;
            Disarm();
        }
    }

    public void PassFinished(bool succeeded)
    {
        lock (gate)
        {
            passRunning = false;
            if (stopped)
            {
                return;
            }

            if (started)
            {
                failures = succeeded ? 0 : failures + 1;
            }

            if (dirty)
            {
                Arm(Debounce);
                return;
            }

            // Retries and the periodic interval are automatic, so they wait for Start.
            if (!started)
            {
                return;
            }

            if (!succeeded)
            {
                Arm(RetryDelay(failures));
            }
            else if (periodicInterval is { } interval)
            {
                Arm(interval);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            stopped = true;
            timer?.Dispose();
            timer = null;
        }
    }

    private static TimeSpan RetryDelay(int failureCount) =>
        failureCount > 5 ? LongestRetry : TimeSpan.FromTicks(Math.Min(FirstRetry.Ticks << (failureCount - 1), LongestRetry.Ticks));

    private void Arm(TimeSpan delay)
    {
        if (timer is null)
        {
            timer = time.CreateTimer(OnTimer, null, delay, Timeout.InfiniteTimeSpan);
        }
        else
        {
            timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Disarm() => timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private void OnTimer(object? state)
    {
        lock (gate)
        {
            // A running pass picks the edit up as a follow-up when it ends.
            if (stopped || passRunning)
            {
                return;
            }
        }

        requestPass();
    }
}
