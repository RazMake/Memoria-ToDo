using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Moves the real pointer and waits until WPF observes it on the requested element.</summary>
[RealInput]
internal static class RealPointer
{
    private static readonly PointerTraceHolder Traces = new();
    private static readonly IPointerReadings Readings = new NativePointerReadings();

    public static void MoveOver(Window window, FrameworkElement element, Point inElement)
    {
        var testId = TestContext.Current.Test?.UniqueID
            ?? throw new InvalidOperationException("A real pointer move needs a running test to keep its trace under.");
        var trace = Traces.For(testId);
        var call = trace.BeginCall();
        AfterFirstDraw.Wait(element.Dispatcher);
        // A window a background process shows can be active yet behind another program's, which would then get the moves.
        var owner = new WindowInteropHelper(window).Handle;
        KeyboardInput.BringToFront(owner);
        if (!window.IsActive)
        {
            throw new InvalidOperationException("The window is not active; hover cards require an active window.");
        }

        var onScreen = element.PointToScreen(inElement);
        var target = new Win32.Point { X = (int)Math.Round(onScreen.X), Y = (int)Math.Round(onScreen.Y) };
        var expected = (PresentationSource.FromVisual(element) as HwndSource)?.Handle
            ?? throw new InvalidOperationException("The pointer target is not in a window.");
        PointerMoveRecorder Recorder(string step, Action stopWait, Func<MarkedMoveReport>? lowLevel = null) =>
            new(trace, PointerTrace.Label(call, step), expected, owner, Readings, stopWait, lowLevel);

        // Check the final target before even the preparatory move can send input.
        Recorder("target", () => { }).Verify(target);

        // WPF must observe the step away before the pointer returns, or Windows can coalesce both moves.
        // The step is far enough that it starts, as the move back does, more than one pixel from its own target.
        if (InjectedMoveReceipt.IsAtTarget(Win32.CursorPosition(), target))
        {
            var away = new Win32.Point { X = target.X + 3, Y = target.Y };
            if (!Win32.IsOnScreen(away))
            {
                away.X = target.X - 3;
            }

            if (PresentationSource.FromVisual(element)?.RootVisual is not UIElement root)
            {
                throw new InvalidOperationException("The pointer target has no input root.");
            }

            MoveAndObserve((stop, lowLevel) => Recorder("away", stop, lowLevel), root, away);
        }

        MoveAndObserve((stop, lowLevel) => Recorder("target", stop, lowLevel), element, target);
    }

    private static void MoveAndObserve(Func<Action, Func<MarkedMoveReport>, PointerMoveRecorder> newRecorder, UIElement element, Win32.Point target)
    {
        // Positive, unique markers cannot be confused with earlier moves or WPF's reserved pen/touch markers.
        var marker = MoveMarkers.Shared.Next();
        var start = Win32.CursorPosition();
        var receipt = new InjectedMoveReceipt(marker, start, target);
        var frame = new DispatcherFrame();
        Action stopWait = () => frame.Continue = false;
        var lowLevel = new LowLevelMouseObserver(marker);
        var recorder = newRecorder(stopWait, lowLevel.Snapshot);
        recorder.Note($"marker {marker}, cursor start ({start.X}, {start.Y}), target ({target.X}, {target.Y})");
        recorder.Note("low-level hook times are measured from the observer's creation, just before this entry");
        var expectedWindow = recorder.TargetWindow;
        var observed = false;
        Win32.Point? last = null;
        ObservedMessage? lastPacket = null;
        var mouseMessages = 0;
        var source = HwndSource.FromHwnd(expectedWindow)
            ?? throw new InvalidOperationException("The pointer target has no WPF window source.");
        var observer = new PointerMessageObserver(source, recorder, processed: packet =>
        {
            mouseMessages++;
            lastPacket = packet;
            receipt.Observe(packet);
            if (receipt.MisplacedAt is not null)
            {
                stopWait();
            }
        }, filtered: id =>
        {
            if (id != 0x0200)
            {
                receipt.Observe(new ObservedMessage(id, 0, default, false));
            }
        });

        MouseEventHandler moved = (_, e) => recorder.Guard(() =>
        {
            var screen = element.PointToScreen(e.GetPosition(element));
            var routed = new Win32.Point { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
            last = routed;
            var accepted = receipt.TakeAcknowledgement();
            recorder.Moved(routed, accepted);
            if (accepted)
            {
                observed = true;
                stopWait();
            }
        });
        var timeout = new DispatcherTimer(DispatcherPriority.Normal, element.Dispatcher) { Interval = TestWait.ForSignal };
        timeout.Tick += (_, _) => recorder.Guard(() =>
        {
            recorder.TimerFired();
            stopWait();
        });

        recorder.Run(target, new PointerMoveSteps(
            Acquire: () =>
            {
                observer.Attach();
                element.AddHandler(Mouse.PreviewMouseMoveEvent, moved, handledEventsToo: true);
                recorder.Note("installing the low-level mouse hook");
                lowLevel.Attach();
                recorder.Note("low-level mouse hook installed");
            },
            Send: () =>
            {
                var move = NativeInput.Move(target, marker);
                recorder.Note("calling SendInput");
                timeout.Start();
                uint inserted;
                try
                {
                    inserted = NativeInput.Send(move);
                }
                catch (Exception rejected)
                {
                    recorder.Note($"SendInput failed: {rejected.Message}");
                    throw;
                }

                recorder.Note($"SendInput inserted {inserted} of 1");
            },
            Wait: () => Dispatcher.PushFrame(frame),
            Decide: () =>
            {
                if (receipt.MisplacedAt is { } misplaced)
                {
                    var landed = $"The pointer packet for {element.GetType().Name} landed at screen ({misplaced.X}, {misplaced.Y}), not at ({target.X}, {target.Y}); "
                        + $"{PointerSnapshot.Windows(target, expectedWindow)}.";
                    return new PointerOutcome(PointerResult.Misplaced, landed, $"landed at ({misplaced.X}, {misplaced.Y})");
                }

                if (observed)
                {
                    return new PointerOutcome(PointerResult.Observed);
                }

                var timedOut =
                    $"WPF did not observe the pointer on {element.GetType().Name}: "
                    + "SendInput accepted the packet; "
                    + $"{(receipt.Received ? "the processed-mouse hook observed its move but no routed move followed it" : "the processed-mouse hook did not observe its move")} "
                    + $"({mouseMessages} processed mouse-hook messages while waiting), "
                    + $"requested screen ({target.X}, {target.Y}), "
                    + $"{PointerSnapshot.Cursor()}, last observed event {(last is { } seen ? $"({seen.X}, {seen.Y})" : "none")}, "
                    + $"expected marker {marker}, last processed mouse-hook message "
                    + $"{(lastPacket is { } packet ? $"0x{packet.Id:X} marker {packet.ExtraInfo} at ({packet.Screen.X}, {packet.Screen.Y}), expected window {packet.InExpectedWindow}" : "none")}, "
                    + $"directly over {PointerSnapshot.TypeName(Mouse.DirectlyOver)}, captured by {PointerSnapshot.TypeName(Mouse.Captured)}; "
                    + $"{PointerSnapshot.Windows(target, expectedWindow)}.";
                return new PointerOutcome(PointerResult.TimedOut, timedOut, "no routed move was accepted before the wait ended");
            },
            Release: failures =>
            {
                failures.Attempt(timeout.Stop);
                failures.Attempt(() => element.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved));
                observer.Release(failures);
                recorder.Note("removing the low-level mouse hook");
                lowLevel.Release(failures);
            }));
    }
}
