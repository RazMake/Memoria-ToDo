using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Integration]
[Collection(ScreenTests.Collection)]
public class PointerMessageObserverIntegrationTests
{
    [WpfFact]
    public void Observes_real_input_before_Wpf_routes_it_and_records_nothing_after_release()
    {
        var target = new Border { Width = 300, Height = 100, Background = Brushes.White };
        var window = new Window { Content = target, Width = 400, Height = 240 };
        var failures = new FailureList();
        PointerMessageObserver? observer = null;
        MouseEventHandler? moved = null;
        try
        {
            window.Show();
            AfterFirstDraw.Wait(window.Dispatcher);
            var source = (HwndSource)PresentationSource.FromVisual(target);
            var trace = new PointerTrace();
            var move = PointerTrace.Label(trace.BeginCall(), "target");
            var recorder = new PointerMoveRecorder(trace, move, source.Handle, source.Handle, new NativePointerReadings(), () => { });
            observer = new PointerMessageObserver(source, recorder, _ => { }, _ => { });
            observer.Attach();
            moved = (_, e) =>
            {
                var point = target.PointToScreen(e.GetPosition(target));
                recorder.Moved(new Win32.Point { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) }, true);
            };
            target.AddHandler(Mouse.PreviewMouseMoveEvent, moved, handledEventsToo: true);

            RealPointer.MoveOver(window, target, new Point(100, 50));
            var screen = target.PointToScreen(new Point(100, 50));
            var packed = unchecked((nint)((int)(ushort)(short)screen.X | ((int)(ushort)(short)screen.Y << 16)));
            Assert.Equal((nint)1, SendMessage(source.Handle, 0x0084, 0, packed));
            var text = trace.Describe();
            var entries = text.Split("Entries, oldest first:", StringSplitOptions.None)[1]
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var retrieved = Array.FindIndex(entries, entry => entry.Contains("retrieval") && entry.Contains("0x200") && entry.Contains("removed"));
            var arrived = Array.FindIndex(entries, entry => entry.Contains("target window") && entry.Contains("0x200"));
            var routed = Array.FindIndex(entries, entry => entry.Contains("WPF routed a move"));

            Assert.True(retrieved >= 0 && arrived > retrieved && routed > arrived, text);
            Assert.Contains($"WM_NCHITTEST window {source.Handle:X}", text);
            Assert.Contains("result 1", text);

            observer.Release(failures);
            failures.ThrowIfAny();
            target.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved);
            moved = null;
            var afterRelease = trace.Describe();
            RealPointer.MoveOver(window, target, new Point(180, 50));
            SendMessage(source.Handle, 0x0084, 0, packed);
            Assert.Equal(afterRelease, trace.Describe());
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
        finally
        {
            if (moved is not null)
            {
                failures.Attempt(() => target.RemoveHandler(Mouse.PreviewMouseMoveEvent, moved));
            }
            observer?.Release(failures);
            failures.Attempt(window.Close);
        }
        failures.ThrowIfAny();
    }

    [RealInput, DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}
