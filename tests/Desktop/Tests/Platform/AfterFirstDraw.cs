using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Waits until WPF has drawn the windows that were just shown. WPF raises Loaded, then draws the first frame, then at
/// Input priority raises ContentRendered. Background is the next priority down, so work posted there runs after all of
/// that. Not a fake of a port: shared test setup.
/// </summary>
internal static class AfterFirstDraw
{
    /// <summary>Runs <paramref name="action"/> on the dispatcher once WPF has drawn every window shown so far.</summary>
    public static void Post(Dispatcher dispatcher, Action action) =>
        dispatcher.BeginInvoke(DispatcherPriority.Background, action);

    /// <summary>Lets the dispatcher work until WPF has drawn every window shown so far. The time limit only bounds a failure.</summary>
    public static void Wait(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        var timedOut = false;
        var timeout = new DispatcherTimer(TestWait.ForSignal, DispatcherPriority.Normal, (_, _) =>
        {
            timedOut = true;
            frame.Continue = false;
        }, dispatcher);
        Post(dispatcher, () => frame.Continue = false);
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
        }

        if (timedOut)
        {
            throw new TimeoutException("WPF didn't draw the window in time.");
        }
    }
}
