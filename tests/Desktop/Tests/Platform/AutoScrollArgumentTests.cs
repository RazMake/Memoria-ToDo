using System.Windows.Controls;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class AutoScrollArgumentTests
{
    [WpfFact]
    public void Attach_rejects_a_missing_viewer()
    {
        Assert.Throws<ArgumentNullException>(() => AutoScroll.Attach(null!, new FakeTimeProvider(), _ => { }));
    }

    [WpfFact]
    public void Attach_rejects_a_missing_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => AutoScroll.Attach(new ScrollViewer(), null!, _ => { }));
    }

    [WpfFact]
    public void Attach_rejects_a_missing_scrolled_callback()
    {
        Assert.Throws<ArgumentNullException>(() => AutoScroll.Attach(new ScrollViewer(), new FakeTimeProvider(), null!));
    }
}
