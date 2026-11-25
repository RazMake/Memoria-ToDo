using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class PointerMoveRecorderTests
{
    private const nint Own = 0x20;
    private const nint Owner = 0x10;
    private const nint Cover = 0x99;
    private const string Sentinel = "snapshot-sentinel";
    private static readonly Win32.Point Target = new() { X = 100, Y = 50 };
    private static readonly string Move = PointerTrace.Label(1, "away");

    private sealed class FakeReadings : IPointerReadings
    {
        public nint Hit = Own;
        public nint Foreground = Owner;
        public int PendingReads;
        public Action? OnSnapshot;

        public bool IsOnScreen(Win32.Point point) => true;
        public nint WindowAt(Win32.Point point) => Hit;
        public nint ForegroundWindow() => Foreground;
        public bool IsChildOf(nint window, nint parent) => false;
        public uint PendingInput() { PendingReads++; return 0x40; }

        public string Snapshot(Win32.Point point, nint targetWindow)
        {
            OnSnapshot?.Invoke();
            return Sentinel;
        }
    }

    private sealed class Scenario
    {
        public readonly FakeReadings Readings = new();
        public readonly PointerTrace Trace = new();
        public int Sent;
        public int Released;
        public int Stops;
        public Action? OnStop;
        public readonly PointerMoveRecorder Recorder;

        public Scenario() => Recorder = new(Trace, Move, Own, Owner, Readings, () => { Stops++; OnStop?.Invoke(); });

        public void Run(
            Action? acquire = null, Action? send = null, Action? wait = null,
            PointerOutcome? outcome = null, Action<FailureList>? release = null) =>
            Recorder.Run(Target, new PointerMoveSteps(
                acquire ?? (() => { }),
                () => { Sent++; send?.Invoke(); },
                wait ?? (() => { }),
                () => outcome ?? new PointerOutcome(PointerResult.Observed),
                failures => { Released++; release?.Invoke(failures); }));
    }

    private static IEnumerable<Exception> Leaves(Exception failure) =>
        failure is AggregateException all ? all.InnerExceptions.SelectMany(Leaves) : [failure];

    private static Exception Primary(Exception failure) => Leaves(failure).First();

    [Fact]
    public void Ends_a_move_whose_message_was_observed_without_an_error_or_a_queue_reading()
    {
        var scenario = new Scenario();

        scenario.Run();

        Assert.Equal(1, scenario.Sent);
        Assert.Equal(1, scenario.Released);
        Assert.Equal(0, scenario.Readings.PendingReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fails_a_move_that_was_not_observed_with_its_trace_and_reads_the_queue_only_at_a_timeout(bool timedOut)
    {
        var scenario = new Scenario();
        var result = timedOut ? PointerResult.TimedOut : PointerResult.Misplaced;
        var error = timedOut ? typeof(TimeoutException) : typeof(InvalidOperationException);

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(outcome: new PointerOutcome(result, "outcome-text", "what-text")));

        Assert.IsType(error, failure);
        Assert.Contains("outcome-text", failure.Message);
        Assert.Contains(Move, failure.Message);
        Assert.Contains(Sentinel, failure.Message);
        Assert.Equal(timedOut ? 1 : 0, scenario.Readings.PendingReads);
        Assert.Equal(1, scenario.Released);
    }

    [Fact]
    public void Refuses_the_point_by_the_state_read_at_the_send_before_sending_anything()
    {
        var scenario = new Scenario();
        scenario.Readings.OnSnapshot = () => scenario.Readings.Hit = Cover;

        var failure = Assert.Throws<InvalidOperationException>(() => scenario.Run());

        Assert.Equal(0, scenario.Sent);
        Assert.Equal(1, scenario.Released);
        Assert.Contains(Move, failure.Message);
        Assert.Contains(PointerTrace.Refused, failure.Message);
        Assert.Equal(0, scenario.Readings.PendingReads);
    }

    [Fact]
    public void Reports_the_first_callback_failure_first_stops_the_wait_once_and_runs_no_later_callback()
    {
        var scenario = new Scenario();
        var first = new InvalidOperationException("first-callback-failure");
        var laterRan = false;

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(wait: () =>
        {
            scenario.Recorder.Guard(() => throw first);
            scenario.Recorder.Guard(() => laterRan = true);
            scenario.Recorder.Guard(() => throw new InvalidOperationException("second-callback-failure"));
        }));

        Assert.Same(first, Primary(failure));
        Assert.False(laterRan);
        Assert.Equal(1, scenario.Stops);
        Assert.DoesNotContain(Leaves(failure), leaf => leaf.Message.Contains("second-callback-failure"));
        Assert.Contains(Leaves(failure), leaf => leaf.Message.Contains(Move));
        Assert.Equal(1, scenario.Released);
    }

    [Theory]
    [InlineData("acquire")]
    [InlineData("snapshot")]
    [InlineData("send")]
    [InlineData("wait")]
    public void Releases_everything_and_reports_the_primary_failure_first_when_a_step_fails(string step)
    {
        var scenario = new Scenario();
        var primary = new InvalidOperationException("primary-failure");
        Action fail = () => throw primary;
        if (step == "snapshot")
        {
            scenario.Readings.OnSnapshot = fail;
        }

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(
            acquire: step == "acquire" ? fail : null,
            send: step == "send" ? fail : null,
            wait: step == "wait" ? fail : null,
            release: failures => failures.Add(new IOException("cleanup-failure"))));

        Assert.Same(primary, Primary(failure));
        Assert.Equal(1, scenario.Released);
        Assert.Contains(Leaves(failure), leaf => leaf.Message == "cleanup-failure");
        Assert.Contains(Leaves(failure), leaf => leaf.Message.Contains(Move));
        Assert.Equal(step is "acquire" or "snapshot" ? 0 : 1, scenario.Sent);
    }

    [Fact]
    public void Reports_every_cleanup_failure_after_a_timeout_that_stays_the_primary_failure()
    {
        var scenario = new Scenario();

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(
            outcome: new PointerOutcome(PointerResult.TimedOut, "timeout-text"),
            release: failures =>
            {
                failures.Add(new IOException("cleanup-one"));
                failures.Add(new IOException("cleanup-two"));
            }));

        var leaves = Leaves(failure).ToList();
        Assert.IsType<TimeoutException>(leaves[0]);
        Assert.Contains("timeout-text", leaves[0].Message);
        Assert.Contains(leaves.Skip(1), leaf => leaf.Message == "cleanup-one");
        Assert.Contains(leaves.Skip(1), leaf => leaf.Message == "cleanup-two");
        Assert.Equal(1, scenario.Readings.PendingReads);
    }

    [Fact]
    public void Fails_a_move_that_was_observed_when_only_its_cleanup_failed()
    {
        var scenario = new Scenario();

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(release: failures => failures.Add(new IOException("cleanup-failure"))));

        Assert.Contains(Leaves(failure), leaf => leaf.Message == "cleanup-failure");
    }

    [Fact]
    public void Records_only_the_lifecycle_messages_it_lists_and_without_data_it_does_not_need()
    {
        var scenario = new Scenario();
        var unknownWindow = (nint)0xABCDE;

        scenario.Recorder.SentMessage(new Win32.SentMessage { Id = 0x0006, Window = unknownWindow, WParam = 7777 });
        scenario.Recorder.SentMessage(new Win32.SentMessage { Id = 0x0003, Window = unknownWindow, WParam = 8888 });
        scenario.Recorder.SentMessage(new Win32.SentMessage { Id = 0x0102, Window = unknownWindow, WParam = 9999 });
        var text = scenario.Trace.Describe();

        Assert.Contains("WM_ACTIVATE", text);
        Assert.Contains("7777", text);
        Assert.Contains("WM_MOVE", text);
        Assert.DoesNotContain("8888", text);
        Assert.DoesNotContain("9999", text);
        Assert.DoesNotContain("0x0102", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, scenario.Readings.PendingReads);
    }

    [Fact]
    public void Keeps_the_timeout_first_with_the_reading_error_and_the_cleanup_when_the_failure_reading_throws()
    {
        var scenario = new Scenario();
        var snapshots = 0;
        scenario.Readings.OnSnapshot = () =>
        {
            if (++snapshots == 2)
            {
                throw new InvalidOperationException("reading-error");
            }
        };

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(
            outcome: new PointerOutcome(PointerResult.TimedOut, "timeout-text"),
            release: failures => failures.Add(new IOException("cleanup-failure"))));

        var leaves = Leaves(failure).ToList();
        Assert.IsType<TimeoutException>(leaves[0]);
        Assert.Contains("timeout-text", leaves[0].Message);
        Assert.Contains(leaves.Skip(1), leaf => leaf.Message == "reading-error");
        Assert.Contains(leaves.Skip(1), leaf => leaf.Message == "cleanup-failure");
        Assert.Equal(1, scenario.Readings.PendingReads);
        Assert.Equal(1, scenario.Released);
    }

    [Fact]
    public void Keeps_the_callback_failure_first_and_does_not_let_a_failing_stop_escape_the_callback()
    {
        var scenario = new Scenario();
        scenario.OnStop = () => throw new InvalidOperationException("stop-failure");
        var first = new InvalidOperationException("first-callback-failure");
        Exception? escaped = null;

        var failure = Assert.ThrowsAny<Exception>(() => scenario.Run(
            wait: () => escaped = Record.Exception(() => scenario.Recorder.Guard(() => throw first))));

        var leaves = Leaves(failure).ToList();
        Assert.Null(escaped);
        Assert.Same(first, leaves[0]);
        Assert.Contains(leaves.Skip(1), leaf => leaf.Message == "stop-failure");
        Assert.Equal(1, scenario.Released);
    }
}
