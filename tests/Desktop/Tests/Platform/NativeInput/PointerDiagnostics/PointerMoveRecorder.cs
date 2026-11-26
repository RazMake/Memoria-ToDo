using System.Runtime.ExceptionServices;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Records one move and keeps callback failures from crossing into native code.</summary>
internal sealed class PointerMoveRecorder
{
    private const string Failed = "failed";
    private const string LowLevelFact = "low-level mouse hook";
    private static readonly Dictionary<uint, (string Name, bool ShowsWParam)> Lifecycle = new()
    {
        [0x0002] = ("WM_DESTROY", false),
        [0x0003] = ("WM_MOVE", false),
        [0x0005] = ("WM_SIZE", false),
        [0x0006] = ("WM_ACTIVATE", true),
        [0x0018] = ("WM_SHOWWINDOW", true),
        [0x001C] = ("WM_ACTIVATEAPP", true),
        [0x001F] = ("WM_CANCELMODE", false),
        [0x0047] = ("WM_WINDOWPOSCHANGED", false),
        [0x0086] = ("WM_NCACTIVATE", true),
        [0x0215] = ("WM_CAPTURECHANGED", false),
    };

    private readonly PointerTrace _trace;
    private readonly string _move;
    private readonly nint _owner;
    private ExceptionDispatchInfo? _callbackFailure;
    private int _otherMessages;
    private int _otherRemoved;
    private int _otherPeeked;
    private readonly Dictionary<string, Stage> _stages = [];
    private bool _reported;
    private readonly IPointerReadings _readings;
    private readonly Action _stopWait;
    private Exception? _stopFailure;
    private readonly Func<MarkedMoveReport>? _lowLevel;
    private bool _lowLevelRead;
    private Exception? _lowLevelFailure;

    public PointerMoveRecorder(PointerTrace trace, string move, nint targetWindow, nint owner,
        IPointerReadings readings, Action stopWait, Func<MarkedMoveReport>? lowLevel = null)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(stopWait);
        _trace = trace;
        _move = move;
        TargetWindow = targetWindow;
        _owner = owner;
        _readings = readings;
        _stopWait = stopWait;
        _lowLevel = lowLevel;
    }

    public nint TargetWindow { get; }

    public void Run(Win32.Point target, PointerMoveSteps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var failures = new FailureList();
        try
        {
            try
            {
                steps.Acquire();
                AtSend(target);
                Verify(target);
                steps.Send();
                steps.Wait();
                WaitEnded();
                ReadLowLevel();
                ThrowCallbackFailure();
                Conclude(failures, target, steps.Decide());
                AddLowLevelFailure(failures);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
                if (_stopFailure is not null)
                {
                    failures.Add(_stopFailure);
                }

                failures.Attempt(ReadLowLevel);
                failures.Attempt(() => ReportFailure(failures, target));
                AddLowLevelFailure(failures);
            }
        }
        finally
        {
            failures.Attempt(() => steps.Release(failures));
        }

        try
        {
            failures.ThrowIfAny();
        }
        catch (Exception failure) when (!_reported)
        {
            var reported = new FailureList();
            reported.Add(failure);
            reported.Attempt(() => ReportFailure(reported, target));
            reported.ThrowIfAny();
        }
    }

    public void Note(string what) => _trace.Record(_trace.Elapsed, _move, what);

    public void Guard(Action callback)
    {
        if (_callbackFailure is not null)
        {
            return;
        }

        try
        {
            callback();
        }
        catch (Exception failure)
        {
            _callbackFailure = ExceptionDispatchInfo.Capture(failure);
            try
            {
                _stopWait();
            }
            catch (Exception stopFailure)
            {
                _stopFailure = stopFailure;
            }
        }
    }

    private void ThrowCallbackFailure() => _callbackFailure?.Throw();

    private void ReadLowLevel()
    {
        if (_lowLevelRead)
        {
            return;
        }

        _lowLevelRead = true;
        if (_lowLevel is null)
        {
            return;
        }

        try
        {
            var report = _lowLevel();
            _trace.Keep(_move, LowLevelFact, report.Describe());
            _lowLevelFailure = report.Failure;
        }
        catch (Exception failure)
        {
            _lowLevelFailure = failure;
            _trace.Keep(_move, LowLevelFact, $"low-level mouse hook: the snapshot failed: {failure.Message}");
        }
    }

    private void AddLowLevelFailure(FailureList failures)
    {
        if (_lowLevelFailure is not null)
        {
            failures.Add(_lowLevelFailure);
        }
    }

    public void Verify(Win32.Point point)
    {
        var onScreen = _readings.IsOnScreen(point);
        var hit = onScreen ? _readings.WindowAt(point) : 0;
        try
        {
            PointerSendCheck.Verify(_move, point, onScreen, TargetWindow, hit, _readings.IsChildOf, _owner, _readings.ForegroundWindow());
        }
        catch (InvalidOperationException refusal)
        {
            var failures = new FailureList();
            Report(failures, point, PointerTrace.Refused, refusal.Message, readQueue: false,
                trace => new InvalidOperationException($"{refusal.Message}\n{trace}", refusal));
            failures.ThrowIfAny();
        }

        Note($"send check passed for ({point.X}, {point.Y})");
    }

    private void AtSend(Win32.Point point) => _trace.Keep(_move, PointerTrace.AtSend, _readings.Snapshot(point, TargetWindow));

    public void Retrieved(Win32.Message message, bool removed)
    {
        if (IsInput(message.Id))
        {
            RecordStage("retrieval", $"{(removed ? "removed" : "peeked")} {InputDetails(message)}");
        }
        else if (removed)
        {
            _otherRemoved++;
        }
        else
        {
            _otherPeeked++;
        }
    }

    public void MouseObserved(uint id, Win32.MouseHook packet, bool processed) =>
        RecordStage("mouse hook",
            $"{(processed ? "processed" : "peeked")} 0x{id:X} window {packet.Window:X} at ({packet.Point.X}, {packet.Point.Y}), marker {packet.ExtraInfo}, hit test {unchecked((int)packet.HitTest)}");

    public void Filtered(Win32.Message message)
    {
        if (IsInput(message.Id))
        {
            RecordStage("WPF loop", InputDetails(message));
        }
        else
        {
            _otherMessages++;
        }
    }

    public void Arrived(nint window, uint id)
    {
        if (IsInput(id))
        {
            RecordStage("target window", $"0x{id:X} window {window:X}");
        }
    }

    public void Moved(Win32.Point screen, bool accepted) =>
        RecordStage("routed move", $"WPF routed a move at ({screen.X}, {screen.Y}), {(accepted ? "accepted" : "not accepted")}");

    public void SentMessage(Win32.SentMessage message)
    {
        if (message.Id == 0x0084)
        {
            var x = unchecked((short)(long)message.LParam);
            var y = unchecked((short)((long)message.LParam >> 16));
            RecordStage("hit test", $"WM_NCHITTEST window {message.Window:X} at ({x}, {y}), result {message.Result}");
        }
        if (Lifecycle.TryGetValue(message.Id, out var known))
        {
            Note($"{known.Name} handled by window {message.Window:X}"
                + (known.ShowsWParam ? $" (wParam {message.WParam})" : ""));
        }
    }

    public void TimerFired() => Note("wait timer fired");

    private void WaitEnded()
    {
        _trace.Keep(_move, "other retrievals", $"{_otherRemoved} removed, {_otherPeeked} peeked");
        Note($"wait ended; {_otherMessages} other filtered messages counted, their content not recorded");
    }

    private static bool IsInput(uint id) => id is
        >= 0x0200 and <= 0x020E or
        >= 0x00A0 and <= 0x00AD or
        >= 0x0241 and <= 0x0253 or
        >= 0x02A0 and <= 0x02A3 or 0x00FF;

    private static string InputDetails(Win32.Message message) =>
        $"0x{message.Id:X} window {message.Window:X}"
        + (message.Id == 0x00FF ? "" : $" at ({message.Point.X}, {message.Point.Y})");

    private void RecordStage(string name, string details)
    {
        var now = _trace.Elapsed;
        if (!_stages.TryGetValue(name, out var stage))
        {
            stage = new Stage(now);
            _stages.Add(name, stage);
        }
        stage.Count++;
        _trace.Keep(_move, name,
            $"count {stage.Count}, first +{(long)stage.First.TotalMilliseconds} ms, last +{(long)now.TotalMilliseconds} ms; {details}");
        _trace.Record(now, _move, $"{name}: {details}");
    }

    private sealed class Stage(TimeSpan first)
    {
        public TimeSpan First { get; } = first;
        public int Count { get; set; }
    }

    private void ReportFailure(FailureList failures, Win32.Point point)
    {
        if (!_reported)
        {
            Report(failures, point, Failed, "the move failed before its outcome was known", readQueue: false,
                trace => new InvalidOperationException($"Pointer trace of the failed move:\n{trace}"));
        }
    }

    private void Report(FailureList failures, Win32.Point point, string outcome, string what, bool readQueue, Func<string, Exception> create)
    {
        ReadLowLevel();
        _reported = true;
        _trace.Keep(_move, outcome, what);
        var readings = new FailureList();
        readings.Attempt(() => _trace.Keep(_move, PointerTrace.AtFailure, _readings.Snapshot(point, TargetWindow)));
        if (readQueue)
        {
            // Read only on timeout: the query changes Windows' new-message bookkeeping.
            readings.Attempt(() => _trace.Keep(_move, PointerTrace.QueueStatus,
                $"pending bits 0x{_readings.PendingInput():X4}, current only, read once at the timeout"));
        }

        failures.Add(create(_trace.Describe()));
        failures.Attempt(readings.ThrowIfAny);
    }

    private void Conclude(FailureList failures, Win32.Point target, PointerOutcome outcome)
    {
        switch (outcome.Result)
        {
            case PointerResult.Observed:
                return;
            case PointerResult.Misplaced:
                Report(failures, target, PointerTrace.Misplaced, outcome.What, readQueue: false,
                    trace => new InvalidOperationException($"{outcome.Message}\n{trace}"));
                return;
            case PointerResult.TimedOut:
                Report(failures, target, PointerTrace.TimedOut, outcome.What, readQueue: true,
                    trace => new TimeoutException($"{outcome.Message}\n{trace}"));
                return;
            default:
                throw new InvalidOperationException($"Unknown pointer result {outcome.Result}.");
        }
    }
}
