namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// Checks for app updates at start and then every 24 hours, counted by the clock from when the previous check
/// began, so time asleep counts. Timers don't run while the PC sleeps, so it looks at the clock at least once an
/// hour while awake; a check that fell due during sleep runs at most an hour after the PC wakes. A failed check is dropped without a trace, because the design says it shows nothing and the next check retries.
/// </summary>
internal sealed class UpdateSchedule(IAppUpdates updates, TimeProvider time)
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxLook = TimeSpan.FromHours(1);

    private CancellationTokenSource? stop;

    /// <summary>Begins the checks without waiting for the first one.</summary>
    public void Start()
    {
        stop = new CancellationTokenSource();
        _ = RunAsync(stop.Token);
    }

    /// <summary>Cancels a running check and ends the schedule.</summary>
    public void Stop()
    {
        stop?.Cancel();
        stop?.Dispose();
        stop = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var began = time.GetUtcNow();
                await CheckAsync(cancellationToken);

                TimeSpan remaining;
                while ((remaining = Interval - (time.GetUtcNow() - began)) > TimeSpan.Zero)
                {
                    await Task.Delay(remaining < MaxLook ? remaining : MaxLook, time, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop ended the schedule.
        }
    }

    // Only a check's own failure is dropped; a cancelled check lets the cancellation reach RunAsync.
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await updates.DownloadNewerVersionAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
