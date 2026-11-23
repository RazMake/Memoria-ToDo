using System.Windows;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>Single window's arguments and failures, with functions that make no window, so none shows.</summary>
[Unit]
public class SingleWindowFailureTests
{
    [WpfFact]
    public void Show_passes_on_the_functions_exception_and_calls_the_function_again_the_next_time()
    {
        var first = new InvalidOperationException("no window");
        var second = new InvalidOperationException("still no window");
        var calls = 0;
        var single = new SingleWindow(() => throw (++calls == 1 ? first : second), Logger.None);

        var firstThrown = Assert.Throws<InvalidOperationException>(single.Show);
        var secondThrown = Assert.Throws<InvalidOperationException>(single.Show);

        Assert.Same(first, firstThrown);
        Assert.Same(second, secondThrown);
        Assert.Equal(2, calls);
        Assert.False(single.IsOpen);
    }

    [WpfFact]
    public void Refuses_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new SingleWindow(() => new Window(), null!));
    }
}
