using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

[Unit]
public class ThemeFollowerArgumentTests
{
    [Fact]
    public void Rejects_a_null_element()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(null!, new FakeTheme(), () => { }));
    }

    [WpfFact]
    public void Rejects_a_null_theme()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(new FrameworkElement(), null!, () => { }));
    }

    [WpfFact]
    public void Rejects_a_null_callback()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(new FrameworkElement(), new FakeTheme(), null!));
    }
}
