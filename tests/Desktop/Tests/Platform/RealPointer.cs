using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Moves the real pointer and waits until WPF observes it on the requested element.</summary>
internal static class RealPointer
{
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

        if (Win32.ProcessIdOf(Win32.WindowAt(target)) != Environment.ProcessId)
        {
            throw new InvalidOperationException(
                $"Another program's window covers the requested pointer position ({target.X}, {target.Y}), so it would get the moves.");
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

            MoveAndObserve(root, away);
        }

        MoveAndObserve(element, target);
    }

    private static void MoveAndObserve(UIElement element, Win32.Point target)
    {
        var frame = new DispatcherFrame();
        var observed = false;
        var landed = target;
        var last = Mouse.GetPosition(element);
        MouseEventHandler moved = (_, e) =>
        {
            last = e.GetPosition(element);
            var actual = element.PointToScreen(last);
            if (Math.Abs(actual.X - landed.X) <= 1 && Math.Abs(actual.Y - landed.Y) <= 1)
            {
                observed = true;
                frame.Continue = false;
            }
        };
        var timeout = new DispatcherTimer(TestWait.ForSignal, DispatcherPriority.Normal,
            (_, _) => frame.Continue = false, element.Dispatcher);
        element.AddHandler(Mouse.PreviewMouseMoveEvent, moved, handledEventsToo: true);
        try
        {
            Win32.MoveCursor(target);
            landed = Win32.CursorPosition();
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            element.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved);
        }

        if (!observed)
        {
            var current = Win32.CursorPosition();
            throw new TimeoutException(
                $"WPF did not observe the pointer on {element.GetType().Name}: requested screen ({target.X}, {target.Y}), "
                + $"current screen ({current.X}, {current.Y}), last element position {last}, directly over {Mouse.DirectlyOver}.");
        }
    }
}
