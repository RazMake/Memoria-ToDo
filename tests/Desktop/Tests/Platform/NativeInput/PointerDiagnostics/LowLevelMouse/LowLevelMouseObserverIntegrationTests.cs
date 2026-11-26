using System.Runtime.InteropServices;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Integration]
[Collection(ScreenTests.Collection)]
public class LowLevelMouseObserverIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Observes_only_its_marked_move_reports_rejection_and_stops_observing_after_release(bool reject)
    {
        var marker = MoveMarkers.Shared.Next();
        var other = MoveMarkers.Shared.Next();
        var sentinel = MoveMarkers.Shared.Next();
        using var reachedSentinel = new AutoResetEvent(false);
        var failures = new FailureList();
        Exception? callbackFailure = null;
        var markedMoves = 0;
        var otherMoves = 0;
        Win32.MessageHook downstream = (code, message, data) =>
        {
            try
            {
                if (code == 0 && message == 0x200)
                {
                    var packet = Marshal.PtrToStructure<Win32.LowLevelMouseHook>(data);
                    if ((packet.Flags & 1) != 0)
                    {
                        if (packet.ExtraInfo == sentinel) reachedSentinel.Set();
                        if (packet.ExtraInfo == other) Interlocked.Increment(ref otherMoves);
                        if (packet.ExtraInfo == marker)
                        {
                            Interlocked.Increment(ref markedMoves);
                            if (reject) return 1;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Interlocked.CompareExchange(ref callbackFailure, error, null);
            }
            return Win32.NextMessage(code, message, data);
        };
        var blocker = new MessageLoopThread(new NativeMessageLoop(),
            () => Win32.HookLowLevelMouse(downstream), Win32.UnhookMessages, TestWait.ForSignal);
        var observer = new LowLevelMouseObserver(marker);
        void AwaitSentinel() =>
            Assert.True(reachedSentinel.WaitOne(TestWait.ForSignal),
                $"The sentinel move did not reach the native hook; own marked moves {Volatile.Read(ref markedMoves)}, " +
                $"other marked moves {Volatile.Read(ref otherMoves)}, observer matches {observer.Snapshot().Count}.");
        try
        {
            blocker.Start();
            observer.Attach();
            var point = Win32.CursorPosition();
            var unmarkedPoint = new Win32.Point { X = point.X + 5, Y = point.Y };
            if (!Win32.IsOnScreen(unmarkedPoint)) unmarkedPoint.X = point.X - 5;
            var otherPoint = new Win32.Point { X = point.X, Y = point.Y + 5 };
            if (!Win32.IsOnScreen(otherPoint)) otherPoint.Y = point.Y - 5;
            var sentinelPoint = new Win32.Point { X = unmarkedPoint.X, Y = otherPoint.Y };
            Assert.True(Win32.IsOnScreen(sentinelPoint), "The sentinel point must be on the screen.");
            NativeInput.Send(NativeInput.Move(unmarkedPoint, 0), NativeInput.Move(otherPoint, other),
                NativeInput.Move(point, marker), NativeInput.Move(sentinelPoint, sentinel));
            AwaitSentinel();
            var snapshot = observer.Snapshot();
            var move = Assert.Single(snapshot.Samples);
            Assert.Equal(1, snapshot.Count);
            Assert.Null(snapshot.Failure);
            Assert.Equal(reject ? (nint)1 : 0, move.Result);
            Assert.NotNull(move.Completed);
            Assert.True(move.Completed >= move.Entered);
            Assert.Equal((point.X, point.Y), (move.Point.X, move.Point.Y));
            Assert.Equal(1u, move.InjectionFlags & 1);
            observer.Release(failures);
            failures.ThrowIfAny();
            NativeInput.Send(NativeInput.Move(point, marker), NativeInput.Move(unmarkedPoint, sentinel));
            AwaitSentinel();
            var after = observer.Snapshot();
            Assert.Equal(1, after.Count);
            Assert.Equal(snapshot.Samples, after.Samples);
            Assert.Null(callbackFailure);
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
        finally
        {
            observer.Release(failures);
            blocker.Stop(failures);
            GC.KeepAlive(downstream);
            if (callbackFailure is not null) failures.Add(callbackFailure);
        }
        failures.ThrowIfAny();
    }
}
