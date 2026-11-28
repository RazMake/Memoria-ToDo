namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed class FakeShutdownTimer(TimeProvider time) : IShutdownTimer
{
    private readonly List<ITimer> timers = [];

    public void Schedule(TimeSpan delay, Action callback) =>
        timers.Add(time.CreateTimer(_ =>
            new Thread(() => callback()) { IsBackground = true }.Start(),
            null, delay, Timeout.InfiniteTimeSpan));
}
