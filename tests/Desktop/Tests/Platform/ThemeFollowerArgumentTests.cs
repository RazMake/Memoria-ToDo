using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class ThemeFollowerArgumentTests
{
    [Fact]
    public void Rejects_a_null_element()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(null!, new FakeTheme(), () => { }));
    }
}
