using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class HoverCardArgumentTests
{
    private static Task<HoverPart?> NoPart(Point at) => Task.FromResult<HoverPart?>(null);

    [WpfFact]
    public void Rejects_a_null_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new HoverCard(null!, NoPart));
    }

    [WpfFact]
    public void Rejects_a_null_part_finder()
    {
        Assert.Throws<ArgumentNullException>(() => new HoverCard(new FrameworkElement(), null!));
    }
}
