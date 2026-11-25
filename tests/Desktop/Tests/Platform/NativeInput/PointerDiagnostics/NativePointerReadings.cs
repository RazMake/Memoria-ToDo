namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed class NativePointerReadings : IPointerReadings
{
    public bool IsOnScreen(Win32.Point point) => Win32.IsOnScreen(point);
    public nint WindowAt(Win32.Point point) => Win32.WindowAt(point);
    public nint ForegroundWindow() => Win32.ForegroundWindow();
    public bool IsChildOf(nint window, nint parent) => Win32.IsChildOf(window, parent);
    public string Snapshot(Win32.Point point, nint targetWindow) => PointerSnapshot.Take(point, targetWindow);
    public uint PendingInput() => Win32.PendingInput();
}
