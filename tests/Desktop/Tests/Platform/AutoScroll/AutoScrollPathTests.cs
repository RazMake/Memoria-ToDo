using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.AutoScroll;

using AutoScroll = Memoria.ToDo.Desktop.Platform.AutoScroll;

/// <summary>
/// The list scrolls while the pointer of a drag is in its top or bottom edge band, on the supplied clock, so these tests move
/// time by hand. They raise the standard WPF drag events on a scroll viewer in a showing window, and read its offset. They are
/// screen tests because a viewer scrolls only inside a window. Each test is one path.
/// </summary>
[UX("AutoScroll")]
[Collection(ScreenTests.Collection)]
public class AutoScrollPathTests
{
    private static readonly TimeSpan Plenty = TimeSpan.FromSeconds(2);
    private const double ViewerHeight = 200;
    private const double Middle = ViewerHeight / 2;
    private const double NearTop = 5;
    private const double NearBottom = ViewerHeight - 5;

    [WpfFact]
    public void Dragging_down_in_the_bottom_band_scrolls_and_keeps_scrolling_until_the_pointer_leaves_it_is_dropped_or_leaves_the_list()
    {
        using var host = new Host(contentHeight: 2000);

        host.Over(Middle);
        host.Pass(Plenty);
        Assert.Equal(0, host.Viewer.VerticalOffset);

        host.Over(NearBottom);
        host.Pass(Plenty);
        var first = host.Viewer.VerticalOffset;
        Assert.True(first > 0);
        Assert.NotEmpty(host.Pointers);
        Assert.All(host.Pointers, pointer => Assert.Equal(new Point(10, NearBottom), pointer));

        host.Pass(Plenty);
        Assert.True(host.Viewer.VerticalOffset > first);

        host.Over(Middle);
        var stoppedInTheMiddle = host.Viewer.VerticalOffset;
        host.Pass(Plenty);
        Assert.Equal(stoppedInTheMiddle, host.Viewer.VerticalOffset);

        host.Over(NearBottom);
        host.Pass(Plenty);
        host.Drop(NearBottom);
        var stoppedByTheDrop = host.Viewer.VerticalOffset;
        host.Pass(Plenty);
        Assert.Equal(stoppedByTheDrop, host.Viewer.VerticalOffset);

        host.Over(NearBottom);
        host.Pass(Plenty);
        host.Leave();
        var stoppedByLeaving = host.Viewer.VerticalOffset;
        host.Pass(Plenty);
        Assert.Equal(stoppedByLeaving, host.Viewer.VerticalOffset);
    }

    [WpfFact]
    public void Dragging_in_the_top_band_scrolls_up()
    {
        using var host = new Host(contentHeight: 2000);
        host.Viewer.ScrollToVerticalOffset(500);
        host.Viewer.UpdateLayout();

        host.Over(NearTop);
        host.Pass(Plenty);

        Assert.True(host.Viewer.VerticalOffset < 500);
    }

    [WpfFact]
    public void Scrolling_stops_at_the_end_of_the_list()
    {
        using var host = new Host(contentHeight: ViewerHeight + 60);
        host.Over(NearBottom);

        host.Pass(Plenty);
        var scrolls = host.Scrolls;
        host.Pass(Plenty);

        Assert.Equal(host.Viewer.ScrollableHeight, host.Viewer.VerticalOffset);
        Assert.Equal(scrolls, host.Scrolls);
    }

    private sealed class Host : IDisposable
    {
        private static readonly IDataObject Data = new DataObject("test", "drag");
        private readonly FakeTimeProvider _time = new();
        private readonly Window _window;

        public Host(double contentHeight)
        {
            Viewer = new ScrollViewer
            {
                Width = 300,
                Height = ViewerHeight,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = new Border { Height = contentHeight },
            };
            AutoScroll.Attach(Viewer, _time, pointer =>
            {
                Pointers.Add(pointer);
            });
            _window = new Window { Content = Viewer, SizeToContent = SizeToContent.WidthAndHeight };
            _window.Show();
        }

        public ScrollViewer Viewer { get; }

        public List<Point> Pointers { get; } = [];

        public int Scrolls => Pointers.Count;

        public void Over(double y) => SimulatedDrop.Over(Viewer, Data, new Point(10, y));

        public void Drop(double y) => SimulatedDrop.Data(Viewer, Data, new Point(10, y));

        public void Leave() => SimulatedDrop.Leave(Viewer, Data);

        public void Pass(TimeSpan time)
        {
            _time.Advance(time);
            FlushDispatcher();
        }

        public void FlushDispatcher() => Viewer.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

        public void Dispose() => _window.Close();
    }
}
