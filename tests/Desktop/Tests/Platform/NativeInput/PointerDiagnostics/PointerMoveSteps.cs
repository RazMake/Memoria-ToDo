using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal interface IPointerReadings
{
    bool IsOnScreen(Win32.Point point);
    nint WindowAt(Win32.Point point);
    nint ForegroundWindow();
    bool IsChildOf(nint window, nint parent);
    string Snapshot(Win32.Point point, nint targetWindow);
    uint PendingInput();
}

internal enum PointerResult { Observed, Misplaced, TimedOut }

internal readonly record struct PointerOutcome(PointerResult Result, string Message = "", string What = "");

internal sealed record PointerMoveSteps(Action Acquire, Action Send, Action Wait, Func<PointerOutcome> Decide, Action<FailureList> Release);
