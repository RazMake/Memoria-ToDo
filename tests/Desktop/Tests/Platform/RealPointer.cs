using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Moves the real pointer and waits until WPF observes it on the requested element.</summary>
[RealInput]
internal static class RealPointer
{
    private static int _lastMarker;

    public static void MoveOver(Window window, FrameworkElement element, Point inElement)
    {
        AfterFirstDraw.Wait(element.Dispatcher);
        // A window a background process shows can be active yet behind another program's, which would then get the moves.
        KeyboardInput.BringToFront(new WindowInteropHelper(window).Handle);
        if (!window.IsActive)
        {
            throw new InvalidOperationException("The window is not active; hover cards require an active window.");
        }

        var onScreen = element.PointToScreen(inElement);
        var target = new Win32.Point { X = (int)Math.Round(onScreen.X), Y = (int)Math.Round(onScreen.Y) };
        if (!Win32.IsOnScreen(target))
        {
            throw new InvalidOperationException($"The requested pointer position ({target.X}, {target.Y}) is off screen.");
        }

        var expected = (PresentationSource.FromVisual(element) as HwndSource)?.Handle
            ?? throw new InvalidOperationException("The pointer target is not in a window.");
        var hit = Win32.WindowAt(target);
        if (hit != expected && !Win32.IsChildOf(hit, expected))
        {
            throw new InvalidOperationException(
                $"Another window covers the requested pointer position ({target.X}, {target.Y}), so it would get the moves: {Describe(expected, hit)}.");
        }

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

            MoveAndObserve(root, away, expected);
        }

        MoveAndObserve(element, target, expected);
    }

    private static void MoveAndObserve(UIElement element, Win32.Point target, nint expectedWindow)
    {
        // Positive, unique markers cannot be confused with earlier moves or WPF's reserved pen/touch markers.
        var marker = Interlocked.Increment(ref _lastMarker);
        if (marker <= 0)
        {
            throw new InvalidOperationException("The pointer move markers of this process ran out.");
        }

        var start = Win32.CursorPosition();
        var move = NativeInput.Move(target, marker);
        var receipt = new InjectedMoveReceipt(marker, start, target);
        var frame = new DispatcherFrame();
        var observed = false;
        Win32.Point? last = null;
        ObservedMessage? lastPacket = null;
        var mouseMessages = 0;
        Win32.MouseMessageHook fetched = (code, message, data) =>
        {
            if (code == 0) // HC_ACTION, not HC_NOREMOVE.
            {
                var packet = Marshal.PtrToStructure<Win32.MouseHook>(data);
                var observedPacket = new ObservedMessage(
                    (uint)message, packet.ExtraInfo, packet.Point,
                    packet.Window == expectedWindow || Win32.IsChildOf(packet.Window, expectedWindow));
                mouseMessages++;
                lastPacket = observedPacket;

                receipt.Observe(observedPacket);
                if (receipt.MisplacedAt is not null)
                {
                    frame.Continue = false;
                }
            }

            return Win32.NextMouseMessage(code, message, data);
        };
        void FilterMessage(ref MSG message, ref bool handled)
        {
            if (message.message != 0x0200)
            {
                receipt.Observe(new ObservedMessage((uint)message.message, 0, default, false));
            }
        }

        MouseEventHandler moved = (_, e) =>
        {
            var screen = element.PointToScreen(e.GetPosition(element));
            last = new Win32.Point { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
            if (receipt.TakeAcknowledgement())
            {
                observed = true;
                frame.Continue = false;
            }
        };
        var hook = Win32.HookMouseMessages(fetched);
        var timeout = new DispatcherTimer(TestWait.ForSignal, DispatcherPriority.Normal,
            (_, _) => frame.Continue = false, element.Dispatcher);
        element.AddHandler(Mouse.PreviewMouseMoveEvent, moved, handledEventsToo: true);
        ComponentDispatcher.ThreadFilterMessage += FilterMessage;
        try
        {
            NativeInput.Send(move);
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            ComponentDispatcher.ThreadFilterMessage -= FilterMessage;
            element.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved);
            Win32.UnhookMouseMessages(hook);
            GC.KeepAlive(fetched);
        }

        if (receipt.MisplacedAt is { } misplaced)
        {
            throw new InvalidOperationException(
                $"The pointer packet for {element.GetType().Name} landed at screen ({misplaced.X}, {misplaced.Y}), not at ({target.X}, {target.Y}); "
                + $"{Describe(expectedWindow, Win32.WindowAt(target))}.");
        }

        if (!observed)
        {
            var current = Win32.CursorPosition();
            throw new TimeoutException(
                $"WPF did not observe the pointer on {element.GetType().Name}: "
                + "SendInput accepted the packet; "
                + $"{(receipt.Received ? "its marked WM_MOUSEMOVE was fetched but no routed move followed it" : "its marked WM_MOUSEMOVE was never fetched")} "
                + $"({mouseMessages} mouse messages fetched while waiting), "
                + $"requested screen ({target.X}, {target.Y}), "
                + $"cursor ({current.X}, {current.Y}), last observed event {(last is { } seen ? $"({seen.X}, {seen.Y})" : "none")}, "
                + $"expected marker {marker}, last fetched mouse message "
                + $"{(lastPacket is { } packet ? $"0x{packet.Id:X} marker {packet.ExtraInfo} at ({packet.Screen.X}, {packet.Screen.Y}), expected window {packet.InExpectedWindow}" : "none")}, "
                + $"directly over {Mouse.DirectlyOver}, captured by {Mouse.Captured}; "
                + $"{Describe(expectedWindow, Win32.WindowAt(target))}.");
        }
    }

    private static string Describe(nint expected, nint hit)
    {
        var hitText = "no window";
        if (hit != 0)
        {
            var rect = Win32.WindowRect(hit);
            hitText = $"{hit:X} (process {Win32.ProcessIdOf(hit)}, rect {rect.Left}, {rect.Top}, {rect.Right}, {rect.Bottom})";
        }

        return $"expected window {expected:X}, hit window {hitText}, foreground window {Win32.ForegroundWindow():X}";
    }
}
