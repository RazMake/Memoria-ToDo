using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.App.Tests;

[RealInput]
internal static class RealClick
{
    public static async Task OnAsync(Window window, FrameworkElement element, Point local, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, TimeSpan.Zero);

        var handle = new WindowInteropHelper(window).Handle;
        var report = new ClickReport("not recorded");
        var clock = new Stopwatch();
        var observation = new Observation(window, handle, report, clock);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MouseButtonEventHandler onReleased = (_, _) => released.TrySetResult();
        var failures = new FailureList();
        var sent = default(Win32.Point);
        var inputFailed = false;

        window.PreviewMouseLeftButtonUp += onReleased;
        try
        {
            KeyboardInput.BringToFront(handle);
            var screen = element.PointToScreen(local);
            observation.Observe(() => report.Aim = Aim(window, element, local, screen));
            observation.Start();
            clock.Restart();
            try
            {
                try
                {
                    sent = MouseInput.Press(screen);
                }
                catch (Exception press)
                {
                    failures.Add(press);
                    inputFailed = true;
                }

                if (!inputFailed)
                {
                    observation.Observe(() =>
                    {
                        report.Record(ClickStage.Press, clock.Elapsed, $"accepted, sent pixel ({sent.X}, {sent.Y})");
                        report.AfterPress = PointerSnapshot.Take(sent, handle);
                    });
                }
            }
            finally
            {
                try
                {
                    MouseInput.Release();
                    observation.Observe(() => report.Record(ClickStage.Release, clock.Elapsed, "accepted"));
                }
                catch (Exception release)
                {
                    failures.Add(release);
                    inputFailed = true;
                }
            }

            if (!inputFailed)
            {
                try
                {
                    await released.Task.WaitAsync(limit);
                }
                catch (TimeoutException timeout)
                {
                    observation.Observe(() => report.AtTimeout = PointerSnapshot.Take(sent, handle));
                    observation.Observe(() => report.PendingInput =
                        PointerReading.Of("pending bits", () => $"0x{Win32.PendingInput():X4}") + ", current only, read once at the timeout");
                    var details = "Real click report unavailable.";
                    observation.Observe(() => details = report.Describe());
                    failures.Add(new TimeoutException($"{timeout.Message}\n{details}", timeout));
                }
            }
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
        finally
        {
            failures.Attempt(() => window.PreviewMouseLeftButtonUp -= onReleased);
            observation.Stop(failures);
        }

        if (observation.Failure is { } diagnosticFailure)
        {
            failures.Add(diagnosticFailure);
        }

        failures.ThrowIfAny();
    }

    private static string Aim(Window window, FrameworkElement element, Point local, Point screen)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return string.Create(CultureInfo.InvariantCulture,
            $"{element.GetType().Name} {element.ActualWidth:0.##}x{element.ActualHeight:0.##}; local ({local.X:0.##}, {local.Y:0.##}); "
            + $"screen ({screen.X:0.##}, {screen.Y:0.##}); element on screen ({topLeft.X:0.##}, {topLeft.Y:0.##}) to ({bottomRight.X:0.##}, {bottomRight.Y:0.##}); "
            + $"element's window is the window brought to the front {Window.GetWindow(element) == window}; window active {window.IsActive}");
    }

    private sealed class Observation
    {
        private static readonly Dictionary<uint, ClickStage> Messages = new()
        {
            [0x0200] = ClickStage.NativeMove,
            [0x0201] = ClickStage.NativeDown,
            [0x0202] = ClickStage.NativeUp,
            [0x00A0] = ClickStage.NativeMove,
            [0x00A1] = ClickStage.NativeDown,
            [0x00A2] = ClickStage.NativeUp,
        };

        private readonly Window _window;
        private readonly nint _target;
        private readonly ClickReport _report;
        private readonly Stopwatch _clock;
        private readonly Win32.MessageHook _mouse;
        private readonly (RoutedEvent Event, Delegate Handler)[] _routed;
        private nint _hook;

        public Observation(Window window, nint target, ClickReport report, Stopwatch clock)
        {
            _window = window;
            _target = target;
            _report = report;
            _clock = clock;
            _mouse = OnMouse;
            _routed =
            [
                (UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) => Routed(ClickStage.RoutedDown, e))),
                (UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, e) => Routed(ClickStage.RoutedUp, e))),
                (UIElement.GotMouseCaptureEvent, new MouseEventHandler((_, e) => Routed(ClickStage.CaptureGained, e))),
                (UIElement.LostMouseCaptureEvent, new MouseEventHandler((_, e) => Routed(ClickStage.CaptureLost, e))),
            ];
        }

        public Exception? Failure { get; private set; }

        public void Observe(Action observe)
        {
            try
            {
                observe();
            }
            catch (Exception failure)
            {
                Failure ??= failure;
            }
        }

        public void Start() => Observe(() =>
        {
            try
            {
                _hook = Win32.HookMouseMessages(_mouse);
            }
            catch (Win32Exception refused)
            {
                _report.NativeMessagesUnavailable($"error {refused.NativeErrorCode}");
            }

            foreach (var (routedEvent, handler) in _routed)
            {
                _window.AddHandler(routedEvent, handler, handledEventsToo: true);
            }
        });

        public void Stop(FailureList failures)
        {
            foreach (var (routedEvent, handler) in _routed)
            {
                failures.Attempt(() => _window.RemoveHandler(routedEvent, handler));
            }

            if (_hook != 0)
            {
                failures.Attempt(() => Win32.UnhookMessages(_hook));
            }

            GC.KeepAlive(_mouse);
        }

        private void Routed(ClickStage stage, RoutedEventArgs e) =>
            Observe(() => _report.Record(stage, _clock.Elapsed, PointerSnapshot.TypeName(e.OriginalSource)));

        private nint OnMouse(int code, nint message, nint data)
        {
            if (code == 0)
            {
                Observe(() =>
                {
                    if (Messages.TryGetValue((uint)message, out var stage))
                    {
                        var packet = Marshal.PtrToStructure<Win32.MouseHook>(data);
                        _report.Record(stage, _clock.Elapsed,
                            $"message 0x{message:X}, point ({packet.Point.X}, {packet.Point.Y}), window {Match(packet.Window)}, hit test {packet.HitTest}");
                    }
                });
            }

            return Win32.NextMessage(code, message, data);
        }

        private string Match(nint window) =>
            window == _target ? "target" : Win32.IsChildOf(window, _target) ? "child of target" : "other";
    }
}
