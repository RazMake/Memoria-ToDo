using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Scrolls a scroll viewer while the pointer of a drag is in its top or bottom edge band (<see cref="EdgeBand"/>). It stops when the
/// pointer leaves the band, the viewer reaches its end, the drop lands, or the drag leaves or is canceled.
/// </summary>
public static class AutoScroll
{
    /// <summary>Makes the viewer scroll during a drag over it, for as long as the viewer lives.</summary>
    /// <param name="viewer">The viewer to scroll.</param>
    /// <param name="time">Makes the timer that paces the scrolling.</param>
    /// <param name="scrolled">Called after each scroll with the pointer's place in the viewer, so the caller can follow what is under it.</param>
    public static void Attach(ScrollViewer viewer, TimeProvider time, Action<Point> scrolled)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(scrolled);
        _ = new Scroller(viewer, time, scrolled);
    }

    private sealed class Scroller
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(30);

        private readonly ScrollViewer _viewer;
        private readonly Action<Point> _scrolled;
        private readonly ITimer _timer;
        private Point _pointer;
        private bool _running;

        public Scroller(ScrollViewer viewer, TimeProvider time, Action<Point> scrolled)
        {
            _viewer = viewer;
            _scrolled = scrolled;
            // The timer ticks on any thread, so each tick is posted to the viewer's thread, as PendingUndo does.
            _timer = time.CreateTimer(_ => viewer.Dispatcher.BeginInvoke(Scroll, DispatcherPriority.Input), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            viewer.PreviewDragOver += (_, e) => Follow(e.GetPosition(viewer));
            viewer.PreviewDragLeave += (_, _) => Stop();
            viewer.PreviewDrop += (_, _) => Stop();
            viewer.Unloaded += (_, _) => Stop();
        }

        private void Follow(Point pointer)
        {
            _pointer = pointer;
            if (EdgeBand.Step(pointer.Y, _viewer.ViewportHeight) == 0)
            {
                Stop();
            }
            else if (!_running)
            {
                _running = true;
                _timer.Change(Interval, Interval);
            }
        }

        private void Stop()
        {
            _running = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void Scroll()
        {
            if (!_running)
            {
                return;
            }

            var step = EdgeBand.Step(_pointer.Y, _viewer.ViewportHeight);
            var before = _viewer.VerticalOffset;
            _viewer.ScrollToVerticalOffset(before + step);
            _viewer.UpdateLayout();
            if (step == 0 || _viewer.VerticalOffset == before)
            {
                Stop();
                return;
            }

            _scrolled(_pointer);
        }
    }
}
