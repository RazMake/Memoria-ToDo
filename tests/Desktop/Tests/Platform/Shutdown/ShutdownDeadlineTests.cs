using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class ShutdownDeadlineTests
{
    [Fact]
    public void No_exit_is_requested_before_shutdown_begins()
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit();
        _ = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process, new InMemoryLogSink().CreateLogger());

        time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(0, process.RequestCount);
        Assert.Equal(0, process.TerminationCount);
    }

    [Fact]
    public async Task The_first_request_allows_the_grace_period_then_logs_and_requests_a_failed_exit()
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit();
        var log = new InMemoryLogSink();
        var deadline = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process, log.CreateLogger());
        deadline.Begin();

        time.Advance(TimeSpan.FromSeconds(39));
        Assert.Equal(0, process.RequestCount);
        Assert.DoesNotContain(log.Events, entry => entry.Level >= LogEventLevel.Warning);
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, await process.Requested);
        Assert.Contains(log.Events, entry => entry.Level == LogEventLevel.Fatal);
        Assert.Equal(0, process.TerminationCount);
    }

    [Fact]
    public async Task An_exit_request_gets_three_more_seconds_before_forced_termination()
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit();
        var deadline = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process, new InMemoryLogSink().CreateLogger());
        deadline.Begin();
        time.Advance(TimeSpan.FromSeconds(40));
        await process.Requested;

        time.Advance(TimeSpan.FromMilliseconds(2999));
        Assert.Equal(0, process.TerminationCount);
        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, await process.Terminated);
    }

    [Fact]
    public async Task Repeated_requests_from_other_threads_do_not_extend_or_repeat_shutdown()
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit();
        var deadline = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process, new InMemoryLogSink().CreateLogger());
        await Task.Run(deadline.Begin, TestContext.Current.CancellationToken).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(39));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(deadline.Begin, TestContext.Current.CancellationToken)))
            .WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await process.Requested);
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(1, await process.Terminated);
        Assert.Equal(1, process.RequestCount);
        Assert.Equal(1, process.TerminationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_blocked_exit_request_or_logger_cannot_block_forced_termination(bool blockLogger)
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Block()
        {
            entered.TrySetResult();
            Assert.True(release.Wait(TestWait.ForSignal), "The test did not release its blocked callback.");
        }
        var sink = new CallbackSink(Block);
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        if (!blockLogger)
        {
            process.OnRequest = Block;
        }
        var deadline = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process,
            blockLogger ? logger : new InMemoryLogSink().CreateLogger());
        try
        {
            deadline.Begin();
            time.Advance(TimeSpan.FromSeconds(40));
            await entered.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(1, await process.Terminated);
        }
        finally
        {
            release.Set();
        }
        await process.Finished;
    }

    [Fact]
    public async Task A_failed_exit_request_does_not_cancel_forced_termination()
    {
        var time = new FakeTimeProvider();
        var process = new FakeProcessExit { OnRequest = () => throw new InvalidOperationException("exit failed") };
        var log = new InMemoryLogSink();
        var deadline = new ShutdownDeadline(TimeSpan.FromSeconds(40), new FakeShutdownTimer(time), process, log.CreateLogger());
        deadline.Begin();
        time.Advance(TimeSpan.FromSeconds(40));
        await process.Finished;

        time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(1, await process.Terminated);
        Assert.Contains(log.Events, entry => entry.Level == LogEventLevel.Fatal);
    }

    private sealed class CallbackSink(Action callback) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => callback();
    }
}
