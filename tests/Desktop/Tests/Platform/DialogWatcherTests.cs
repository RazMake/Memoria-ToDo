using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class DialogWatcherTests
{
    private sealed class FailedThread : IMessageLoopThread
    {
        public readonly Exception StartupFailure = new TimeoutException("startup-failed");
        public readonly Exception CleanupFailure = new InvalidOperationException("late-cleanup-failed");
        public bool Stopped;
        public void Start() => throw StartupFailure;
        public void Stop(FailureList failures)
        {
            Stopped = true;
            failures.Add(CleanupFailure);
        }
    }

    [Fact]
    public void Failed_construction_stops_the_worker_and_preserves_startup_then_cleanup_failures()
    {
        var thread = new FailedThread();
        var failure = Assert.ThrowsAny<Exception>(() => new DialogWatcher(_ => thread));
        Assert.True(thread.Stopped);
        var errors = Assert.IsType<AggregateException>(failure).InnerExceptions;
        Assert.Same(thread.StartupFailure, errors[0].InnerException);
        Assert.Same(thread.CleanupFailure, errors[1]);
    }
}
