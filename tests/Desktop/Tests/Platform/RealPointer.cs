using System.Windows;
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
        var current = Win32.CursorPosition();
        if (current.X == target.X && current.Y == target.Y)
        {
            var away = new Win32.Point { X = target.X + 2, Y = target.Y };
            if (!Win32.IsOnScreen(away))
            {
                away.X = target.X - 2;
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

        var move = NativeInput.Move(target, marker);
        var receipt = new InjectedMoveReceipt(marker, target);
        var frame = new DispatcherFrame();
        var observed = false;
        Win32.Point? last = null;
        ThreadMessageEventHandler fetched = (ref MSG message, ref bool handled) =>
        {
            receipt.Observe(new ObservedMessage(
                (uint)message.message,
                Win32.MessageExtraInfo(),
                new Win32.Point { X = message.pt_x, Y = message.pt_y },
                message.hwnd == expectedWindow || Win32.IsChildOf(message.hwnd, expectedWindow)));
            if (receipt.MisplacedAt is not null)
            {
                frame.Continue = false;
            }
        };
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
        var timeout = new DispatcherTimer(TestWait.ForSignal, DispatcherPriority.Normal,
            (_, _) => frame.Continue = false, element.Dispatcher);
        ComponentDispatcher.ThreadPreprocessMessage += fetched;
        element.AddHandler(Mouse.PreviewMouseMoveEvent, moved, handledEventsToo: true);
        try
        {
            NativeInput.Send(move);
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            element.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved);
            ComponentDispatcher.ThreadPreprocessMessage -= fetched;
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
                + $"{(receipt.Received ? "the packet's message was fetched but no routed move followed it" : "the packet's message was never fetched")}, "
                + $"requested screen ({target.X}, {target.Y}), "
                + $"cursor ({current.X}, {current.Y}), last observed event {(last is { } seen ? $"({seen.X}, {seen.Y})" : "none")}, "
                + $"directly over {Mouse.DirectlyOver}; {Describe(expectedWindow, Win32.WindowAt(target))}.");
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
