using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.HoverCard;

using HoverCard = Memoria.ToDo.Desktop.Platform.HoverCard;

[Unit]
public class HoverCardArgumentTests
{
    private static Task<HoverPart?> NoPart(Point at) => Task.FromResult<HoverPart?>(null);

    [WpfFact]
    public void Rejects_a_null_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new HoverCard(null!, NoPart, new FakeDragPointer(), Serilog.Core.Logger.None));
    }

    [WpfFact]
    public void Rejects_a_null_part_finder()
    {
        Assert.Throws<ArgumentNullException>(() => new HoverCard(new FrameworkElement(), null!, new FakeDragPointer(), Serilog.Core.Logger.None));
    }

    [WpfFact]
    public void Rejects_a_null_pointer()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new HoverCard(new FrameworkElement(), NoPart, null!, Serilog.Core.Logger.None));

        Assert.Equal("pointer", error.ParamName);
    }

    [WpfFact]
    public void Rejects_a_null_logger()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new HoverCard(new FrameworkElement(), NoPart, new FakeDragPointer(), null!));

        Assert.Equal("logger", error.ParamName);
    }
}
