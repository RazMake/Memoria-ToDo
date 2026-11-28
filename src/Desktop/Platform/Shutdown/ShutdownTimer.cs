using System.Diagnostics;

namespace Memoria.ToDo.Desktop.Platform;

internal sealed class ShutdownTimer : IShutdownTimer
{
    public void Schedule(TimeSpan delay, Action callback)
    {
        var started = Stopwatch.GetTimestamp();
        new Thread(() =>
        {
            var remaining = delay - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                using var wait = new ManualResetEvent(false);
                wait.WaitOne(remaining);
            }
            callback();
        }) { IsBackground = true, Name = "Shutdown watchdog" }.Start();
    }
}
