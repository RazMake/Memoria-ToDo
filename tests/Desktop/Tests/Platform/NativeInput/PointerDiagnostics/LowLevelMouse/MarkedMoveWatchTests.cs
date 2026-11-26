using System.Runtime.InteropServices;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class MarkedMoveWatchTests
{
    private const nint Marker = 0x5134;

    private sealed class Chain(Func<nint> next) : IHookChain
    {
        public int Calls { get; private set; }
        public nint CallNext(int code, nint message, nint data)
        {
            Calls++;
            return next();
        }
    }

    private sealed class Packet : IDisposable
    {
        public nint Address { get; } = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.LowLevelMouseHook>());
        public Packet(nint marker, uint flags = 1, int x = 137, int y = -29) =>
            Marshal.StructureToPtr(new Win32.LowLevelMouseHook
            {
                ExtraInfo = marker, Flags = flags, Point = new() { X = x, Y = y },
                MouseData = 0xDEADBEEF, Time = 0xABCDEF,
            }, Address, false);
        public void Dispose() => Marshal.FreeHGlobal(Address);
    }

    private sealed class FailingCompletionClock : TimeProvider
    {
        private int calls;
        public readonly Exception Failure = new InvalidOperationException("clock-failed");
        public override long GetTimestamp() => ++calls > 2 ? throw Failure : 0;
    }

    [Fact]
    public void Contains_a_completion_clock_failure_without_changing_the_chain_result()
    {
        var clock = new FailingCompletionClock();
        var chain = new Chain(() => 29);
        var watch = new MarkedMoveWatch(Marker, chain, clock);
        using var packet = new Packet(Marker);
        Assert.Equal((nint)29, watch.Observe(0, 0x200, packet.Address));
        Assert.Equal(1, chain.Calls);
        Assert.Same(clock.Failure, watch.Snapshot().Failure);
    }

    [Fact]
    public void The_lower_integrity_flag_alone_does_not_identify_an_injected_move()
    {
        var chain = new Chain(() => 7);
        var watch = new MarkedMoveWatch(Marker, chain, new FakeTimeProvider());
        using var packet = new Packet(Marker, flags: 2);
        Assert.Equal((nint)7, watch.Observe(0, 0x200, packet.Address));
        Assert.Equal(0, watch.Snapshot().Count);
        Assert.Empty(watch.Snapshot().Samples);
        Assert.Equal(1, chain.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Reports_its_marked_move_and_preserves_the_downstream_result(int result)
    {
        var clock = new FakeTimeProvider();
        var chain = new Chain(() => { clock.Advance(TimeSpan.FromMilliseconds(7)); return result; });
        var watch = new MarkedMoveWatch(Marker, chain, clock);
        using var packet = new Packet(Marker, flags: 0xFFFF);
        clock.Advance(TimeSpan.FromMilliseconds(5));

        Assert.Equal((nint)result, watch.Observe(0, 0x200, packet.Address));

        var report = watch.Snapshot();
        var move = Assert.Single(report.Samples);
        Assert.Equal(1, report.Count);
        Assert.Equal(1, chain.Calls);
        Assert.Null(report.Failure);
        Assert.Equal(TimeSpan.FromMilliseconds(5), move.Entered);
        Assert.Equal(TimeSpan.FromMilliseconds(12), move.Completed);
        Assert.Equal((nint)result, move.Result);
        Assert.Equal((137, -29), (move.Point.X, move.Point.Y));
        Assert.Equal(3u, move.InjectionFlags);
        Assert.DoesNotContain("DEADBEEF", report.Describe());
        Assert.DoesNotContain("ABCDEF", report.Describe());
    }

    [Theory]
    [InlineData(0, 0x200, 0, 1)]
    [InlineData(0, 0x200, 0x5133, 1)]
    [InlineData(0, 0x200, 0x5135, 1)]
    [InlineData(0, 0x200, 0x5134, 0)]
    [InlineData(-1, 0x200, 0x5134, 1)]
    [InlineData(3, 0x200, 0x5134, 1)]
    [InlineData(0, 0x201, 0x5134, 1)]
    public void Passes_other_input_on_without_retaining_any_of_it(int code, int message, int marker, int flags)
    {
        var chain = new Chain(() => 9);
        var watch = new MarkedMoveWatch(Marker, chain, new FakeTimeProvider());
        using var packet = new Packet(marker, (uint)flags, 9371, 9283);
        var before = watch.Snapshot();
        var address = code != 0 || message != 0x200 ? 0 : packet.Address;

        Assert.Equal((nint)9, watch.Observe(code, message, address));

        var after = watch.Snapshot();
        Assert.Equal(1, chain.Calls);
        Assert.Equal(0, after.Count);
        Assert.Empty(after.Samples);
        Assert.Null(after.Failure);
        Assert.Equal(before.Describe(), after.Describe());
    }

    [Fact]
    public async Task Shows_an_entered_call_as_pending_until_its_downstream_chain_returns()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var clock = new FakeTimeProvider();
        var chain = new Chain(() =>
        {
            entered.Set();
            if (!release.Wait(TestWait.ForSignal)) throw new TimeoutException("Test did not release its chain.");
            return 17;
        });
        var watch = new MarkedMoveWatch(Marker, chain, clock);
        using var packet = new Packet(Marker);
        var call = Task.Run(() => watch.Observe(0, 0x200, packet.Address));
        MarkedMoveReport pending;
        try
        {
            Assert.True(entered.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
            pending = watch.Snapshot();
            var move = Assert.Single(pending.Samples);
            Assert.Equal(1, pending.Count);
            Assert.Null(move.Completed);
            Assert.Null(move.Result);
            Assert.Contains("pending", pending.Describe(), StringComparison.OrdinalIgnoreCase);
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }
        finally
        {
            release.Set();
            await call.WaitAsync(TestWait.ForSignal, CancellationToken.None);
        }
        var completed = Assert.Single(watch.Snapshot().Samples);
        Assert.Equal((nint)17, completed.Result);
        Assert.Equal(TimeSpan.FromMilliseconds(10), completed.Completed);
        Assert.Null(Assert.Single(pending.Samples).Result);
    }

    [Fact]
    public void Keeps_the_first_four_matching_calls_and_counts_further_matches()
    {
        var watch = new MarkedMoveWatch(Marker, new Chain(() => 0), new FakeTimeProvider());
        for (var i = 0; i < 7; i++)
        {
            using var packet = new Packet(Marker, x: i);
            watch.Observe(0, 0x200, packet.Address);
        }
        var report = watch.Snapshot();
        Assert.Equal(7, report.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, report.Samples.Select(s => s.Point.X));
    }

    [Fact]
    public void Contains_downstream_exceptions_without_blocking_input_and_keeps_the_first_failure()
    {
        var failure = new InvalidOperationException("broken-chain");
        var chain = new Chain(() => throw failure);
        var watch = new MarkedMoveWatch(Marker, chain, new FakeTimeProvider());
        using var packet = new Packet(Marker);
        Assert.Equal((nint)0, watch.Observe(0, 0x200, packet.Address));
        Assert.Same(failure, watch.Snapshot().Failure);
        Assert.Null(Assert.Single(watch.Snapshot().Samples).Result);
        Assert.Equal((nint)0, watch.Observe(-1, 0x200, 0));
        Assert.Equal(2, chain.Calls);
        Assert.Same(failure, watch.Snapshot().Failure);
    }

    [Fact]
    public void Contains_an_unreadable_matching_payload_and_still_calls_the_next_hook()
    {
        var chain = new Chain(() => 23);
        var watch = new MarkedMoveWatch(Marker, chain, new FakeTimeProvider());
        Assert.Equal((nint)23, watch.Observe(0, 0x200, 0));
        Assert.Equal(1, chain.Calls);
        Assert.NotNull(watch.Snapshot().Failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Refuses_a_marker_that_cannot_identify_our_injected_move(int marker) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkedMoveWatch(marker, new Chain(() => 0), new FakeTimeProvider()));

    [Fact]
    public void Refuses_missing_dependencies_before_observing_any_input()
    {
        Assert.Throws<ArgumentNullException>(() => new MarkedMoveWatch(Marker, null!, new FakeTimeProvider()));
        Assert.Throws<ArgumentNullException>(() => new MarkedMoveWatch(Marker, new Chain(() => 0), null!));
    }
}
