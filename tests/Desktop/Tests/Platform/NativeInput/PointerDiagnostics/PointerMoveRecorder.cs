using System.Runtime.ExceptionServices;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Records one move and keeps callback failures from crossing into native code.</summary>
internal sealed class PointerMoveRecorder
{
    private const string Failed = "failed";
    private const uint FirstMouseMessage = 0x0200;
    private const uint LastMouseMessage = 0x020E;

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
    private bool _reported;
    private readonly IPointerReadings _readings;
    private readonly Action _stopWait;
    private Exception? _stopFailure;

    public PointerMoveRecorder(PointerTrace trace, string move, nint targetWindow, nint owner,
        IPointerReadings readings, Action stopWait)
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
                ThrowCallbackFailure();
                Conclude(failures, target, steps.Decide());
            }
            catch (Exception failure)
            {
                failures.Add(failure);
                if (_stopFailure is not null)
                {
                    failures.Add(_stopFailure);
                }

                failures.Attempt(() => ReportFailure(failures, target));
            }
        }
        finally
        {
            failures.Attempt(() => steps.Release(failures));
        }

        failures.ThrowIfAny();
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

    public void Mouse(ObservedMessage message) => Note(
        $"mouse message 0x{message.Id:X} marker {message.ExtraInfo} at ({message.Screen.X}, {message.Screen.Y}), expected window {message.InExpectedWindow}");

    public void Fetched(uint id)
    {
        if (id < FirstMouseMessage || id > LastMouseMessage)
        {
            _otherMessages++;
        }
    }

    public void Moved(Win32.Point screen, bool accepted) =>
        Note($"WPF routed a move at ({screen.X}, {screen.Y}), {(accepted ? "accepted" : "not accepted")}");

    public void SentMessage(Win32.SentMessage message)
    {
        if (Lifecycle.TryGetValue(message.Id, out var known))
        {
            Note($"{known.Name} handled by window {message.Window:X}"
                + (known.ShowsWParam ? $" (wParam {message.WParam})" : ""));
        }
    }

    public void TimerFired() => Note("wait timer fired");

    private void WaitEnded() => Note($"wait ended; {_otherMessages} other messages counted, their content not recorded");

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
