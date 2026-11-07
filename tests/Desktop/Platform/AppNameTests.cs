using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class AppNameTests
{
    [Fact]
    public void Is_Memoria_ToDo()
    {
        Assert.Equal("Memoria-ToDo", AppName.Value);
    }
}
