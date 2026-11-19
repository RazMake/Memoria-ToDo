using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The list scrolls while the pointer of a drag is in its top or bottom edge band, on the supplied clock, so these tests move
/// time by hand. They raise the standard WPF drag events on a scroll viewer in a showing window, and read its offset. It is a
/// screen test, not a unit test, because a viewer scrolls only inside a window.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AutoScrollTests
{
    private static readonly TimeSpan Plenty = TimeSpan.FromSeconds(2);
    private const double ViewerHeight = 200;
    private const double Middle = ViewerHeight / 2;
    private const double NearTop = 5;
    private const double NearBottom = ViewerHeight - 5;

    [WpfFact]
    public void Scrolls_down_while_the_pointer_is_in_the_bottom_band()
    {
        using var host = new Host(contentHeight: 2000);

        host.Over(NearBottom);
        host.Pass(Plenty);

        Assert.True(host.Viewer.VerticalOffset > 0);
    }

    [WpfFact]
    public void Scrolls_up_while_the_pointer_is_in_the_top_band()
    {
        using var host = new Host(contentHeight: 2000);
        host.Viewer.ScrollToVerticalOffset(500);
        host.Viewer.UpdateLayout();

        host.Over(NearTop);
        host.Pass(Plenty);

        Assert.True(host.Viewer.VerticalOffset < 500);
    }

    [WpfFact]
    public void Keeps_scrolling_as_time_passes_while_the_pointer_stays_in_the_band()
    {
        using var host = new Host(contentHeight: 2000);
        host.Over(NearBottom);
        host.Pass(Plenty);
        var first = host.Viewer.VerticalOffset;

        host.Pass(Plenty);

        Assert.True(host.Viewer.VerticalOffset > first);
    }

    [WpfFact]
    public void Does_not_scroll_while_the_pointer_is_in_the_middle()
    {
        using var host = new Host(contentHeight: 2000);

        host.Over(Middle);
        host.Pass(Plenty);

        Assert.Equal(0, host.Viewer.VerticalOffset);
    }

    [WpfFact]
    public void Stops_when_the_pointer_leaves_the_band_for_the_middle()
    {
        using var host = new Host(contentHeight: 2000);
        host.Over(NearBottom);
        host.Pass(Plenty);

        host.Over(Middle);
        var stopped = host.Viewer.VerticalOffset;
        host.Pass(Plenty);

        Assert.Equal(stopped, host.Viewer.VerticalOffset);
    }

    [WpfFact]
    public void Stops_at_the_end_of_the_list()
    {
        using var host = new Host(contentHeight: ViewerHeight + 60);
        host.Over(NearBottom);

        host.Pass(Plenty);
        var scrolls = host.Scrolls;
        host.Pass(Plenty);

        Assert.Equal(host.Viewer.ScrollableHeight, host.Viewer.VerticalOffset);
        Assert.Equal(scrolls, host.Scrolls);
    }

    [WpfFact]
    public void Stops_when_the_drop_lands()
    {
        using var host = new Host(contentHeight: 2000);
        host.Over(NearBottom);
        host.Pass(Plenty);

        host.Drop(NearBottom);
        var stopped = host.Viewer.VerticalOffset;
        host.Pass(Plenty);

        Assert.Equal(stopped, host.Viewer.VerticalOffset);
    }

    [WpfFact]
    public void Stops_when_the_drag_leaves_the_list()
    {
        using var host = new Host(contentHeight: 2000);
        host.Over(NearBottom);
        host.Pass(Plenty);

        host.Leave();
        var stopped = host.Viewer.VerticalOffset;
        host.Pass(Plenty);

        Assert.Equal(stopped, host.Viewer.VerticalOffset);
    }

    [WpfFact]
    public void Tells_the_caller_the_pointers_place_in_the_viewer_after_each_scroll()
    {
        using var host = new Host(contentHeight: 2000);

        host.Over(NearBottom);
        host.Pass(Plenty);

        Assert.NotEmpty(host.Pointers);
        Assert.All(host.Pointers, pointer => Assert.Equal(new Point(10, NearBottom), pointer));
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
